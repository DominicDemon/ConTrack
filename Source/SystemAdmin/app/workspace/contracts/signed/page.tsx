"use client";

import { Suspense, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import { dateTime, internalApi, money } from "@/services/internal-api";
import { useInternalUser } from "@/components/internal/internal-shell";
import { onNotificationRefresh } from "../../../../../Shared/notification-navigation";

type Entry = {
  documentId: string; versionId: string; code: string; name: string; customerName: string;
  employeeName: string; versionNo: string; signedAt: string | null;
  paperSigned: boolean;
  payment: { total: number; paid: number; paidInstallment: number | null };
};
type Page = { items: Entry[]; hasMore: boolean };
type Installment = { id: string; title: string; amount: number; dueDate: string | null; paid: boolean; paidAt: string | null };

function paymentLabel(payment: Entry["payment"]) {
  const { total, paid, paidInstallment } = payment;
  if (!total) return "Chưa có lịch thanh toán";
  if (!paid) return total > 2 ? `Chưa thanh toán (0/${total} đợt)` : "Chưa thanh toán";
  if (paid === total) return `Đã thanh toán đủ (${total === 1 ? "100%" : `${paid}/${total} đợt`})`;
  if (paidInstallment) return `Đã thanh toán đợt ${paidInstallment}`;
  return `Đã thanh toán ${paid}/${total} đợt`;
}

function PaymentDialog({ entry, close, onSaved }: { entry: Entry; close: () => void; onSaved: () => void }) {
  const path = `/signed-contracts/${entry.documentId}/versions/${entry.versionId}/payments`;
  const [items, setItems] = useState<Installment[] | null>(null);
  const [selected, setSelected] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  useEffect(() => {
    let active = true;
    internalApi<Installment[]>(path).then(rows => { if (active) setItems(rows); })
      .catch(e => { if (active) setError(e.message); });
    return () => { active = false; };
  }, [path]);
  async function confirm() {
    if (busy || !selected) return;
    setBusy(true); setError(""); setNotice("");
    try {
      await internalApi(`${path}/${selected}/confirm`, "POST");
      onSaved();
      setSelected("");
      setItems(await internalApi<Installment[]>(path));
      setNotice("Đã ghi nhận thanh toán và báo Sale phụ trách.");
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  return <Dialog open onOpenChange={open => { if (!open && !busy) close(); }}>
    <DialogContent>
      <DialogHeader><DialogTitle>Kiểm tra thanh toán</DialogTitle>
        <DialogDescription>{entry.code} · Phiên bản {entry.versionNo}</DialogDescription>
      </DialogHeader>
      {error && <p role="alert" className={ui.error}>{error}</p>}
      {notice && <p role="status">{notice}</p>}
      {!items && !error && <p role="status">Đang tải lịch thanh toán…</p>}
      {items?.length === 0 && <p>Chưa có lịch thanh toán.</p>}
      {!!items?.length && <fieldset disabled={busy} className="grid max-h-[50vh] gap-3 overflow-auto">
        <legend>Chọn đợt đã đối chiếu</legend>
        {items.map((row, index) => <label key={row.id} className={ui.card}>
          <input type="radio" name="payment-installment" disabled={row.paid}
            checked={selected === row.id} onChange={() => setSelected(row.id)}/> <strong>{row.title || `Đợt ${index + 1}`}</strong>
          <p>{money(row.amount)} ₫ · Hạn {row.dueDate ? row.dueDate.split("-").reverse().join("/") : "chưa xác định"}</p>
          <p>{row.paid ? `Đã thanh toán${row.paidAt ? ` · ${dateTime(row.paidAt)}` : ""}` : "Chưa thanh toán"}</p>
        </label>)}
      </fieldset>}
      {!!items?.some(row => !row.paid) && <button type="button" disabled={!selected || busy} className={ui.primaryButton}
        onClick={() => void confirm()}>{busy ? "Đang xác nhận…" : "Xác nhận và báo Sale"}</button>}
    </DialogContent>
  </Dialog>;
}

function SignedPdf({ entry, close }: { entry: Entry; close: () => void }) {
  const [url, setUrl] = useState("");
  const [error, setError] = useState("");
  useEffect(() => {
    const controller = new AbortController();
    let fileUrl = "";
    async function open() {
      try {
        const response = await fetch(`/api/internal/signed-contracts/${entry.documentId}/versions/${entry.versionId}/pdf`, {
          cache: "no-store", signal: controller.signal,
        });
        if (!response.ok) {
          const body = await response.json().catch(() => null);
          throw new Error(body?.message || "Không mở được tệp PDF của phiên bản này.");
        }
        if (!response.headers.get("content-type")?.startsWith("application/pdf")) throw new Error("Máy chủ chưa trả về tệp PDF.");
        const blob = await response.blob();
        if (controller.signal.aborted) return;
        fileUrl = URL.createObjectURL(blob);
        setUrl(fileUrl);
      } catch (e) { if (!controller.signal.aborted) setError((e as Error).message); }
    }
    void open();
    return () => { controller.abort(); if (fileUrl) URL.revokeObjectURL(fileUrl); };
  }, [entry]);
  return <Dialog open onOpenChange={open => { if (!open) close(); }}>
    <DialogContent className="w-[95vw] sm:max-w-6xl">
      <DialogHeader><DialogTitle>{entry.name}</DialogTitle>
        <DialogDescription>{entry.code} · Phiên bản {entry.versionNo} · {entry.customerName}</DialogDescription>
      </DialogHeader>
      {error ? <p className={ui.error} role="alert">{error}</p> : url ? <>
        <iframe title="Bản PDF đã ký của hai bên" src={url} className="h-[65vh] w-full rounded border"/>
        <a className={ui.primaryButton} download={`${entry.code || entry.documentId}-v${entry.versionNo}-da-ky.pdf`} href={url}>Tải PDF</a>
      </> : <p role="status">Đang mở bản đã ký…</p>}
    </DialogContent>
  </Dialog>;
}

export default function SignedContractsPage() {
  return <Suspense fallback={<p role="status">Đang tải hợp đồng…</p>}><SignedContracts/></Suspense>;
}

function SignedContracts() {
  const documentId = useSearchParams().get("documentId");
  return <SignedContractsTable key={documentId} documentId={documentId}/>;
}

function SignedContractsTable({ documentId }: { documentId: string | null }) {
  const { canCheck, permissions } = useInternalUser();
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<Page | null>(null);
  const [error, setError] = useState("");
  const [selected, setSelected] = useState<Entry | null>(null);
  const [paymentEntry, setPaymentEntry] = useState<Entry | null>(null);
  const [refresh, setRefresh] = useState(0);
  useEffect(() => onNotificationRefresh(() => setRefresh(n => n + 1)), []);
  useEffect(() => {
    let active = true;
    setResult(null); setError("");
    const timer = setTimeout(() => {
      const params = new URLSearchParams({ page: String(page), search: search.trim() });
      if (documentId) params.set("documentId", documentId);
      void internalApi<Page>(`/signed-contracts?${params}`).then(data => { if (active) setResult(data); })
        .catch(e => { if (active) setError(e.message); });
    }, 200);
    return () => { active = false; clearTimeout(timer); };
  }, [search, page, refresh, documentId]);
  return <main className={s.page}>
    <div className={s.titleRow}><h1>Hợp đồng đã ký</h1></div>
    <section className={ui.card}>
      <div className={s.toolbar}>{documentId ? <Link href="/workspace/contracts/signed">Xem tất cả hợp đồng đã ký</Link> : <input className={ui.field} type="search" aria-label="Tìm hợp đồng đã ký"
        placeholder="Tìm theo số hợp đồng, tên hợp đồng hoặc khách hàng…" value={search}
        onChange={e => { setSearch(e.target.value); setPage(1); }}/>}</div>
      {error ? <p role="alert" className={ui.error}>{error}</p> : !result ? <p className={s.empty}>Đang tải hợp đồng…</p>
        : result.items.length === 0 ? <p className={s.empty}>{search ? "Không tìm thấy hợp đồng phù hợp." : "Chưa có hợp đồng đã ký."}</p>
        : <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Hợp đồng</th><th>Khách hàng</th><th>Sale phụ trách</th><th>Phiên bản</th><th>Ký hoàn tất</th><th>Thanh toán</th><th></th></tr></thead>
          <tbody>{result.items.map(row => <tr key={row.versionId}><td><strong>{row.name}</strong><div className={s.muted}>{row.code}</div></td>
            <td>{row.customerName}</td><td>{row.employeeName}</td><td>v{row.versionNo}</td><td>{row.signedAt ? dateTime(row.signedAt) : "—"}</td>
            <td><span className={s.badge}>{paymentLabel(row.payment)}</span>
              {canCheck && row.payment.total > 0 && <div><button type="button" className={ui.secondaryButton} onClick={() => setPaymentEntry(row)}>Kiểm tra thanh toán</button></div>}</td>
            <td>{row.paperSigned ? (permissions.includes("contract.signed_evidence.view")
              ? <Link className={ui.secondaryButton} href={`/workspace/signed-contracts/${row.documentId}`}>Mở hồ sơ ký giấy</Link>
              : <span className={s.muted}>Đã xác nhận bản ký giấy</span>)
              : <button className={ui.secondaryButton} onClick={() => setSelected(row)}>Xem bản đã ký</button>}</td></tr>)}</tbody></table></div>}
      {(page > 1 || result?.hasMore) && <nav aria-label="Phân trang" className={s.toolbar}>
        <button className={ui.secondaryButton} disabled={page === 1} onClick={() => setPage(page - 1)}>Trang trước</button>
        <span>Trang {page}</span><button className={ui.secondaryButton} disabled={!result?.hasMore} onClick={() => setPage(page + 1)}>Trang sau</button>
      </nav>}
    </section>
    {selected && <SignedPdf entry={selected} close={() => setSelected(null)}/>}
    {paymentEntry && <PaymentDialog entry={paymentEntry} close={() => setPaymentEntry(null)} onSaved={() => setRefresh(n => n + 1)}/>}
  </main>;
}
