"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowLeft, ArrowRight, CheckCircle2, Loader2 } from "lucide-react";

import { SignupSteps } from "@/components/marketing/signup-steps";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getPricingHref } from "@/lib/pricing-preview";
import {
  normalizePhone,
  normalizeTenantCode,
  readSignupDraft,
  TENANT_CODE_PATTERN,
  validateSignupInfo,
  writeSignupDraft,
  type SignupDraft,
  type SignupInfo,
} from "@/lib/signup-draft";
import { subscriptionApi } from "@/services/subscription-api";

type CodeCheck = { code: string; available: boolean; message: string | null } | null;

const EMPTY: SignupInfo = { tenantCode: "", tenantName: "", fullName: "", email: "", phone: "" };

const FIELDS: { name: keyof SignupInfo; label: string; type?: string; autoComplete?: string; hint?: string; maxLength: number }[] = [
  { name: "tenantCode", label: "Mã tổ chức", maxLength: 30, autoComplete: "off", hint: "Dùng để đăng nhập, ví dụ: cong-ty-abc. Chỉ gồm chữ thường, số và dấu gạch ngang." },
  { name: "tenantName", label: "Tên tổ chức", maxLength: 255, autoComplete: "organization" },
  { name: "fullName", label: "Họ tên đầy đủ", maxLength: 100, autoComplete: "name" },
  { name: "email", label: "Email", type: "email", maxLength: 50, autoComplete: "email", hint: "Tài khoản đăng nhập sẽ được gửi tới email này." },
  { name: "phone", label: "Số điện thoại", type: "tel", maxLength: 15, autoComplete: "tel" },
];

export function SignupInfoForm() {
  const router = useRouter();
  const [draft, setDraft] = useState<SignupDraft | null>(null);
  const [info, setInfo] = useState<SignupInfo>(EMPTY);
  const [errors, setErrors] = useState<Partial<Record<keyof SignupInfo, string>>>({});
  const [codeCheck, setCodeCheck] = useState<CodeCheck>(null);
  const [checking, setChecking] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const checkSeq = useRef(0);

  useEffect(() => {
    const saved = readSignupDraft();
    if (!saved) {
      router.replace("/pricing");
      return;
    }
    queueMicrotask(() => {
      setDraft(saved);
      setInfo(saved.info ?? EMPTY);
    });
  }, [router]);

  const checkCode = async (raw: string): Promise<CodeCheck> => {
    const code = normalizeTenantCode(raw);
    if (!TENANT_CODE_PATTERN.test(code)) return null;
    if (codeCheck?.code === code) return codeCheck;
    const seq = ++checkSeq.current;
    setChecking(true);
    try {
      const result = await subscriptionApi.checkTenantCode(code);
      const next = { code, available: result.available, message: result.message };
      if (seq === checkSeq.current) setCodeCheck(next);
      return next;
    } catch {
      return null; // lỗi mạng: để server kiểm tra lại khi checkout
    } finally {
      if (seq === checkSeq.current) setChecking(false);
    }
  };

  const update = (name: keyof SignupInfo, value: string) => {
    setInfo((current) => ({ ...current, [name]: value }));
    setErrors((current) => ({ ...current, [name]: undefined }));
    if (name === "tenantCode") setCodeCheck(null);
  };

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!draft || submitting) return;
    const validation = validateSignupInfo(info);
    setErrors(validation);
    if (Object.keys(validation).length > 0) return;
    setSubmitting(true);
    const check = await checkCode(info.tenantCode);
    if (check && !check.available) {
      setErrors({ tenantCode: check.message ?? "Mã tổ chức đã được sử dụng." });
      setSubmitting(false);
      return;
    }
    const normalized: SignupInfo = {
      tenantCode: normalizeTenantCode(info.tenantCode),
      tenantName: info.tenantName.trim(),
      fullName: info.fullName.trim(),
      email: info.email.trim().toLowerCase(),
      phone: normalizePhone(info.phone),
    };
    writeSignupDraft({ ...draft, info: normalized });
    router.push("/signup/cart");
  };

  if (!draft) {
    return <p role="status" className="flex items-center gap-2 px-6 py-20 text-sm text-muted-foreground"><Loader2 className="size-4 animate-spin" aria-hidden="true" />Đang tải...</p>;
  }

  const backHref = draft.plan.isPaid
    ? getPricingHref({ kind: "paid", months: Math.max(1, Math.round(draft.plan.durationDays / 30)) })
    : getPricingHref({ kind: "trial" });

  return (
    <div className="mx-auto max-w-2xl px-4 py-10 sm:px-6 sm:py-12">
      <SignupSteps current={1} />
      <Card>
        <CardHeader>
          <CardTitle className="text-2xl">Thông tin tổ chức</CardTitle>
          <p className="text-sm text-muted-foreground">Gói đã chọn: <span className="font-medium text-foreground">{draft.plan.label}</span></p>
        </CardHeader>
        <CardContent>
          <form noValidate onSubmit={submit} className="space-y-5">
            {FIELDS.map((field) => {
              const id = `signup-${field.name}`;
              const error = errors[field.name];
              const showAvailable = field.name === "tenantCode" && codeCheck?.available && codeCheck.code === normalizeTenantCode(info.tenantCode);
              return (
                <div key={field.name} className="space-y-2">
                  <Label htmlFor={id}>{field.label} <span className="text-destructive" aria-hidden="true">*</span></Label>
                  <Input
                    id={id}
                    name={field.name}
                    type={field.type ?? "text"}
                    value={info[field.name]}
                    maxLength={field.maxLength}
                    autoComplete={field.autoComplete}
                    required
                    aria-invalid={Boolean(error)}
                    aria-describedby={`${id}-hint ${id}-error`}
                    onChange={(event) => update(field.name, event.target.value)}
                    onBlur={field.name === "tenantCode" ? async () => {
                      const result = await checkCode(info.tenantCode);
                      if (result && !result.available) setErrors((current) => ({ ...current, tenantCode: result.message ?? undefined }));
                    } : undefined}
                  />
                  {field.hint && <p id={`${id}-hint`} className="text-xs text-muted-foreground">{field.hint}</p>}
                  {field.name === "tenantCode" && checking && <p role="status" className="flex items-center gap-1.5 text-xs text-muted-foreground"><Loader2 className="size-3.5 animate-spin" aria-hidden="true" />Đang kiểm tra mã...</p>}
                  {showAvailable && !error && <p role="status" className="flex items-center gap-1.5 text-xs text-green-600 dark:text-green-500"><CheckCircle2 className="size-3.5" aria-hidden="true" />Mã tổ chức có thể sử dụng.</p>}
                  {error && <p id={`${id}-error`} role="alert" className="text-xs text-destructive">{error}</p>}
                </div>
              );
            })}
            <div className="flex flex-col-reverse gap-3 pt-2 sm:flex-row sm:justify-between">
              <Button asChild variant="outline"><Link href={backHref}><ArrowLeft aria-hidden="true" />Quay lại</Link></Button>
              <Button type="submit" disabled={submitting}>
                {submitting ? <Loader2 className="animate-spin" aria-hidden="true" /> : null}Tiếp tục <ArrowRight aria-hidden="true" />
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
