"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ArrowUpRight, CheckCircle2, Loader2, ReceiptText } from "lucide-react";
import { Header } from "@/components/ui/custom/header";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { invoicesApi, type InvoiceListResponse } from "@/services/invoices-api";
import { formatCurrency } from "@/lib/format-currency";

function date(value: string | null) {
  if (!value) return "—";
  return new Date(/Z$|[+-]\d\d:\d\d$/.test(value) ? value : `${value}Z`).toLocaleDateString("vi-VN");
}

export function InvoiceHistory() {
  const [page, setPage] = useState(1);
  const [retry, setRetry] = useState(0);
  const [result, setResult] = useState<{ page: number; data?: InvoiceListResponse; error?: boolean } | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    invoicesApi.list(page, controller.signal).then(data => {
      if (!controller.signal.aborted) setResult({ page, data });
    }).catch(() => {
      if (!controller.signal.aborted) setResult({ page, error: true });
    });
    return () => controller.abort();
  }, [page, retry]);
  const current = result?.page === page ? result : null;
  const data = current?.data;
  return <>
    <Header title="Gói dịch vụ & Thanh toán" />
    <div className="grow overflow-y-auto bg-background/50">
      <div className="mx-auto max-w-5xl space-y-6 px-4 py-6 sm:px-6 lg:py-8">
        <div className="space-y-2">
          <h1 className="text-2xl font-semibold tracking-tight">Gói dịch vụ & Thanh toán</h1>
          <p className="text-sm leading-6 text-muted-foreground">Xem các gói đã mua và tải chứng từ xác nhận thanh toán của bạn.</p>
        </div>
        {!current ? <div className="flex items-center justify-center gap-2 rounded-xl border bg-card p-12" role="status"><Loader2 className="size-5 animate-spin text-primary" />Đang tải lịch sử thanh toán…</div>
          : current.error ? <div className="space-y-4 rounded-xl border bg-card p-8 text-center" role="alert">
            <p>Chưa thể tải lịch sử thanh toán. Vui lòng thử lại.</p>
            <Button variant="outline" onClick={() => { setResult(null); setRetry(x => x + 1); }}>Thử lại</Button>
          </div>
          : data?.items.length === 0 ? <div className="space-y-3 rounded-xl border bg-card px-6 py-12 text-center">
            <ReceiptText className="mx-auto size-10 text-muted-foreground" />
            <h2 className="text-lg font-semibold">Chưa có giao dịch thanh toán</h2>
            <p className="mx-auto max-w-md text-sm leading-6 text-muted-foreground">Chứng từ chỉ hiển thị cho tài khoản mua gói ban đầu của tổ chức. Gói dùng thử miễn phí không có trong danh sách này.</p>
          </div>
          : data && <>
            <div className="flex items-center justify-between gap-3">
              <h2 className="font-semibold">Lịch sử mua gói</h2>
              <span className="text-sm text-muted-foreground">{data.total} giao dịch</span>
            </div>
            <ul className="space-y-4">
              {data.items.map(invoice => <li key={invoice.publicId} className="space-y-5 rounded-xl border border-border bg-card p-5 shadow-sm sm:p-6">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0 space-y-1">
                    <h3 className="text-lg font-semibold">{invoice.planName || (invoice.durationMonths ? `Gói ${invoice.durationMonths} tháng` : "Gói dịch vụ đã mua")}</h3>
                    <p className="break-all font-mono text-xs text-muted-foreground">{invoice.invoiceCode}</p>
                  </div>
                  <Badge variant="secondary" className="gap-1.5 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400"><CheckCircle2 className="size-3.5" />Đã thanh toán</Badge>
                </div>
                <dl className="grid gap-4 text-sm sm:grid-cols-3">
                  <div><dt className="mb-1 text-muted-foreground">Ngày thanh toán</dt><dd>{date(invoice.paidAt)}</dd></div>
                  <div><dt className="mb-1 text-muted-foreground">Thời hạn sử dụng</dt><dd>{date(invoice.startAt)} – {date(invoice.endAt)}</dd></div>
                  <div><dt className="mb-1 text-muted-foreground">Tổng thanh toán</dt><dd className="text-lg font-semibold">{formatCurrency(invoice.totalAmount, invoice.currency)}</dd></div>
                </dl>
                <div className="flex justify-end border-t pt-4">
                  <Button asChild variant="outline" className="w-full sm:w-auto"><Link href={`/billing/invoices/${invoice.publicId}`} aria-label={`Xem chứng từ ${invoice.invoiceCode}`}>Xem chứng từ<ArrowUpRight className="ml-2 size-4" /></Link></Button>
                </div>
              </li>)}
            </ul>
            {data.total > data.pageSize && <nav aria-label="Phân trang lịch sử thanh toán" className="flex items-center justify-center gap-3">
              <Button variant="outline" disabled={data.page <= 1} onClick={() => { setResult(null); setPage(data.page - 1); }}>Trước</Button>
              <span className="text-sm">{data.page} / {Math.ceil(data.total / data.pageSize)}</span>
              <Button variant="outline" disabled={data.page * data.pageSize >= data.total} onClick={() => { setResult(null); setPage(data.page + 1); }}>Sau</Button>
            </nav>}
          </>}
      </div>
    </div>
  </>;
}
