"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { CheckCircle2, CircleSlash, Loader2, MailCheck, TriangleAlert, XCircle } from "lucide-react";

import { SignupSteps } from "@/components/marketing/signup-steps";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { getApiErrorMessage } from "@/lib/api-error";
import { formatCurrency } from "@/lib/format-currency";
import { clearSignupDraft } from "@/lib/signup-draft";
import { subscriptionApi, type SubscriptionStatusResponse } from "@/services/subscription-api";

const POLL_MS = 4000;
const MAX_POLL_MS = 15 * 60 * 1000;

export function SignupResult() {
  const params = useSearchParams();
  const publicId = params.get("subscription") ?? "";
  // PayOS thêm cancel=true vào cancelUrl khi người dùng bấm hủy trên trang thanh toán.
  const cancelled = params.get("cancel") === "true";
  const [status, setStatus] = useState<SubscriptionStatusResponse | null>(null);
  const [error, setError] = useState("");
  const [timedOut, setTimedOut] = useState(false);
  const started = useRef(false);

  useEffect(() => {
    if (!publicId || started.current) return;
    started.current = true;
    let timer: ReturnType<typeof setTimeout> | undefined;
    let stopped = false;
    const startedAt = Date.now();

    const poll = async (first: boolean) => {
      try {
        const next = first && cancelled ? await subscriptionApi.cancel(publicId) : await subscriptionApi.getStatus(publicId);
        if (stopped) return;
        setStatus(next);
        setError("");
        if (next.status === "ACTIVE") clearSignupDraft();
        if (next.status !== "PENDING") return;
      } catch (err) {
        if (stopped) return;
        setError(getApiErrorMessage(err, "Không kiểm tra được trạng thái đơn. Đang thử lại..."));
      }
      if (Date.now() - startedAt > MAX_POLL_MS) {
        setTimedOut(true);
        return;
      }
      timer = setTimeout(() => void poll(false), POLL_MS);
    };

    void poll(true);
    return () => {
      stopped = true;
      clearTimeout(timer);
      started.current = false;
    };
  }, [publicId, cancelled]);

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
                title={status.isPaid ? "Đang khởi tạo tài khoản..." : "Đang chờ xác nhận thanh toán..."}
                text={timedOut ? "Hệ thống xử lý lâu hơn bình thường. Bạn có thể đóng trang này; email sẽ được gửi khi hoàn tất." : status.message}
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
