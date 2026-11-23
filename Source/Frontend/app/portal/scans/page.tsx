"use client";
import { useCallback, useEffect, useRef, useState, type ChangeEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Camera, Eye, EyeOff, FileScan, FileUp, KeyRound, RefreshCw, Search } from "lucide-react";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import DocumentTabs from "@/components/portal/document-tabs";
import { REMEMBER_MINUTES, touchFileKey, useRememberedFileKey } from "@/lib/remembered-file-key";
import PortalHeader from "@/components/portal/portal-header";
import { customerApi, customerApiForm, handleAuthError, type CustomerSession } from "@/services/customer-api";
import { imagesToPdf } from "@/lib/images-to-pdf";

type Scan = { fileId: string; kind: string; fileName: string; contractCode: string | null; contractName: string | null; uploadedAt: string; matchedBy: string; ready: boolean; status: string };
type KeyStatus = { hasKey: boolean };
type CustomerContract = { documentId: string; documentCode: string | null; documentName: string; customerName: string; contractStatus: string; totalAmount: number; versionNo: number | null; signedOnline: boolean; paperEvidenceStatus: string | null };

const MAX_BYTES = 20 * 1024 * 1024;
const when = (value: string) => new Date(value).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });
const contractStatusText: Record<string, string> = { Draft: "Bản nháp", Negotiating: "Đang trao đổi", PendingApproval: "Chờ duyệt", PendingSignature: "Chờ ký", Signed: "Đã ký", Completed: "Hoàn thành" };
const statusText: Record<string, string> = { PendingReview: "Đang xử lý", Accepted: "Đã xác nhận", Rejected: "Cần gửi lại", Superseded: "Đã được thay thế", Ready: "Sẵn sàng xem" };

/** Customer scans, upload and tracking for the customer's own contracts. */
export default function CustomerScansPage() {
  const router = useRouter();
  const cameraInput = useRef<HTMLInputElement>(null); const fileInput = useRef<HTMLInputElement>(null);
  const [session, setSession] = useState<CustomerSession | null>(null);
  const [scans, setScans] = useState<Scan[] | null>(null); const [contracts, setContracts] = useState<CustomerContract[] | null>(null);
  const [keyStatus, setKeyStatus] = useState<KeyStatus | null>(null); const [key, setKey, remembered, forget] = useRememberedFileKey(); const [showKey, setShowKey] = useState(false);
  const [query, setQuery] = useState(""); const [selected, setSelected] = useState<CustomerContract | null>(null);
  const [files, setFiles] = useState<File[]>([]);
  const [loadingContract, setLoadingContract] = useState<string | null>(null); const [uploading, setUploading] = useState(false);
  const [error, setError] = useState(""); const [notice, setNotice] = useState("");

  const refresh = useCallback(async () => {
    try {
      const [list, ownContracts, status] = await Promise.all([
        customerApi<Scan[]>("/scans"), customerApi<CustomerContract[]>("/scans/contracts"), customerApi<KeyStatus>("/file-key"),
      ]);
      setScans(list); setContracts(ownContracts); setKeyStatus(status);
    } catch (e) { if (handleAuthError(e, router.replace)) setError((e as Error).message); }
  }, [router]);

  useEffect(() => {
    void Promise.all([customerApi<CustomerSession>("/auth/me"), customerApi<Scan[]>("/scans"), customerApi<KeyStatus>("/file-key"), customerApi<CustomerContract[]>("/scans/contracts")])
      .then(([me, list, status, ownContracts]) => { setSession(me); setScans(list); setKeyStatus(status); setContracts(ownContracts); })
      .catch(e => { if (handleAuthError(e, router.replace)) setError(e.message); });
  }, [router]);

  async function chooseContract(contract: CustomerContract) {
    setError(""); setNotice(""); setFiles([]); setSelected(contract);
  }

  async function open(scan: Scan) {
    setError("");
    if (!key.trim()) { setError("Nhập key xem bản scan của bạn trước."); return; }
    setLoadingContract(scan.fileId);
    const tab = window.open("", "_blank");
    try {
      const response = await fetch(`/api/customer/scans/${scan.fileId}/file`, { credentials: "same-origin", cache: "no-store", headers: { "X-File-Key": key.trim() } });
      if (!response.ok) { const body = await response.json().catch(() => null) as { message?: string } | null; throw new Error(body?.message ?? "Không mở được bản scan."); }
      touchFileKey(key);
      const url = URL.createObjectURL(await response.blob());
      if (tab) tab.location.assign(url); else window.location.assign(url);
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (e) { tab?.close(); setError((e as Error).message); } finally { setLoadingContract(null); }
  }

  function setPicked(event: ChangeEvent<HTMLInputElement>) {
    const picked = Array.from(event.target.files ?? []); event.target.value = ""; setError("");
    if (!picked.length) return;
    const pdfs = picked.filter(file => file.type === "application/pdf" || file.name.toLowerCase().endsWith(".pdf"));
    const images = picked.filter(file => ["image/jpeg", "image/png"].includes(file.type));
    if (pdfs.length ? (pdfs.length !== 1 || images.length || picked.length !== 1) : images.length !== picked.length) { setError("Chọn một tệp PDF hoặc một/nhiều ảnh JPG, PNG."); return; }
    if (picked.some(file => file.size <= 0 || file.size > MAX_BYTES)) { setError("Mỗi tệp phải có nội dung và không quá 20 MB."); return; }
    setFiles(picked);
  }

  async function upload() {
    if (!selected) return;
    if (!files.length) { setError("Chọn bản scan cần tải lên."); return; }
    if (!window.confirm(`Lưu bản scan vào hợp đồng ${selected.documentCode ?? selected.documentName}? Bản scan sẽ hiển thị ngay trong danh sách của bạn.`)) return;
    setUploading(true); setError(""); setNotice("");
    try {
      const isPdf = files[0].type === "application/pdf" || files[0].name.toLowerCase().endsWith(".pdf");
      const file = isPdf ? files[0] : await imagesToPdf(files, `ban-ky-${selected.documentCode ?? selected.documentId}.pdf`);
      if (file.size > MAX_BYTES) throw new Error("Tệp sau khi ghép vượt giới hạn 20 MB. Hãy chọn ít ảnh hơn.");
      const form = new FormData(); form.append("file", file, file.name);
      await customerApiForm(`/scans/contracts/${selected.documentId}/scans`, form);
      setFiles([]); setNotice("Đã lưu bản scan vào hợp đồng của bạn.");
      await refresh();
    } catch (e) { if (handleAuthError(e, router.replace)) setError((e as Error).message); }
    finally { setUploading(false); }
  }

  const visibleScans = (scans ?? []).filter(scan => `${scan.fileName} ${scan.contractCode ?? ""} ${scan.contractName ?? ""}`.toLocaleLowerCase("vi").includes(query.trim().toLocaleLowerCase("vi")));
  const visibleContracts = (contracts ?? []).filter(contract => `${contract.documentCode ?? ""} ${contract.documentName}`.toLocaleLowerCase("vi").includes(query.trim().toLocaleLowerCase("vi")));

  return <><PortalHeader name={session?.name}/><div className={s.page} style={{ maxWidth: 1100, margin: "0 auto" }}>
    <DocumentTabs active="scans"/><div className={s.titleRow}><div><h1 style={{ display: "flex", gap: 10, alignItems: "center", marginTop: 6 }}><FileScan/>Bản scan hợp đồng</h1><p className={s.muted}>Bản giấy của hợp đồng đã ký với công ty: chọn hợp đồng bên dưới để tải lên hoặc chụp, rồi tìm và xem lại tại đây.</p></div>
      <button className={ui.secondaryButton} onClick={() => void refresh()}><RefreshCw size={16}/>Tải lại</button></div>
    {error && <p role="alert" className={ui.error} style={{ marginTop: 12 }}>{error}</p>}{notice && <p role="status" className={ui.card} style={{ marginTop: 12 }}>{notice}</p>}
    {keyStatus && !keyStatus.hasKey && <p className={ui.card} style={{ marginTop: 18 }}>Tạo key để mở các bản scan đã được chia sẻ. <Link href="/portal/file-key">Tạo key</Link></p>}
    {keyStatus?.hasKey && <section className={ui.card} style={{ marginTop: 18, display: "flex", gap: 10, alignItems: "end", flexWrap: "wrap" }}>
      <label className={s.label} style={{ flex: "1 1 260px" }}>Key xem bản scan<div style={{ display: "flex", gap: 8 }}><input type={showKey ? "text" : "password"} autoComplete="off" spellCheck={false} maxLength={64} className={ui.field} value={key} onChange={e => setKey(e.target.value)} style={{ fontFamily: "monospace" }}/><button type="button" className={s.iconButton} aria-label={showKey ? "Ẩn key" : "Hiện key"} onClick={() => setShowKey(!showKey)}>{showKey ? <EyeOff size={18}/> : <Eye size={18}/>}</button></div>{remembered && <div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>Key được nhớ trong tab này {REMEMBER_MINUTES} phút kể từ lần dùng cuối. <button type="button" onClick={forget} style={{ border: 0, background: "none", padding: 0, color: "var(--primary)", cursor: "pointer", fontSize: 11 }}>Quên key ngay</button></div>}</label>
      <Link href="/portal/file-key" className={ui.secondaryButton}><KeyRound size={16}/>Đổi / quên key</Link></section>}

    <section style={{ marginTop: 28 }}><div className={s.titleRow} style={{ marginBottom: 12 }}><div><h2 style={{ fontSize: 19, fontWeight: 650 }}>Hợp đồng của bạn</h2><p className={s.muted}>Bấm “Thêm bản scan” ở hợp đồng cần lưu bản giấy.</p></div><span className={s.badge}>{contracts?.length ?? 0} hợp đồng</span></div>
      <div className={s.toolbar}><Search size={17}/><input className={ui.field} aria-label="Tìm hợp đồng" placeholder="Tìm theo mã hoặc tên hợp đồng" value={query} onChange={e => setQuery(e.target.value)}/></div>
      {!contracts ? <p className={s.muted}>Đang tải hợp đồng…</p> : visibleContracts.length === 0 ? <div className={s.empty}><FileScan size={36}/><strong>{query.trim() ? "Không có hợp đồng phù hợp" : "Bạn chưa có hợp đồng nào với công ty nên chưa tải bản scan được"}</strong>{!query.trim() && <p>Muốn lưu tài liệu riêng, kể cả hợp đồng với đối tác khác? <Link href="/portal/vault">Mở Kho lưu trữ riêng</Link></p>}</div> : <section className={ui.card}><div className={s.tableWrap}><table className={s.table}><thead><tr><th>Hợp đồng</th><th>Trạng thái</th><th>Phiên bản</th><th>Bản scan</th><th/></tr></thead><tbody>{visibleContracts.map(contract => <tr key={contract.documentId}>
        <td><strong>{contract.documentCode ?? "Chưa cấp mã"}</strong><div className={s.muted}>{contract.documentName}</div></td><td>{contractStatusText[contract.contractStatus] ?? contract.contractStatus}</td><td>{contract.versionNo ?? "—"}</td><td>{contract.paperEvidenceStatus ? statusText[contract.paperEvidenceStatus] ?? contract.paperEvidenceStatus : "Chưa có bản scan"}</td><td><button className={ui.secondaryButton} disabled={loadingContract === contract.documentId} onClick={() => void chooseContract(contract)}>{loadingContract === contract.documentId ? "Đang tải…" : "Thêm bản scan"}</button></td>
      </tr>)}</tbody></table></div></section>}
    </section>

    {selected && <section className={ui.card} style={{ marginTop: 18, padding: 20 }}><div className={s.titleRow}><div><span className={s.eyebrow}>Bản scan của bạn</span><h2 style={{ fontSize: 19, fontWeight: 650, marginTop: 5 }}>{selected.documentCode ?? "Hợp đồng"} · {selected.documentName}</h2></div><button className={s.iconButton} onClick={() => setSelected(null)} aria-label="Đóng">×</button></div>
      <div style={{ display: "grid", gap: 14, maxWidth: 680 }}><p className={s.muted}>Bản scan được lưu ngay trong hợp đồng để bạn tìm kiếm và xem lại.</p>
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}><button type="button" className={ui.secondaryButton} disabled={uploading} onClick={() => cameraInput.current?.click()}><Camera size={16}/>Chụp bản scan</button><button type="button" className={ui.secondaryButton} disabled={uploading} onClick={() => fileInput.current?.click()}><FileUp size={16}/>Chọn PDF / ảnh</button>
          <input ref={cameraInput} hidden type="file" accept="image/jpeg,image/png" capture="environment" onChange={setPicked}/><input ref={fileInput} hidden type="file" accept="application/pdf,image/jpeg,image/png" multiple onChange={setPicked}/></div>
        {files.length > 0 && <p className={s.muted}>{files.length === 1 ? files[0].name : `${files.length} ảnh sẽ ghép thành một PDF`}</p>}
        <button className={ui.primaryButton} disabled={uploading || !files.length} onClick={() => void upload()}>{uploading ? "Đang lưu…" : "Lưu bản scan"}</button></div>
    </section>}

    <section style={{ marginTop: 30 }}><div className={s.titleRow} style={{ marginBottom: 12 }}><div><h2 style={{ fontSize: 19, fontWeight: 650 }}>Bản scan hợp đồng</h2><p className={s.muted}>Tìm kiếm bản scan bạn đã tải lên và bản scan được chia sẻ cho bạn.</p></div><span className={s.badge}>{visibleScans.length} bản scan</span></div>
      {scans && scans.length > 0 && <section className={ui.card}><div className={s.toolbar}><Search size={17}/><input className={ui.field} aria-label="Tìm bản scan" placeholder="Tìm theo tên tệp hoặc hợp đồng" value={query} onChange={e => setQuery(e.target.value)}/></div>
        {visibleScans.length === 0 ? <div className={s.empty}><FileScan size={36}/><strong>Không tìm thấy bản scan phù hợp</strong></div> : <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Bản scan</th><th>Hợp đồng</th><th>Ngày lưu</th><th>Trạng thái</th><th/></tr></thead><tbody>{visibleScans.map(scan => <tr key={scan.fileId}>
          <td><strong style={{ overflowWrap: "anywhere" }}>{scan.fileName}</strong><div className={s.muted} style={{ fontSize: 11 }}>{scan.kind === "customer-upload" ? "Bạn đã tải lên" : scan.kind === "evidence" ? "Bản scan hợp đồng" : "Được chia sẻ"}</div></td><td>{scan.contractCode ?? "—"}{scan.contractName && <div className={s.muted} style={{ fontSize: 11 }}>{scan.contractName}</div>}</td><td className={s.muted}>{when(scan.uploadedAt)}</td><td><span className={`${s.badge} ${scan.status === "Rejected" ? s.warning : ""}`}>{statusText[scan.status] ?? (scan.ready ? "Sẵn sàng xem" : "Đang chờ cấp quyền")}</span></td><td>{scan.ready && <button className={ui.secondaryButton} disabled={loadingContract !== null || !keyStatus?.hasKey} onClick={() => void open(scan)}>{loadingContract === scan.fileId ? "Đang mở…" : "Xem"}</button>}</td>
        </tr>)}</tbody></table></div>}</section>}
      {scans?.length === 0 && <div className={s.empty}><FileScan size={42}/><strong>Chưa có bản scan nào</strong><p>Bản scan khách hàng gửi sẽ hiển thị tại đây.</p></div>}
    </section>
  </div></>;
}
