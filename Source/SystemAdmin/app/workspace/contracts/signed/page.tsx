"use client";

import { useEffect, useState } from "react";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import { dateTime, internalApi } from "@/services/internal-api";

type Entry = {
  documentId: string; versionId: string; code: string; name: string; customerName: string;
  employeeName: string; versionNo: string; signedAt: string | null;
};
type Page = { items: Entry[]; hasMore: boolean };

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

export default function SignedContracts() {
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<Page | null>(null);
  const [error, setError] = useState("");
  const [selected, setSelected] = useState<Entry | null>(null);
  useEffect(() => {
    let active = true;
    setResult(null); setError("");
    const timer = setTimeout(() => {
      const params = new URLSearchParams({ page: String(page), search: search.trim() });
      void internalApi<Page>(`/signed-contracts?${params}`).then(data => { if (active) setResult(data); })
        .catch(e => { if (active) setError(e.message); });
    }, 200);
    return () => { active = false; clearTimeout(timer); };
  }, [search, page]);
  return <main className={s.page}>
    <div className={s.titleRow}><h1>Hợp đồng đã ký</h1></div>
    <section className={ui.card}>
      <div className={s.toolbar}><input className={ui.field} type="search" aria-label="Tìm hợp đồng đã ký"
        placeholder="Tìm theo số hợp đồng, tên hợp đồng hoặc khách hàng…" value={search}
        onChange={e => { setSearch(e.target.value); setPage(1); }}/></div>
      {error ? <p role="alert" className={ui.error}>{error}</p> : !result ? <p className={s.empty}>Đang tải hợp đồng…</p>
        : result.items.length === 0 ? <p className={s.empty}>{search ? "Không tìm thấy hợp đồng phù hợp." : "Chưa có hợp đồng đã ký."}</p>
        : <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Hợp đồng</th><th>Khách hàng</th><th>Sale phụ trách</th><th>Phiên bản</th><th>Ký hoàn tất</th><th></th></tr></thead>
          <tbody>{result.items.map(row => <tr key={row.versionId}><td><strong>{row.name}</strong><div className={s.muted}>{row.code}</div></td>
            <td>{row.customerName}</td><td>{row.employeeName}</td><td>v{row.versionNo}</td><td>{row.signedAt ? dateTime(row.signedAt) : "—"}</td>
            <td><button className={ui.secondaryButton} onClick={() => setSelected(row)}>Xem bản đã ký</button></td></tr>)}</tbody></table></div>}
      {(page > 1 || result?.hasMore) && <nav aria-label="Phân trang" className={s.toolbar}>
        <button className={ui.secondaryButton} disabled={page === 1} onClick={() => setPage(page - 1)}>Trang trước</button>
        <span>Trang {page}</span><button className={ui.secondaryButton} disabled={!result?.hasMore} onClick={() => setPage(page + 1)}>Trang sau</button>
      </nav>}
    </section>
    {selected && <SignedPdf entry={selected} close={() => setSelected(null)}/>}
  </main>;
}
