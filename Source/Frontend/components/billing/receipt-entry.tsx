"use client";

import { useCallback, useEffect, useRef } from "react";
import { useRouter } from "next/navigation";
import { Loader2, ReceiptText } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useStatusPolling } from "@/hooks/use-status-polling";
import { invoicesApi, type InvoiceReadiness } from "@/services/invoices-api";
import { getAuthReturnPath } from "@/lib/auth-return";

const terminal = (value: InvoiceReadiness) => value.status !== "PENDING";

export function ReceiptEntry({ publicId }: { publicId: string }) {
  const request = useCallback((signal: AbortSignal) => invoicesApi.readiness(publicId, signal), [publicId]);
  const { value, error, timedOut, retry } = useStatusPolling(publicId, request, terminal);
  const redirected = useRef<string | null>(null);
  const router = useRouter();
  useEffect(() => {
    const path = getAuthReturnPath(value?.returnTo ?? null);
    if (value?.status !== "READY" || !path || redirected.current === publicId) return;
    redirected.current = publicId;
    router.replace(`${path}?${new URLSearchParams({ tenantCode: value.tenantCode ?? "" })}`);
  }, [value, router, publicId]);
  const unavailable = (error as { response?: { status?: number } })?.response?.status === 404;
  const waiting = !value || value.status === "PENDING";
  const title = unavailable ? "Liên kết chứng từ không hợp lệ"
    : timedOut ? "Xử lý lâu hơn bình thường"
    : value?.status === "READY" ? "Đang mở xác nhận thanh toán…"
    : value?.status === "PROVISIONING_FAILED" ? "Đã thanh toán, chưa tạo được tài khoản"
    : value?.status === "RECONCILIATION_REQUIRED" ? "Chứng từ cần đối chiếu thông tin người mua"
    : value?.status === "UNAVAILABLE" ? "Tổ chức hiện chưa thể truy cập. Vui lòng liên hệ hỗ trợ."
    : !waiting ? "Đơn đăng ký đã hủy hoặc thanh toán thất bại"
    : value?.isPaid ? "Đã thanh toán, đang tạo tài khoản…" : "Đang chờ xác nhận thanh toán…";
  return <section className="mx-auto my-12 max-w-xl space-y-5 rounded-2xl border bg-card p-6 text-center sm:p-10" aria-live="polite">
    {waiting && !timedOut && !unavailable ? <Loader2 className="mx-auto size-10 animate-spin text-primary" /> : <ReceiptText className="mx-auto size-10 text-primary" />}
    <h1 className="text-xl font-semibold">{title}</h1>
    {waiting && !unavailable && <p className="text-sm text-muted-foreground">Trang sẽ tự chuyển tiếp khi tài khoản sẵn sàng.</p>}
    {Boolean(error) && !unavailable && <p className="text-sm text-muted-foreground">Đang kết nối lại. Hệ thống sẽ tự thử lại.</p>}
    {timedOut && <Button onClick={retry}>Tiếp tục kiểm tra</Button>}
    {value?.status === "PROVISIONING_FAILED" && <p className="text-sm text-muted-foreground">Vui lòng liên hệ hỗ trợ để xử lý khoản thanh toán; không cần thanh toán lại.</p>}
  </section>;
}
