"use client";
import { ListPager } from "@/components/scanned-contracts/list-pager";
import ContractTabs from "@/components/internal/contract-tabs";
import { SortHeader, useListView } from "@/components/internal/list-tools";

import { useCallback, useEffect, useRef, useState } from "react";
import { Check, FileSignature, RefreshCw, Trash2 } from "lucide-react";
import { internalApi, type CompanySigningQueueItem } from "@/services/internal-api";
import WordPdfViewer from "@/components/internal/word-pdf-viewer";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";

export default function CompanySigningQueuePage() {
  const [search, setSearch] = useState(""); const [signer, setSigner] = useState("");
  const [rows, setRows] = useState<CompanySigningQueueItem[]>([]);
  const [selected, setSelected] = useState<CompanySigningQueueItem | null>(null);
  const [documentUrl, setDocumentUrl] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const [hasInk, setHasInk] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const drawingRef = useRef(false);

  const load = useCallback(async () => {
    setError("");
    try { setRows(await internalApi<CompanySigningQueueItem[]>("/company-signing/queue")); }
    catch (e) { setError((e as Error).message); }
  }, []);
  useEffect(() => { void load(); }, [load]);
  const list = useListView(rows, { search, fields: r => [r.code, r.contractName, r.customerName], filterKey: signer,
    filter: r => !signer || r.signerName === signer,
    sorts: { code: r => r.code, name: r => r.contractName, customer: r => r.customerName, signer: r => r.signerName, created: r => r.createdAt },
    initial: { key: "created", dir: "asc" } });
  const signers = [...new Set(rows.map(r => r.signerName))].sort((a, b) => a.localeCompare(b, "vi"));
  useEffect(() => {
    if (!selected) { setDocumentUrl(""); return; }
    let active = true;
    let url = "";
    void fetch(`/api/internal/company-signing/${encodeURIComponent(selected.signatureId)}/document`, { credentials: "same-origin", cache: "no-store" })
      .then(async response => { if (!response.ok) throw new Error("Không tải được PDF hợp đồng."); return response.blob(); })
      .then(blob => { if (!active) return; url = URL.createObjectURL(blob); setDocumentUrl(url); })
      .catch(e => { if (active) setError((e as Error).message); });
    return () => { active = false; if (url) URL.revokeObjectURL(url); };
  }, [selected]);

  function point(event: React.PointerEvent<HTMLCanvasElement>) {
    const canvas = event.currentTarget;
    const bounds = canvas.getBoundingClientRect();
    return { x: (event.clientX - bounds.left) * canvas.width / bounds.width, y: (event.clientY - bounds.top) * canvas.height / bounds.height };
  }
  function startStroke(event: React.PointerEvent<HTMLCanvasElement>) {
    const canvas = event.currentTarget;
    const context = canvas.getContext("2d");
    if (!context) return;
    drawingRef.current = true; canvas.setPointerCapture(event.pointerId);
    const p = point(event); context.beginPath(); context.moveTo(p.x, p.y);
  }
  function continueStroke(event: React.PointerEvent<HTMLCanvasElement>) {
    if (!drawingRef.current) return;
    const context = event.currentTarget.getContext("2d");
    if (!context) return;
    const p = point(event); context.lineWidth = 4; context.lineCap = "round"; context.lineJoin = "round"; context.strokeStyle = "#132b42";
    context.lineTo(p.x, p.y); context.stroke();
    setHasInk(true);
  }
  function clearSignature() {
    const canvas = canvasRef.current; const context = canvas?.getContext("2d");
    if (canvas && context) context.clearRect(0, 0, canvas.width, canvas.height);
    setHasInk(false);
    setConfirmed(false);
  }
  function croppedSignature(canvas: HTMLCanvasElement): string | null {
    const context = canvas.getContext("2d");
    if (!context) return null;
    const { data, width, height } = context.getImageData(0, 0, canvas.width, canvas.height);
    let left = width, top = height, right = -1, bottom = -1;
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
      if (data[(y * width + x) * 4 + 3] === 0) continue;
      left = Math.min(left, x); top = Math.min(top, y); right = Math.max(right, x); bottom = Math.max(bottom, y);
    }
    if (right < left || bottom < top) return null;
    const padding = 18;
    left = Math.max(0, left - padding); top = Math.max(0, top - padding);
    right = Math.min(width - 1, right + padding); bottom = Math.min(height - 1, bottom + padding);
    const crop = document.createElement("canvas");
    crop.width = right - left + 1; crop.height = bottom - top + 1;
    crop.getContext("2d")?.putImageData(context.getImageData(left, top, crop.width, crop.height), 0, 0);
    return crop.toDataURL("image/png").split(",")[1] ?? null;
  }
  async function sign() {
    const signaturePngBase64 = canvasRef.current ? croppedSignature(canvasRef.current) : null;
    if (!selected || !signaturePngBase64 || !hasInk || !confirmed) {
      setError("Vui lòng vẽ chữ ký và xác nhận đã kiểm tra PDF trước khi ký.");
      return;
    }
    setBusy(true); setError(""); setNotice("");
    try {
      await internalApi(`/company-signing/${selected.signatureId}/sign`, "POST", { signaturePngBase64, confirmed: true });
      setNotice(`Đã ghi nhận chữ ký công ty cho ${selected.code}. Khách hàng đăng nhập Portal, mở hợp đồng trong mục “Hợp đồng của bạn”, xác minh OTP rồi ký tại trang chi tiết.`); setSelected(null); setDocumentUrl(""); clearSignature(); await load();
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  return <main className={`${s.page} ${s.signingPage}`} data-ui="company-signing-queue"><ContractTabs/>
    <div className={s.titleRow}><div><span className={s.eyebrow}>HỢP ĐỒNG ĐÃ ĐƯỢC PHÊ DUYỆT</span><h1>Ký phía công ty</h1><p className={s.muted}>Công ty ký trước. Sau đó khách đăng nhập Portal, xác minh OTP và ký trên hợp đồng của mình.</p></div>
      <button className={ui.secondaryButton} disabled={busy} onClick={() => void load()}><RefreshCw size={16}/> Làm mới</button></div>
    {error && <p role="alert" className={ui.error}>{error}</p>}{notice && <p role="status" className={ui.notice}>{notice}</p>}
    {!rows.length ? <section className={ui.card}><p>Chưa có hợp đồng chờ công ty ký. Hành chính duyệt bản chốt xong, phiên ký sẽ tự tạo và xuất hiện tại đây.</p></section> :
      <section className={ui.card} aria-label="Danh sách hợp đồng chờ công ty ký"><div className={s.toolbar}>
        <input aria-label="Tìm hợp đồng chờ ký" className={ui.field} placeholder="Tìm mã, tên hợp đồng hoặc khách hàng…" value={search} onChange={e => setSearch(e.target.value)}/>
        <select aria-label="Lọc người ký" className={ui.field} style={{ maxWidth: 240 }} value={signer} onChange={e => setSigner(e.target.value)}><option value="">Mọi người ký</option>{signers.map(n => <option key={n} value={n}>{n}</option>)}</select></div>
        {list.total === 0 ? <div className={s.empty}><FileSignature size={42} strokeWidth={1.3}/><strong>Không tìm thấy hợp đồng phù hợp</strong><p>Thử đổi từ khóa hoặc bộ lọc.</p></div>
        : <div className={s.tableWrap}><table className={s.table}><thead><tr><SortHeader label="Mã hợp đồng" column="code" sort={list.sort} onSort={list.sortBy}/><SortHeader label="Hợp đồng" column="name" sort={list.sort} onSort={list.sortBy}/><SortHeader label="Khách hàng" column="customer" sort={list.sort} onSort={list.sortBy}/><SortHeader label="Người ký" column="signer" sort={list.sort} onSort={list.sortBy}/><SortHeader label="Chờ từ" column="created" sort={list.sort} onSort={list.sortBy}/><th><span className="sr-only">Thao tác</span></th></tr></thead><tbody>
        {list.rows.map(row => <tr key={row.signatureId}><td>{row.code}</td><td>{row.contractName}</td><td>{row.customerName}</td><td>{row.signerName}</td><td className={s.muted}>{new Date(row.createdAt).toLocaleString("vi-VN")}</td><td><button className={ui.secondaryButton} onClick={() => { setSelected(row); setConfirmed(false); clearSignature(); }}>Xem PDF và ký</button></td></tr>)}
      </tbody></table></div>}<ListPager page={list.page} pageSize={list.pageSize} total={list.total} onPage={list.setPage} label="hợp đồng"/></section>}
    {selected && <section className={`${ui.card} ${s.companySigningDetail}`} aria-label="Ký hợp đồng">
      <header className={s.companySigningDetailHeader}><div><span className={s.signingCode}>{selected.code} · {selected.customerName}</span><h2>{selected.contractName}</h2><p>PDF chốt đã được khách hàng đồng ý. Chữ ký sẽ ghi trực tiếp lên bản này.</p></div><button className={ui.secondaryButton} onClick={() => setSelected(null)}>Đóng</button></header>
      <div className={s.companySigningWork}>
        <div className={s.companySigningPdf}><div className={s.companySigningPdfLabel}><strong>Hợp đồng theo mẫu Word</strong><span>{selected.code}</span></div>
          {documentUrl ? <WordPdfViewer url={documentUrl}/> : <p role="status">Đang tải PDF…</p>}
        </div>
        <aside className={s.companySigningActions} aria-label="Thông tin và chữ ký người ký">
          <div className={s.companySigningSignatureLabel}>Ký vào vùng dưới đây. PDF giữ nguyên họ tên đã in sẵn trong hợp đồng.</div>
          <canvas id="company-signature-canvas" ref={canvasRef} width={900} height={260} aria-label="Vẽ chữ ký công ty" onPointerDown={startStroke} onPointerMove={continueStroke} onPointerUp={() => { drawingRef.current = false; }} onPointerCancel={() => { drawingRef.current = false; }}
            className={s.companySigningCanvas}/>
          <button className={ui.secondaryButton} type="button" onClick={clearSignature}><Trash2 size={15}/> Xóa chữ ký</button>
          <label className={s.companySigningConfirm}><input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)}/>Tôi đã kiểm tra PDF và xác nhận ký phiên bản này.</label>
          <button className={ui.primaryButton} type="button" disabled={busy || !confirmed || !hasInk || !documentUrl} onClick={() => void sign()}><FileSignature size={16}/><Check size={15}/>{busy ? "Đang lưu…" : "Ký với tư cách công ty"}</button>
        </aside>
      </div>
    </section>}
  </main>;
}
