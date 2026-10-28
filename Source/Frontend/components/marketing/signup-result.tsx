"use client";

import { useCallback, useEffect, useRef } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { CheckCircle2, CircleSlash, Loader2, MailCheck, TriangleAlert, XCircle } from "lucide-react";

import { SignupSteps } from "@/components/marketing/signup-steps";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { getApiErrorMessage } from "@/lib/api-error";
import { formatCurrency } from "@/lib/format-currency";
import { clearSignupDraft } from "@/lib/signup-draft";
import { subscriptionApi, type SubscriptionStatusResponse } from "@/services/subscription-api";

import { useStatusPolling } from "@/hooks/use-status-polling";

const terminal = (value: SubscriptionStatusResponse) => value.status !== "PENDING";

export function SignupResult() {
  const params = useSearchParams();
  const publicId = params.get("subscription") ?? "";
  // PayOS thêm cancel=true vào cancelUrl khi người dùng bấm hủy trên trang thanh toán.
  const cancelled = params.get("cancel") === "true";
  const router = useRouter();
  const redirected = useRef<string | null>(null);
  const cancelRequests = useRef(new Map<string, Promise<SubscriptionStatusResponse>>());
  const request = useCallback(async (signal: AbortSignal) => {
    if (cancelled) {
      // The mutation is idempotent and shared across Strict Mode probes; only reads are aborted.
      if (!cancelRequests.current.has(publicId)) {
        cancelRequests.current.set(publicId, subscriptionApi.cancel(publicId).catch(error => {
          cancelRequests.current.delete(publicId);
          throw error;
        }));
      }
      await cancelRequests.current.get(publicId);
      signal.throwIfAborted();
    }
    return subscriptionApi.getStatus(publicId, signal);
  }, [publicId, cancelled]);
  const { value: status, error: pollError, timedOut, retry } = useStatusPolling(publicId, request, terminal);
  const error = pollError ? getApiErrorMessage(pollError, "Đang kết nối lại. Hệ thống sẽ tự thử lại...") : "";
  useEffect(() => {
    if (status?.status !== "ACTIVE") return;
    clearSignupDraft();
    if (status.receiptPath && /^\/billing\/receipts\/[0-9a-f-]{36}$/i.test(status.receiptPath) && redirected.current !== publicId) {
      redirected.current = publicId;
      router.replace(status.receiptPath);
    }
  }, [status, publicId, router]);

  return (
    <div className="mx-auto max-w-xl px-4 py-10 sm:px-6 sm:py-12">
      <SignupSteps current={4} />
      <Card>
        <CardContent className="space-y-5 py-8 text-center" aria-live="polite">
          {!publicId ? (
            <Outcome icon={<XCircle className="size-12 text-destructive" />} title="Thiếu mã đơn đăng ký" text="Liên kết không hợp lệ. Vui lòng đăng ký lại từ trang Gói dịch vụ." />
          ) : !status ? (
            error
              ? <Outcome icon={<TriangleAlert className="size-12 text-amber-500" />} title="Đang kết nối lại" text={error} />
              : <Outcome icon={<Loader2 className="size-12 animate-spin text-primary" />} title="Đang kiểm tra đơn đăng ký..." />
          ) : status.status === "ACTIVE" ? (
            <>
              <Outcome icon={<MailCheck className="size-12 text-green-600 dark:text-green-500" />} title="Đăng ký thành công!" text="Vui lòng kiểm tra email để nhận tài khoản đăng nhập." />
              <p className="text-sm">Mã tổ chức của bạn: <span className="font-mono font-semibold">{status.tenantCode}</span></p>
              <Button asChild><Link href={`/?tenantCode=${encodeURIComponent(status.tenantCode)}`}>Đến trang đăng nhập</Link></Button>
            </>
          ) : status.status === "PENDING" ? (
            <>
              <Outcome
                icon={<Loader2 className="size-12 animate-spin text-primary" />}
                title={status.isPaid ? "Đã thanh toán, đang tạo tài khoản…" : "Đang chờ xác nhận thanh toán..."}
                text={timedOut ? "Xử lý lâu hơn bình thường. Bạn có thể tiếp tục kiểm tra ngay bên dưới." : status.message}
              />
              {!status.isPaid && status.checkoutUrl && (
                <Button asChild variant="outline"><a href={status.checkoutUrl}>Tiếp tục thanh toán {formatCurrency(status.total, status.currency)}</a></Button>
              )}
            </>
          ) : status.status === "PROVISIONING_FAILED" ? (
            <Outcome icon={<TriangleAlert className="size-12 text-amber-500" />} title="Đã thanh toán, chưa tạo được tài khoản" text={`${status.message} Mã đơn: ${status.invoiceCode}.`} />
          ) : (
            <>
              <Outcome
                icon={status.status === "CANCELLED" ? <CircleSlash className="size-12 text-muted-foreground" /> : <XCircle className="size-12 text-destructive" />}
                title={status.status === "CANCELLED" ? "Đơn đăng ký đã bị hủy" : "Thanh toán không thành công"}
                text={`${status.message} Bạn chưa bị trừ tiền cho đơn này.`}
              />
              <div className="flex flex-col justify-center gap-3 sm:flex-row">
                <Button asChild><Link href="/signup/cart">Thử lại</Link></Button>
                <Button asChild variant="outline"><Link href="/pricing">Chọn gói khác</Link></Button>
              </div>
            </>
          )}
          {Boolean(error) && status?.status === "PENDING" && <p className="text-sm text-muted-foreground">{error}</p>}
          {timedOut && <div className="space-y-3"><p className="text-sm">Xử lý lâu hơn bình thường.</p><Button onClick={retry}>Tiếp tục kiểm tra</Button></div>}
          {status?.status === "ACTIVE" && <p className="flex items-center justify-center gap-1.5 text-xs text-muted-foreground"><CheckCircle2 className="size-3.5" aria-hidden="true" />Hệ thống sẽ yêu cầu đổi mật khẩu ở lần đăng nhập đầu tiên.</p>}
        </CardContent>
      </Card>
    </div>
  );
}

function Outcome({ icon, title, text }: { icon: React.ReactNode; title: string; text?: string }) {
  return (
    <div className="flex flex-col items-center gap-3">
      <span aria-hidden="true">{icon}</span>
      <h1 className="text-xl font-semibold">{title}</h1>
      {text && <p className="max-w-md text-sm leading-6 text-muted-foreground">{text}</p>}
    </div>
  );
}
