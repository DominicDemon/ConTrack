"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Check, FileSignature, RefreshCw, Trash2 } from "lucide-react";
import { internalApi, type CompanySigningQueueItem } from "@/services/internal-api";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";

export default function CompanySigningQueuePage() {
  const [rows, setRows] = useState<CompanySigningQueueItem[]>([]);
  const [selected, setSelected] = useState<CompanySigningQueueItem | null>(null);
  const [documentUrl, setDocumentUrl] = useState("");
  const [confirmed, setConfirmed] = useState(false);
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
    const p = point(event); context.lineWidth = 3; context.lineCap = "round"; context.lineJoin = "round"; context.strokeStyle = "#132b42";
    context.lineTo(p.x, p.y); context.stroke();
  }
  function clearSignature() {
    const canvas = canvasRef.current; const context = canvas?.getContext("2d");
    if (canvas && context) context.clearRect(0, 0, canvas.width, canvas.height);
    setConfirmed(false);
  }
  async function sign() {
    if (!selected || !canvasRef.current || !confirmed) return;
    setBusy(true); setError(""); setNotice("");
    try {
      const signaturePngBase64 = canvasRef.current.toDataURL("image/png");
      await internalApi(`/company-signing/${selected.signatureId}/sign`, "POST", { signaturePngBase64, confirmed: true });
      setNotice(`Đã ghi nhận chữ ký công ty cho ${selected.code}. Khách hàng đăng nhập Portal, mở hợp đồng trong mục “Hợp đồng của bạn”, xác minh OTP rồi ký tại trang chi tiết.`); setSelected(null); setDocumentUrl(""); clearSignature(); await load();
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  return <main className={s.page} data-ui="company-signing-queue">
    <div className={s.titleRow}><div><span className={s.eyebrow}>HỢP ĐỒNG ĐÃ ĐƯỢC PHÊ DUYỆT</span><h1>Ký phía công ty</h1><p className={s.muted}>Công ty ký trước. Sau đó khách đăng nhập Portal, xác minh OTP và ký trên hợp đồng của mình.</p></div>
      <button className={ui.secondaryButton} disabled={busy} onClick={() => void load()}><RefreshCw size={16}/> Làm mới</button></div>
    {error && <p role="alert" className={ui.error}>{error}</p>}{notice && <p role="status" className={ui.notice}>{notice}</p>}
    {!rows.length ? <section className={ui.card}><p>Chưa có hợp đồng chờ công ty ký. Hãy duyệt hợp đồng trong mục Hợp đồng nháp, sau đó tạo phiên ký.</p></section> :
      <section className={ui.card} aria-label="Danh sách hợp đồng chờ công ty ký"><div className={s.tableWrap}><table className={s.table}><thead><tr><th>Mã hợp đồng</th><th>Hợp đồng</th><th>Khách hàng</th><th>Người ký</th><th></th></tr></thead><tbody>
        {rows.map(row => <tr key={row.signatureId}><td>{row.code}</td><td>{row.contractName}</td><td>{row.customerName}</td><td>{row.signerName}</td><td><button className={ui.secondaryButton} onClick={() => { setSelected(row); setConfirmed(false); clearSignature(); }}>Mở để ký</button></td></tr>)}
      </tbody></table></div></section>}
    {selected && <section className={ui.card} style={{ marginTop: 18 }} aria-label="Ký hợp đồng">
      <div className={s.titleRow}><div><span className={s.eyebrow}>{selected.code} · {selected.customerName}</span><h2>{selected.contractName}</h2></div><button className={ui.secondaryButton} onClick={() => setSelected(null)}>Đóng</button></div>
      {documentUrl ? <iframe title={`PDF ${selected.contractName}`} src={documentUrl} style={{ width: "100%", height: "58vh", border: "1px solid var(--border)" }}/> : <p>Đang tải PDF…</p>}
      <div style={{ marginTop: 18, display: "grid", gap: 10 }}><strong>Chữ ký người đại diện công ty</strong>
        <canvas ref={canvasRef} width={700} height={180} aria-label="Vẽ chữ ký công ty" onPointerDown={startStroke} onPointerMove={continueStroke} onPointerUp={() => { drawingRef.current = false; }} onPointerCancel={() => { drawingRef.current = false; }}
          style={{ width: "100%", maxWidth: 700, height: 180, border: "1px solid var(--border)", touchAction: "none", background: "white" }}/>
        <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 12 }}><button className={ui.secondaryButton} type="button" onClick={clearSignature}><Trash2 size={15}/> Xóa chữ ký</button>
          <label style={{ display: "flex", alignItems: "center", gap: 8 }}><input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)}/>Tôi đã kiểm tra PDF và xác nhận ký phiên bản này.</label>
          <button className={ui.primaryButton} type="button" disabled={busy || !confirmed || !documentUrl} onClick={() => void sign()}><FileSignature size={16}/><Check size={15}/>{busy ? "Đang lưu…" : "Ký với tư cách công ty"}</button></div>
      </div>
    </section>}
  </main>;
}
