"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { CheckCircle2, Printer, ReceiptText } from "lucide-react";
import { Button } from "@/components/ui/button";
import { invoicesApi, type PaymentConfirmation } from "@/services/invoices-api";
import { authApi } from "@/services/auth-api";
import { useAuthStore } from "@/hooks/use-auth-store";
import { currentAuthHref } from "@/lib/auth-return";
import { formatCurrency } from "@/lib/format-currency";

export function PaymentReceipt({ publicId }: { publicId: string }) {
  const [data, setData] = useState<PaymentConfirmation | null>(null);
  const [error, setError] = useState<"forbidden" | "network" | null>(null);
  const [retry, setRetry] = useState(0);
  const [switching, setSwitching] = useState(false);
  const router = useRouter();
  useEffect(() => {
    const controller = new AbortController();
    invoicesApi.get(publicId, controller.signal).then(value => {
      if (!controller.signal.aborted) { setData(value); setError(null); }
    }).catch((err: { response?: { status?: number } }) => {
      if (!controller.signal.aborted) setError([403, 404].includes(err.response?.status ?? 0) ? "forbidden" : "network");
    });
    return () => controller.abort();
  }, [publicId, retry]);

  const switchAccount = async () => {
    setSwitching(true);
    try {
      const login = currentAuthHref("/");
      await authApi.logout();
      useAuthStore.getState().logout();
      router.replace(login);
    } catch { setError("network"); setSwitching(false); }
  };
  if (error) return <div className="m-auto max-w-lg space-y-4 p-6 text-center" role="alert">
    <h1 className="text-xl font-semibold">{error === "forbidden" ? "Không có quyền xem chứng từ" : "Chưa thể tải chứng từ"}</h1>
    <p className="text-muted-foreground">{error === "forbidden" ? "Chỉ tài khoản mua gói ban đầu của đúng tổ chức được xem xác nhận này." : "Vui lòng kiểm tra kết nối và thử lại."}</p>
    {error === "forbidden" ? <Button disabled={switching} onClick={switchAccount}>Đăng nhập bằng tài khoản khác</Button>
      : <Button onClick={() => { setError(null); setRetry(x => x + 1); }}>Thử lại</Button>}
  </div>;
  if (!data || data.publicId.toLowerCase() !== publicId.toLowerCase()) return <p className="m-auto p-6" role="status">Đang tải xác nhận thanh toán…</p>;
  const money = (amount: number) => formatCurrency(amount, data.currency);
  const paidDate = data.paidAt ? new Date(/Z$|[+-]\d\d:\d\d$/.test(data.paidAt) ? data.paidAt : `${data.paidAt}Z`).toLocaleString("vi-VN") : "—";
  return <div className="grow overflow-y-auto p-4 sm:p-8 print:overflow-visible print:p-0 print:text-black print:bg-white">
    <style media="print">{`
      @page { size: A4; margin: 16mm; }
      body:has([data-payment-receipt]) { height: auto !important; overflow: visible !important; background: white !important; }
      body:has([data-payment-receipt]) [data-sonner-toaster],
      body:has([data-payment-receipt]) nextjs-portal { display: none !important; }
      [data-payment-receipt], [data-payment-receipt] * { color: #111827 !important; box-shadow: none !important; }
      [data-payment-receipt] .text-muted-foreground { color: #475569 !important; }
      [data-payment-receipt] header p { background: transparent !important; }
    `}</style>
    <div className="mx-auto mb-5 flex max-w-3xl flex-wrap items-center justify-between gap-3 print:hidden">
      <h1 className="text-xl font-semibold">Xác nhận thanh toán</h1>
      <Button asChild variant="ghost"><Link href="/billing/invoices">Lịch sử thanh toán</Link></Button>
      <Button variant="outline" onClick={() => window.print()}><Printer className="mr-2 size-4" />In / Lưu PDF</Button>
    </div>
    <article className="mx-auto max-w-3xl space-y-7 rounded-2xl border border-border bg-card p-5 shadow-sm sm:p-10 print:max-w-none print:rounded-none print:border-0 print:bg-white print:p-0 print:shadow-none" data-payment-receipt>
      <header className="flex flex-wrap items-start justify-between gap-4 border-b pb-6">
        <div><p className="flex items-center gap-2 text-2xl font-bold text-primary print:text-black"><ReceiptText />eContract</p><p className="mt-1 text-sm text-muted-foreground">Một sản phẩm của DTC</p></div>
        <div className="sm:text-right"><p className="inline-flex items-center gap-2 rounded-full bg-emerald-500/10 px-3 py-1 text-sm font-medium text-emerald-700 dark:text-emerald-400 print:text-black"><CheckCircle2 className="size-4" />Đã thanh toán</p><p className="mt-2 break-all font-mono text-sm">{data.invoiceCode}</p></div>
      </header>
      <h2 className="text-2xl font-semibold">Xác nhận thanh toán</h2>
      <dl className="grid gap-x-8 gap-y-5 sm:grid-cols-2 print:grid-cols-2">
        <Field label="Tổ chức" value={data.billingName} /><Field label="Người mua" value={data.buyerName} />
        <Field label="Email thanh toán" value={data.billingEmail} /><Field label="Điện thoại" value={data.billingPhone} />
        {data.billingTaxIdentifier && <Field label="Mã số thuế" value={data.billingTaxIdentifier} />}
        {data.billingAddress && <Field label="Địa chỉ" value={data.billingAddress} />}
        <Field label="Gói dịch vụ" value={data.planName ?? "Dữ liệu cũ cần đối chiếu"} />
        <Field label="Thời hạn" value={data.durationMonths ? `${data.durationMonths} tháng` : data.durationDays ? `${data.durationDays} ngày` : "—"} />
        <Field label="Thời điểm thanh toán" value={paidDate} /><Field label="Phương thức" value={data.paymentMethod} />
        <Field label="Mã giao dịch" value={data.paymentReference} />
      </dl>
      <dl className="space-y-3 border-y py-5">
        <div className="flex justify-between gap-4"><dt>Giá gốc</dt><dd>{money(data.subtotal)}</dd></div>
        <div className="flex justify-between gap-4"><dt>Giảm giá</dt><dd>−{money(data.discountAmount)}</dd></div>
        <div className="flex flex-wrap justify-between gap-3 pt-2 text-xl font-bold"><dt>Tổng đã thanh toán</dt><dd>{money(data.totalAmount)}</dd></div>
      </dl>
      <p className="text-xs leading-5 text-muted-foreground print:text-gray-600">Chứng từ xác nhận thanh toán dịch vụ eContract. Không phải hóa đơn giá trị gia tăng.</p>
    </article>
  </div>;
}
function Field({ label, value }: { label: string; value: string | null }) {
  return <div className="min-w-0 break-words"><dt className="mb-1 text-sm text-muted-foreground">{label}</dt><dd className="font-medium">{value || "—"}</dd></div>;
}
