"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Eye, EyeOff, FileScan, KeyRound } from "lucide-react";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import PortalHeader from "@/components/portal/portal-header";
import { customerApi, handleAuthError, type CustomerSession } from "@/services/customer-api";

/** GET /api/customer/scans. */
type Scan = { fileId: string; kind: "evidence" | "scan"; fileName: string; contractCode: string | null; contractName: string | null; uploadedAt: string; matchedBy: string; ready: boolean };
type KeyStatus = { hasKey: boolean };

const when = (value: string) => new Date(value).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });

/** The scans of the customer's contracts, opened with the customer's own key (kept in this tab only). */
export default function CustomerScansPage() {
  const router = useRouter();
  const [session, setSession] = useState<CustomerSession | null>(null);
  const [scans, setScans] = useState<Scan[] | null>(null); const [status, setStatus] = useState<KeyStatus | null>(null);
  const [key, setKey] = useState(""); const [show, setShow] = useState(false);
  const [busy, setBusy] = useState<string | null>(null); const [error, setError] = useState("");

  useEffect(() => {
    void Promise.all([customerApi<CustomerSession>("/auth/me"), customerApi<Scan[]>("/scans"), customerApi<KeyStatus>("/file-key")])
      .then(([me, list, keyStatus]) => { setSession(me); setScans(list); setStatus(keyStatus); })
      .catch(e => { if (handleAuthError(e, router.replace)) setError(e.message); });
  }, [router]);

  async function open(scan: Scan) {
    setError("");
    if (!key.trim()) { setError("Nhập key xem bản scan của bạn trước."); return; }
    setBusy(scan.fileId);
    // Opened before the download, so the browser does not block it as a pop-up.
    const tab = window.open("", "_blank");
    try {
      const response = await fetch(`/api/customer/scans/${scan.fileId}/file`, { credentials: "same-origin", cache: "no-store", headers: { "X-File-Key": key.trim() } });
      if (!response.ok) {
        const body = await response.json().catch(() => null) as { message?: string } | null;
        throw new Error(body?.message ?? "Không mở được bản scan.");
      }
      const url = URL.createObjectURL(await response.blob());
      if (tab) tab.location.assign(url); else window.location.assign(url);
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (e) { tab?.close(); setError((e as Error).message); } finally { setBusy(null); }
  }

  return <><PortalHeader name={session?.name}/><div className={s.page} style={{ maxWidth: 960, margin: "0 auto" }}>
    <span className={s.eyebrow}>Hợp đồng</span><h1 style={{ display: "flex", gap: 10, alignItems: "center" }}><FileScan/>Bản scan hợp đồng</h1>
    <p className={s.muted}>Bản ký giấy đã được xác nhận của hợp đồng của bạn, và hợp đồng giấy có mã số thuế, số điện thoại hoặc email trùng với tài khoản. Bản scan được mã hóa; mở bằng key của bạn.</p>
    {error && <p role="alert" className={ui.error} style={{ marginTop: 12 }}>{error}</p>}
    {status && !status.hasKey && <p className={ui.card} style={{ marginTop: 18 }}>Bạn chưa có key xem bản scan. <Link href="/portal/file-key">Tạo key</Link> để mở các bản scan bên dưới.</p>}
    {status?.hasKey && <section className={ui.card} style={{ marginTop: 18, display: "flex", gap: 10, alignItems: "end", flexWrap: "wrap" }}>
      <label className={s.label} style={{ flex: "1 1 260px" }}>Key xem bản scan (chỉ giữ trong tab này)
        <div style={{ display: "flex", gap: 8 }}><input type={show ? "text" : "password"} autoComplete="off" spellCheck={false} maxLength={64} className={ui.field} value={key} onChange={e => setKey(e.target.value)} style={{ fontFamily: "monospace" }}/>
          <button type="button" className={s.iconButton} aria-label={show ? "Ẩn key" : "Hiện key"} onClick={() => setShow(!show)}>{show ? <EyeOff size={18}/> : <Eye size={18}/>}</button></div></label>
      <Link href="/portal/file-key" className={ui.secondaryButton}><KeyRound size={16}/>Đổi / quên key</Link>
    </section>}
    {!scans ? !error && <p className={s.muted} style={{ marginTop: 18 }}>Đang tải…</p>
      : scans.length === 0 ? <div className={s.empty} style={{ marginTop: 18 }}><FileScan size={42} strokeWidth={1.3}/><strong>Chưa có bản scan nào</strong><p>Bản ký giấy sẽ hiện ở đây sau khi công ty xác nhận.</p></div>
      : <section className={ui.card} style={{ marginTop: 18 }}><div className={s.tableWrap}><table className={s.table}>
        <thead><tr><th>Bản scan</th><th>Hợp đồng</th><th>Ngày lưu</th><th>Vì sao là của bạn</th><th><span className="sr-only">Thao tác</span></th></tr></thead>
        <tbody>{scans.map(scan => <tr key={scan.fileId}>
          <td><strong style={{ overflowWrap: "anywhere" }}>{scan.fileName}</strong><div className={s.muted} style={{ fontSize: 11 }}>{scan.kind === "evidence" ? "Bản ký giấy" : "Hợp đồng giấy scan"}</div></td>
          <td>{scan.contractCode ?? "—"}{scan.contractName && <div className={s.muted} style={{ fontSize: 11 }}>{scan.contractName}</div>}</td>
          <td className={s.muted}>{when(scan.uploadedAt)}</td>
          <td className={s.muted}>{scan.matchedBy}</td>
          <td>{scan.ready ? <button className={ui.secondaryButton} disabled={busy !== null || !status?.hasKey} onClick={() => void open(scan)}>{busy === scan.fileId ? "Đang mở…" : "Xem"}</button>
            : <span className={s.muted} style={{ fontSize: 12 }}>{status?.hasKey ? "Đang chờ cấp quyền" : "Cần tạo key"}</span>}</td>
        </tr>)}</tbody></table></div></section>}
  </div></>;
}
