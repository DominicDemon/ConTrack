"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowLeft, Loader2, QrCode, RefreshCw, TicketPercent, X } from "lucide-react";

import { SignupSteps } from "@/components/marketing/signup-steps";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getApiErrorCode, getApiErrorMessage } from "@/lib/api-error";
import { formatCurrency } from "@/lib/format-currency";
import { readSignupDraft, writeSignupDraft, type SignupDraft } from "@/lib/signup-draft";
import { subscriptionApi, type VoucherPreviewResponse } from "@/services/subscription-api";

const INFO_ROWS = [
  ["Mã tổ chức", "tenantCode"],
  ["Tên tổ chức", "tenantName"],
  ["Họ tên", "fullName"],
  ["Email", "email"],
  ["Số điện thoại", "phone"],
] as const;

export function SignupCart() {
  const router = useRouter();
  const [draft, setDraft] = useState<SignupDraft | null>(null);
  const [price, setPrice] = useState<VoucherPreviewResponse | null>(null);
  const [priceError, setPriceError] = useState("");
  const [voucherInput, setVoucherInput] = useState("");
  const [voucherError, setVoucherError] = useState("");
  const [applying, setApplying] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [acceptedTerms, setAcceptedTerms] = useState(false);
  const [termsError, setTermsError] = useState("");
  const termsRef = useRef<HTMLButtonElement>(null);
  const [submitError, setSubmitError] = useState<{ message: string; editInfo: boolean } | null>(null);
  const submitLock = useRef(false);

  const loadPrice = async (current: SignupDraft) => {
    setPriceError("");
    try {
      setPrice(await subscriptionApi.previewVoucher(current.plan.planVersionId, current.voucherCode ?? null));
    } catch (error) {
      if (current.voucherCode) {
        // Voucher đã lưu không còn hợp lệ (hết hạn/hết lượt): bỏ voucher và tính lại.
        const withoutVoucher = { ...current, voucherCode: undefined };
        writeSignupDraft(withoutVoucher);
        setDraft(withoutVoucher);
        setVoucherError(getApiErrorMessage(error, "Mã giảm giá không còn hợp lệ."));
        return loadPrice(withoutVoucher);
      }
      setPriceError(getApiErrorMessage(error, "Không tính được tổng tiền. Vui lòng thử lại."));
    }
  };

  useEffect(() => {
    const saved = readSignupDraft();
    if (!saved?.info) {
      router.replace(saved ? "/signup/info" : "/pricing");
      return;
    }
    queueMicrotask(() => {
      setDraft(saved);
      setVoucherInput(saved.voucherCode ?? "");
      void loadPrice(saved);
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- chỉ đọc nháp một lần khi vào trang
  }, [router]);

  const applyVoucher = async (code: string | undefined) => {
    if (!draft) return;
    setApplying(true);
    setVoucherError("");
    try {
      const result = await subscriptionApi.previewVoucher(draft.plan.planVersionId, code ?? null);
      const next = { ...draft, voucherCode: result.voucherCode ?? undefined };
      writeSignupDraft(next);
      setDraft(next);
      setPrice(result);
      setVoucherInput(result.voucherCode ?? "");
    } catch (error) {
      setVoucherError(getApiErrorMessage(error, "Không áp dụng được mã giảm giá."));
    } finally {
      setApplying(false);
    }
  };

  const confirm = async () => {
    if (!draft?.info || !price || submitLock.current) return;
    if (!acceptedTerms) {
      setTermsError("Vui lòng đồng ý với Điều khoản dịch vụ để tiếp tục.");
      termsRef.current?.focus();
      return;
    }
    submitLock.current = true;
    setSubmitting(true);
    setSubmitError(null);
    try {
      const result = await subscriptionApi.checkout({ planVersionId: draft.plan.planVersionId, ...draft.info, voucherCode: draft.voucherCode ?? null });
      if (result.checkoutUrl) {
        window.location.assign(result.checkoutUrl); // màn (4): trang QR của PayOS
        return;
      }
      router.push(`/signup/result?subscription=${encodeURIComponent(result.publicId)}`);
    } catch (error) {
      const code = getApiErrorCode(error) ?? "";
      setSubmitError({
        message: getApiErrorMessage(error, "Không tạo được đơn đăng ký. Vui lòng thử lại."),
        editInfo: code.startsWith("SIGNUP_") || code.startsWith("TENANT_CODE") || code === "TRIAL_ALREADY_USED",
      });
      submitLock.current = false;
      setSubmitting(false);
    }
  };

  if (!draft?.info) {
    return <p role="status" className="flex items-center gap-2 px-6 py-20 text-sm text-muted-foreground"><Loader2 className="size-4 animate-spin" aria-hidden="true" />Đang tải...</p>;
  }

  const currency = price?.currency ?? draft.plan.currency;
  const isFree = price ? price.total === 0 : !draft.plan.isPaid;

  return (
    <div className="mx-auto max-w-4xl px-4 py-10 sm:px-6 sm:py-12">
      <SignupSteps current={2} />
      <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
        <Card>
          <CardHeader>
            <CardTitle className="text-2xl">Xác nhận đơn đăng ký</CardTitle>
            <p className="text-sm text-muted-foreground">Kiểm tra lại thông tin trước khi thanh toán.</p>
          </CardHeader>
          <CardContent className="space-y-6">
            <dl className="divide-y divide-border rounded-lg border border-border text-sm">
              <div className="flex justify-between gap-4 p-3"><dt className="text-muted-foreground">Gói</dt><dd className="text-right font-medium">{draft.plan.label}</dd></div>
              {INFO_ROWS.map(([label, key]) => (
                <div key={key} className="flex justify-between gap-4 p-3"><dt className="text-muted-foreground">{label}</dt><dd className="min-w-0 break-words text-right font-medium">{draft.info![key]}</dd></div>
              ))}
            </dl>
            <fieldset className="space-y-3">
              <legend className="mb-3 text-sm font-semibold">Phương thức thanh toán</legend>
              <label className="flex items-center gap-3 rounded-lg border border-primary bg-primary/5 p-3 text-sm">
                <input type="radio" name="payment-method" checked readOnly className="accent-primary" />
                <QrCode className="size-5 text-primary" aria-hidden="true" />
                <span>{isFree ? "Miễn phí – không cần thanh toán" : "Chuyển khoản QR qua PayOS"}</span>
              </label>
            </fieldset>
          </CardContent>
        </Card>

        <Card className="border-primary/20">
          <CardHeader><CardTitle className="text-lg">Tổng tiền</CardTitle></CardHeader>
          <CardContent className="space-y-5">
            {draft.plan.isPaid && (
              <form className="space-y-2" onSubmit={(event) => { event.preventDefault(); if (voucherInput.trim()) void applyVoucher(voucherInput); }}>
                <Label htmlFor="voucher">Mã giảm giá</Label>
                <div className="flex gap-2">
                  <Input id="voucher" value={voucherInput} maxLength={50} autoComplete="off" disabled={Boolean(draft.voucherCode)}
                    aria-invalid={Boolean(voucherError)} aria-describedby="voucher-error"
                    onChange={(event) => { setVoucherInput(event.target.value.toUpperCase()); setVoucherError(""); }} />
                  {draft.voucherCode ? (
                    <Button type="button" variant="outline" disabled={applying} onClick={() => void applyVoucher(undefined)} aria-label="Bỏ mã giảm giá"><X aria-hidden="true" /></Button>
                  ) : (
                    <Button type="submit" variant="outline" disabled={applying || !voucherInput.trim()}>
                      {applying ? <Loader2 className="animate-spin" aria-hidden="true" /> : <TicketPercent aria-hidden="true" />}Áp dụng
                    </Button>
                  )}
                </div>
                {voucherError && <p id="voucher-error" role="alert" className="text-xs text-destructive">{voucherError}</p>}
              </form>
            )}
            {priceError ? (
              <Alert variant="destructive">
                <AlertDescription>
                  <p>{priceError}</p>
                  <Button type="button" variant="outline" size="sm" className="mt-3" onClick={() => void loadPrice(draft)}><RefreshCw aria-hidden="true" />Thử lại</Button>
                </AlertDescription>
              </Alert>
            ) : !price ? (
              <p role="status" className="flex items-center gap-2 text-sm text-muted-foreground"><Loader2 className="size-4 animate-spin" aria-hidden="true" />Đang tính tổng tiền...</p>
            ) : (
              <dl className="space-y-3 text-sm" aria-live="polite">
                <div className="flex justify-between"><dt className="text-muted-foreground">Tạm tính</dt><dd className="tabular-nums">{formatCurrency(price.subtotal, currency)}</dd></div>
                <div className="flex justify-between"><dt className="text-muted-foreground">Giảm giá{price.voucherCode ? ` (${price.voucherCode})` : ""}</dt><dd className="tabular-nums">{price.discount ? `−${formatCurrency(price.discount, currency)}` : "—"}</dd></div>
                <div className="flex items-baseline justify-between border-t border-border pt-3"><dt className="font-semibold">Tổng thanh toán</dt><dd className="text-2xl font-bold tabular-nums">{formatCurrency(price.total, currency)}</dd></div>
              </dl>
            )}
            {submitError && (
              <Alert variant="destructive">
                <AlertTitle>Không thể đặt đơn</AlertTitle>
                <AlertDescription>
                  <p>{submitError.message}</p>
                  {submitError.editInfo && <Link href="/signup/info" className="mt-2 inline-block font-medium underline underline-offset-4">Sửa thông tin</Link>}
                </AlertDescription>
              </Alert>
            )}
            <div className="space-y-2">
              <div className="flex items-start gap-2.5">
                <Checkbox
                  ref={termsRef}
                  id="accept-terms"
                  className="mt-0.5"
                  checked={acceptedTerms}
                  disabled={submitting}
                  aria-invalid={Boolean(termsError)}
                  aria-describedby={termsError ? "accept-terms-error" : undefined}
                  onCheckedChange={(checked) => {
                    setAcceptedTerms(checked === true);
                    setTermsError("");
                  }}
                />
                <label htmlFor="accept-terms" className="cursor-pointer text-sm leading-5 text-muted-foreground">
                  Tôi đồng ý với <span className="font-medium text-foreground">Điều khoản dịch vụ</span> và đã đọc <span className="font-medium text-foreground">Chính sách bảo mật</span> của eContract.
                </label>
              </div>
              {termsError && <p id="accept-terms-error" role="alert" className="text-xs text-destructive">{termsError}</p>}
            </div>
            <Button type="button" className="w-full" disabled={!price || submitting || applying} onClick={() => void confirm()}>
              {submitting && <Loader2 className="animate-spin" aria-hidden="true" />}
              {isFree ? "Xác nhận đăng ký" : "Xác nhận và thanh toán"}
            </Button>
            <Button asChild variant="outline" className="w-full"><Link href="/signup/info"><ArrowLeft aria-hidden="true" />Quay lại</Link></Button>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
