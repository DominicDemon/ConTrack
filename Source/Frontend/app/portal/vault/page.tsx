"use client";
import { useCallback, useEffect, useRef, useState, type ChangeEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Archive, BellRing, Camera, Eye, EyeOff, FileUp, Mail, Pencil, Plus, RefreshCw, RotateCcw, Search, Trash2, X } from "lucide-react";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import PortalHeader from "@/components/portal/portal-header";
import DocumentTabs from "@/components/portal/document-tabs";
import { customerApi, customerApiForm, handleAuthError, type CustomerSession } from "@/services/customer-api";
import { imagesToPdf } from "@/lib/images-to-pdf";
import { REMEMBER_MINUTES, touchFileKey, useRememberedFileKey } from "@/lib/remembered-file-key";

type Keyword = { id: string; type: string; value: string; manual: boolean };
/** GET /api/customer/vault. */
type Item = {
  id: string; title: string; note: string | null; category: string | null; fileName: string; contentType: string; sizeBytes: number; uploadedAt: string;
  ocrStatus: string; pageCount: number | null; keywords: Keyword[]; sameContractCode: string[];
  expiresOn: string | null; remindDays: number | null; daysLeft: number | null; trashedAt: string | null;
};
type Usage = { usedBytes: number; limitBytes: number; files: number; inTrash: number };
type KeyStatus = { hasKey: boolean };
type Edit = { id: string; title: string; note: string; category: string; expiresOn: string; remindDays: string };

const MAX_BYTES = 20 * 1024 * 1024;
const TRASH_DAYS = 30;
const KEYWORD: Record<string, string> = { ContractCode: "Số HĐ", TaxCode: "MST", PhoneNumber: "SĐT", CustomerName: "Tên", Email: "Email" };
const OCR: Record<string, string> = { QUEUED: "Đang chờ đọc…", PROCESSING: "Đang đọc nội dung…", FAILED: "Không đọc được nội dung tệp.",
  INTERRUPTED: "Máy chủ khởi động lại khi đang đọc. Nhập key rồi bấm “Đọc lại”." };
const mb = (bytes: number) => `${(bytes / 1048576).toLocaleString("vi-VN", { maximumFractionDigits: 1 })} MB`;
const day = (value: string) => new Date(value).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });

/** dd/mm/yyyy typed with the slashes added; "" or yyyy-mm-dd for the API, and back. */
const maskDay = (typed: string) => { const d = typed.replace(/\D/g, "").slice(0, 8); return [d.slice(0, 2), d.slice(2, 4), d.slice(4, 8)].filter(Boolean).join("/"); };
const isoDay = (text: string) => { const m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(text); return m ? `${m[3]}-${m[2]}-${m[1]}` : ""; };
const viDay = (iso: string | null) => iso ? iso.split("-").reverse().join("/") : "";
const isPdf = (f: File) => f.type === "application/pdf" || f.name.toLowerCase().endsWith(".pdf");

/** A call that needs the customer's key in the X-File-Key header (open, read again, email). */
async function withKey(path: string, key: string, method = "GET", body?: unknown): Promise<Response> {
  const headers: Record<string, string> = { "X-File-Key": key.trim() };
  if (method !== "GET") {
    const token = await fetch("/api/customer/auth/csrf", { credentials: "same-origin", cache: "no-store" }).then(r => r.json()) as { token: string };
    headers["X-CSRF-TOKEN"] = token.token;
    if (body !== undefined) headers["Content-Type"] = "application/json";
  }
  const response = await fetch(`/api/customer${path}`, { method, credentials: "same-origin", cache: "no-store", headers, body: body === undefined ? undefined : JSON.stringify(body) });
  if (!response.ok) {
    const error = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(error?.message ?? "Không thực hiện được.");
  }
  touchFileKey(key);
  return response;
}

function Expiry({ item }: { item: Item }) {
  if (!item.expiresOn || item.daysLeft === null) return null;
  const color = item.daysLeft < 0 ? "#b91c1c" : item.daysLeft <= (item.remindDays ?? 30) ? "#805400" : undefined;
  const text = item.daysLeft < 0 ? `Đã hết hạn ${viDay(item.expiresOn)}` : item.daysLeft === 0 ? "Hết hạn hôm nay" : `Hết hạn ${viDay(item.expiresOn)} · còn ${item.daysLeft} ngày`;
  return <div style={{ fontSize: 11, color, display: "flex", gap: 4, alignItems: "center" }}>{item.remindDays && <BellRing size={11}/>}{text}</div>;
}

/** The customer's own document store: scan and keep their contracts and papers; nobody at the company can open them. */
export default function CustomerVaultPage() {
  const router = useRouter();
  const [session, setSession] = useState<CustomerSession | null>(null);
  const [items, setItems] = useState<Item[] | null>(null); const [usage, setUsage] = useState<Usage | null>(null); const [keyStatus, setKeyStatus] = useState<KeyStatus | null>(null);
  const [categories, setCategories] = useState<string[]>([]);
  const [trash, setTrash] = useState(false); const [loadedAt, setLoadedAt] = useState(0);
  const [query, setQuery] = useState(""); const [from, setFrom] = useState(""); const [to, setTo] = useState(""); const [folder, setFolder] = useState("");
  const [key, setKey, remembered, forget] = useRememberedFileKey(); const [showKey, setShowKey] = useState(false);
  const [files, setFiles] = useState<File[]>([]); const [title, setTitle] = useState(""); const [note, setNote] = useState("");
  const [category, setCategory] = useState(""); const [expires, setExpires] = useState("");
  const [busy, setBusy] = useState<string | null>(null); const [error, setError] = useState(""); const [notice, setNotice] = useState("");
  const [editing, setEditing] = useState<Edit | null>(null); const [mailing, setMailing] = useState<Item | null>(null);
  const [mailTo, setMailTo] = useState(""); const [mailNote, setMailNote] = useState("");
  const [adding, setAdding] = useState<{ id: string; type: string; value: string } | null>(null);
  const [preview, setPreview] = useState<{ title: string; url: string; type: string } | null>(null);
  const camera = useRef<HTMLInputElement>(null); const picker = useRef<HTMLInputElement>(null);

  const load = useCallback(async () => {
    const params = new URLSearchParams();
    if (query.trim()) params.set("q", query.trim());
    if (isoDay(from)) params.set("from", isoDay(from));
    if (isoDay(to)) params.set("to", isoDay(to));
    if (folder) params.set("category", folder);
    if (trash) params.set("trash", "true");
    const [list, used, folders] = await Promise.all([customerApi<Item[]>(`/vault?${params}`), customerApi<Usage>("/vault/usage"), customerApi<string[]>("/vault/categories")]);
    setItems(list); setUsage(used); setCategories(folders); setLoadedAt(Date.now());
  }, [query, from, to, folder, trash]);

  useEffect(() => {
    void Promise.all([customerApi<CustomerSession>("/auth/me"), customerApi<KeyStatus>("/file-key")])
      .then(([me, k]) => { setSession(me); setKeyStatus(k); })
      .catch(e => { if (handleAuthError(e, router.replace)) setError(e.message); });
  }, [router]);
  useEffect(() => {
    const timer = setTimeout(() => { load().catch(e => { if (handleAuthError(e, router.replace)) setError(e.message); }); }, 300);
    return () => clearTimeout(timer);
  }, [load, router]);
  // Documents still being read refresh by themselves.
  const reading = (items ?? []).some(i => i.ocrStatus === "QUEUED" || i.ocrStatus === "PROCESSING");
  useEffect(() => {
    if (!reading) return;
    const timer = setInterval(() => { void load().catch(() => {}); }, 4000);
    return () => clearInterval(timer);
  }, [reading, load]);
  useEffect(() => () => { if (preview) URL.revokeObjectURL(preview.url); }, [preview]);

  const run = async (id: string, action: () => Promise<void>) => {
    setBusy(id); setError(""); setNotice("");
    try { await action(); } catch (e) { if (handleAuthError(e, router.replace)) setError((e as Error).message); } finally { setBusy(null); }
  };
  const needKey = () => { if (!key.trim()) { setError("Nhập key xem bản scan của bạn trước."); return true; } return false; };

  function picked(event: ChangeEvent<HTMLInputElement>) {
    const chosen = Array.from(event.target.files ?? []);
    event.target.value = "";
    if (!chosen.length) return;
    // PDFs are kept one document each; photos join the pages of one PDF.
    setFiles(old => [...old, ...chosen]);
    if (!title && chosen.length === 1 && isPdf(chosen[0])) setTitle(chosen[0].name.replace(/\.pdf$/i, ""));
  }
  const pdfs = files.filter(isPdf); const images = files.filter(f => !isPdf(f));
  const documents = pdfs.length + (images.length ? 1 : 0);

  const upload = () => run("upload", async () => {
    if (!files.length) throw new Error("Chụp hoặc chọn tài liệu cần lưu.");
    const name = (title.trim() || "tai-lieu").replace(/[^\p{L}\p{N}_-]+/gu, "-");
    const queue: { file: File; title: string }[] = pdfs.map(f => ({ file: f, title: documents === 1 && title.trim() ? title : f.name.replace(/\.pdf$/i, "") }));
    if (images.length) queue.push({ file: await imagesToPdf(images, `${name}.pdf`), title: title.trim() || name });
    const done: string[] = []; const failed: string[] = [];
    for (const [index, entry] of queue.entries()) {
      setBusy(`upload ${index + 1}/${queue.length}`);
      try {
        if (entry.file.size > MAX_BYTES) throw new Error(`“${entry.file.name}” vượt 20 MB.`);
        const form = new FormData(); form.append("file", entry.file, entry.file.name); form.append("title", entry.title); form.append("note", note);
        form.append("category", category); form.append("expiresOn", isoDay(expires));
        await customerApiForm(`/vault`, form); done.push(entry.title);
      } catch (e) { failed.push((e as Error).message); }
    }
    setFiles([]); setTitle(""); setNote(""); setExpires("");
    if (done.length) setNotice(`Đã lưu ${done.length} tài liệu. Hệ thống đang đọc nội dung để tìm kiếm.`);
    if (failed.length) setError(failed.join(" "));
    await load();
  });

  const view = (item: Item) => { if (needKey()) return; void run(item.id, async () => {
    const blob = await (await withKey(`/vault/${item.id}/file`, key)).blob();
    setPreview({ title: item.title, url: URL.createObjectURL(blob), type: item.contentType });
  }); };
  const reread = (item: Item) => { if (needKey()) return; void run(item.id, async () => { await withKey(`/vault/${item.id}/reread`, key, "POST"); setNotice("Đang đọc lại nội dung. Thông tin bạn tự thêm được giữ nguyên."); await load(); }); };
  const remove = (item: Item) => { if (!window.confirm(`Chuyển “${item.title}” vào thùng rác? Bạn khôi phục được trong ${TRASH_DAYS} ngày.`)) return;
    void run(item.id, async () => { await customerApi(`/vault/${item.id}`, "DELETE"); setNotice("Đã chuyển vào thùng rác."); await load(); }); };
  const restore = (item: Item) => void run(item.id, async () => { await customerApi(`/vault/${item.id}/restore`, "POST"); setNotice(`Đã khôi phục “${item.title}”.`); await load(); });
  const purge = (item: Item) => { if (!window.confirm(`Xóa hẳn “${item.title}”? Không khôi phục được nữa.`)) return;
    void run(item.id, async () => { await customerApi(`/vault/trash/${item.id}`, "DELETE"); setNotice("Đã xóa hẳn tài liệu."); await load(); }); };
  const saveEdit = () => editing && void run(editing.id, async () => {
    const expiresOn = isoDay(editing.expiresOn);
    if (editing.expiresOn && !expiresOn) throw new Error("Ngày hết hạn theo dạng dd/mm/yyyy.");
    await customerApi(`/vault/${editing.id}`, "PUT", { title: editing.title, note: editing.note, category: editing.category, expiresOn: expiresOn || null,
      remindDays: expiresOn && editing.remindDays ? Number(editing.remindDays) : null });
    setEditing(null); setNotice("Đã lưu thông tin tài liệu."); await load(); });
  const addKeyword = () => adding && void run(adding.id, async () => {
    await customerApi(`/vault/${adding.id}/keywords`, "POST", { type: adding.type, value: adding.value }); setAdding(null); await load(); });
  const dropKeyword = (item: Item, k: Keyword) => void run(item.id, async () => { await customerApi(`/vault/${item.id}/keywords/${k.id}`, "DELETE"); await load(); });
  const send = () => { if (!mailing || needKey()) return; void run(mailing.id, async () => {
    const result = await (await withKey(`/vault/${mailing.id}/email`, key, "POST", { recipients: mailTo.split(/[,;\s]+/).filter(Boolean), note: mailNote || null })).json() as { sent: string[] };
    setMailing(null); setMailTo(""); setMailNote(""); setNotice(`Đã gửi tới ${result.sent.join(", ")}.`); }); };

  const percent = usage ? Math.min(100, Math.round(usage.usedBytes / usage.limitBytes * 100)) : 0;
  const folderOptions = <>{categories.map(c => <option key={c} value={c}>{c}</option>)}</>;
  return <><PortalHeader name={session?.name}/><div className={s.page} style={{ maxWidth: 1100, margin: "0 auto" }}>
    <DocumentTabs active="vault"/>
    <h1 style={{ display: "flex", gap: 10, alignItems: "center" }}><Archive/>Kho lưu trữ của tôi</h1>
    <p className={s.muted}>Scan và lưu hợp đồng, tài liệu của bạn, kể cả hợp đồng với đối tác khác. Tài liệu được mã hóa bằng key của bạn; nhân viên công ty không xem được.</p>
    <details style={{ marginTop: 6, fontSize: 12 }} className={s.muted}><summary style={{ cursor: "pointer" }}>Ai có thể mở tài liệu của tôi?</summary>
      <ul style={{ margin: "6px 0 0 18px", display: "grid", gap: 3 }}>
        <li>Chỉ bạn, bằng key bạn tự đặt. Không nhân viên nào được cấp quyền mở kho lưu trữ riêng.</li>
        <li>Để bạn không mất tài liệu khi quên key, hệ thống giữ thêm một <strong>khóa khôi phục</strong> do quản trị hệ thống quản lý. Khóa này chỉ được dùng khi chính bạn đặt lại key (cần mật khẩu và mã gửi qua email).</li>
        <li>Mỗi lần khóa khôi phục được dùng, hệ thống ghi nhật ký và gửi email báo cho bạn.</li>
        <li>Tài liệu bạn gửi qua email cho người khác không còn được mã hóa.</li>
      </ul></details>
    {usage && <div style={{ marginTop: 10, maxWidth: 420 }}><div className={s.muted} style={{ fontSize: 12 }}>Đã dùng {mb(usage.usedBytes)} / 1 GB · {usage.files} tài liệu{usage.inTrash ? ` · ${usage.inTrash} trong thùng rác` : ""}</div>
      <div style={{ height: 6, background: "var(--muted)", borderRadius: 3, marginTop: 4 }}><div style={{ width: `${percent}%`, height: 6, borderRadius: 3, background: percent > 90 ? "#dc2626" : "var(--primary)" }}/></div></div>}
    {error && <p role="alert" className={ui.error} style={{ marginTop: 12 }}>{error}</p>}
    {notice && <p role="status" className={ui.notice} style={{ marginTop: 12 }}>{notice}</p>}
    {keyStatus && !keyStatus.hasKey && <p className={ui.card} style={{ marginTop: 16 }}>Bạn cần <Link href="/portal/file-key">tạo key xem bản scan</Link> trước khi lưu tài liệu: tài liệu được mã hóa bằng key đó.</p>}

    {keyStatus?.hasKey && !trash && <section className={ui.card} style={{ marginTop: 16, display: "grid", gap: 12 }}>
      <h2 style={{ fontSize: 16 }}>Thêm tài liệu</h2>
      <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
        <button type="button" className={ui.secondaryButton} disabled={busy !== null} onClick={() => camera.current?.click()}><Camera size={16}/>Chụp bằng camera</button>
        <button type="button" className={ui.secondaryButton} disabled={busy !== null} onClick={() => picker.current?.click()}><FileUp size={16}/>Chọn PDF / ảnh (nhiều tệp)</button>
        <input ref={camera} hidden type="file" accept="image/jpeg,image/png" capture="environment" onChange={picked}/>
        <input ref={picker} hidden type="file" accept="application/pdf,image/jpeg,image/png" multiple onChange={picked}/>
      </div>
      {files.length > 0 && <div className={s.muted} style={{ fontSize: 13 }}>
        Sẽ lưu {documents} tài liệu: {pdfs.map(f => f.name).join(", ")}{pdfs.length && images.length ? "; " : ""}{images.length ? `${images.length} ảnh ghép thành 1 PDF` : ""}
        {" "}<button type="button" className={s.iconButton} onClick={() => setFiles([])} aria-label="Bỏ chọn"><X size={14}/></button></div>}
      <label className={s.label}>Tên tài liệu{documents > 1 ? " (để trống: lấy theo tên tệp)" : ""}<input className={ui.field} maxLength={300} value={title} onChange={e => setTitle(e.target.value)} placeholder="Ví dụ: Hợp đồng thuê văn phòng 2026"/></label>
      <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
        <label className={s.label} style={{ flex: "1 1 220px" }}>Thư mục<input className={ui.field} list="vault-folders" maxLength={100} value={category} onChange={e => setCategory(e.target.value)} placeholder="Chọn hoặc gõ tên mới"/></label>
        <label className={s.label} style={{ flex: "0 1 180px" }}>Ngày hết hạn (nếu có)<input className={ui.field} inputMode="numeric" placeholder="dd/mm/yyyy" value={expires} onChange={e => setExpires(maskDay(e.target.value))}/></label>
      </div>
      <label className={s.label}>Ghi chú / từ khóa (không bắt buộc)<input className={ui.field} maxLength={1000} value={note} onChange={e => setNote(e.target.value)} placeholder="Ví dụ: đối tác ABC, gia hạn tháng 12"/></label>
      <div><button type="button" className={ui.primaryButton} disabled={busy !== null || !files.length} onClick={upload}>{busy?.startsWith("upload") ? `Đang lưu ${busy.slice(7)}…` : "Lưu vào kho"}</button></div>
    </section>}
    <datalist id="vault-folders">{folderOptions}</datalist>

    <section className={ui.card} style={{ marginTop: 16 }}>
      <div role="tablist" style={{ display: "flex", gap: 6, marginBottom: 12 }}>
        <button type="button" role="tab" aria-selected={!trash} className={trash ? ui.secondaryButton : ui.primaryButton} onClick={() => setTrash(false)}>Tài liệu</button>
        <button type="button" role="tab" aria-selected={trash} className={trash ? ui.primaryButton : ui.secondaryButton} onClick={() => setTrash(true)}><Trash2 size={14}/>Thùng rác{usage?.inTrash ? ` (${usage.inTrash})` : ""}</button>
      </div>
      {trash && <p className={s.muted} style={{ fontSize: 12, marginBottom: 10 }}>Tài liệu trong thùng rác vẫn tính vào dung lượng và tự xóa hẳn sau {TRASH_DAYS} ngày.</p>}
      <div style={{ display: "flex", gap: 10, flexWrap: "wrap", alignItems: "end", marginBottom: 12 }}>
        <label className={s.label} style={{ flex: "2 1 240px" }}><span style={{ display: "flex", gap: 6, alignItems: "center" }}><Search size={14}/>Tìm theo tên, ghi chú, số HĐ, MST, SĐT, email</span>
          <input className={ui.field} value={query} onChange={e => setQuery(e.target.value)}/></label>
        <label className={s.label} style={{ flex: "1 1 170px" }}>Thư mục<select className={ui.field} value={folder} onChange={e => setFolder(e.target.value)}><option value="">Tất cả</option>{folderOptions}</select></label>
        <label className={s.label} style={{ flex: "0 1 130px" }}>Từ ngày<input className={ui.field} inputMode="numeric" placeholder="dd/mm/yyyy" value={from} onChange={e => setFrom(maskDay(e.target.value))}/></label>
        <label className={s.label} style={{ flex: "0 1 130px" }}>Đến ngày<input className={ui.field} inputMode="numeric" placeholder="dd/mm/yyyy" value={to} onChange={e => setTo(maskDay(e.target.value))}/></label>
        {!trash && <label className={s.label} style={{ flex: "1 1 220px" }}>Key để xem / gửi / đọc lại<div style={{ display: "flex", gap: 6 }}>
          <input type={showKey ? "text" : "password"} autoComplete="off" spellCheck={false} maxLength={64} className={ui.field} value={key} onChange={e => setKey(e.target.value)} style={{ fontFamily: "monospace" }}/>
          <button type="button" className={s.iconButton} aria-label={showKey ? "Ẩn key" : "Hiện key"} onClick={() => setShowKey(!showKey)}>{showKey ? <EyeOff size={16}/> : <Eye size={16}/>}</button></div>{remembered && <div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>Key được nhớ trong tab này {REMEMBER_MINUTES} phút kể từ lần dùng cuối. <button type="button" onClick={forget} style={{ border: 0, background: "none", padding: 0, color: "var(--primary)", cursor: "pointer", fontSize: 11 }}>Quên key ngay</button></div>}</label>}
      </div>
      {!items ? <p className={s.muted}>Đang tải…</p> : items.length === 0 ? <div className={s.empty}>{trash ? <Trash2 size={40} strokeWidth={1.3}/> : <Archive size={40} strokeWidth={1.3}/>}
          <strong>{query || from || to || folder ? "Không tìm thấy tài liệu phù hợp" : trash ? "Thùng rác trống" : "Kho của bạn chưa có tài liệu"}</strong></div>
        : <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Tài liệu</th><th>Thông tin để tìm</th><th>{trash ? "Ngày xóa" : "Ngày lưu"}</th><th><span className="sr-only">Thao tác</span></th></tr></thead>
          <tbody>{items.map(item => <tr key={item.id}>
            <td style={{ maxWidth: 320 }}><strong style={{ overflowWrap: "anywhere" }}>{item.title}</strong>
              <div className={s.muted} style={{ fontSize: 11 }}>{item.category ? `${item.category} · ` : ""}{item.fileName} · {mb(item.sizeBytes)}{item.pageCount ? ` · ${item.pageCount} trang` : ""}</div>
              {item.note && <div className={s.muted} style={{ fontSize: 12, overflowWrap: "anywhere" }}>{item.note}</div>}
              <Expiry item={item}/>
              {item.sameContractCode.length > 0 && <div style={{ fontSize: 11, color: "#805400" }}>Có thể trùng: cùng số hợp đồng với {item.sameContractCode.length} tài liệu khác</div>}</td>
            <td style={{ fontSize: 12, maxWidth: 300 }}>
              {item.ocrStatus !== "SUCCESS" && <div style={{ color: item.ocrStatus === "INTERRUPTED" || item.ocrStatus === "FAILED" ? "#805400" : undefined }} className={item.ocrStatus === "INTERRUPTED" || item.ocrStatus === "FAILED" ? undefined : s.muted}>{OCR[item.ocrStatus] ?? item.ocrStatus}</div>}
              <div style={{ display: "flex", flexWrap: "wrap", gap: 4, marginTop: 2 }}>{item.keywords.map(k =>
                <span key={k.id} title={k.manual ? "Bạn tự thêm" : "Đọc từ tài liệu"} style={{ border: "1px solid var(--border)", borderRadius: 10, padding: "1px 6px", background: k.manual ? "var(--muted)" : undefined, display: "inline-flex", gap: 3, alignItems: "center" }}>
                  <span className={s.muted}>{KEYWORD[k.type] ?? k.type}:</span>{k.value}
                  {!trash && <button type="button" aria-label={`Bỏ ${k.value}`} disabled={busy !== null} onClick={() => dropKeyword(item, k)} style={{ border: 0, background: "none", cursor: "pointer", padding: 0, lineHeight: 0 }}><X size={11}/></button>}</span>)}
                {item.ocrStatus === "SUCCESS" && !item.keywords.length && <span className={s.muted}>Không đọc được từ khóa</span>}
                {!trash && adding?.id !== item.id && <button type="button" className={s.iconButton} title="Thêm số HĐ, MST, SĐT…" aria-label="Thêm thông tin" disabled={busy !== null} onClick={() => setAdding({ id: item.id, type: "ContractCode", value: "" })}><Plus size={13}/></button>}
              </div>
              {adding?.id === item.id && <div style={{ display: "flex", gap: 4, marginTop: 6, flexWrap: "wrap" }}>
                <select className={ui.field} style={{ width: 90 }} value={adding.type} onChange={e => setAdding({ ...adding, type: e.target.value })}>{Object.entries(KEYWORD).map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select>
                <input className={ui.field} style={{ flex: "1 1 120px" }} autoFocus maxLength={120} value={adding.value} onChange={e => setAdding({ ...adding, value: e.target.value })} onKeyDown={e => { if (e.key === "Enter") addKeyword(); }} placeholder="VD: HDTVP-2026-0099"/>
                <button type="button" className={ui.primaryButton} disabled={busy !== null || !adding.value.trim()} onClick={addKeyword}>Thêm</button>
                <button type="button" className={s.iconButton} aria-label="Hủy" onClick={() => setAdding(null)}><X size={14}/></button></div>}</td>
            <td className={s.muted}>{day(item.trashedAt ?? item.uploadedAt)}{item.trashedAt && <div style={{ fontSize: 11 }}>xóa hẳn sau {Math.max(0, TRASH_DAYS - Math.floor((loadedAt - new Date(item.trashedAt).getTime()) / 86_400_000))} ngày</div>}</td>
            <td><div style={{ display: "flex", gap: 6, flexWrap: "wrap", justifyContent: "flex-end" }}>
              {trash ? <>
                <button type="button" className={ui.secondaryButton} disabled={busy !== null} onClick={() => restore(item)}><RotateCcw size={14}/>Khôi phục</button>
                <button type="button" className={s.iconButton} title="Xóa hẳn" aria-label="Xóa hẳn" disabled={busy !== null} onClick={() => purge(item)}><Trash2 size={16}/></button>
              </> : <>
                <button type="button" className={ui.secondaryButton} disabled={busy !== null} onClick={() => view(item)}>{busy === item.id ? "…" : "Xem"}</button>
                <button type="button" className={s.iconButton} title="Gửi email" aria-label="Gửi email" disabled={busy !== null} onClick={() => setMailing(item)}><Mail size={16}/></button>
                <button type="button" className={s.iconButton} title="Sửa thông tin, hạn, thư mục" aria-label="Sửa" disabled={busy !== null}
                  onClick={() => setEditing({ id: item.id, title: item.title, note: item.note ?? "", category: item.category ?? "", expiresOn: viDay(item.expiresOn), remindDays: String(item.remindDays ?? 30) })}><Pencil size={16}/></button>
                {item.ocrStatus !== "QUEUED" && item.ocrStatus !== "PROCESSING" && <button type="button" className={s.iconButton} title="Đọc lại nội dung" aria-label="Đọc lại" disabled={busy !== null} onClick={() => reread(item)}><RefreshCw size={16}/></button>}
                <button type="button" className={s.iconButton} title="Chuyển vào thùng rác" aria-label="Xóa" disabled={busy !== null} onClick={() => remove(item)}><Trash2 size={16}/></button>
              </>}
            </div></td></tr>)}</tbody></table></div>}
    </section>

    {editing && <section className={ui.card} style={{ marginTop: 16, display: "grid", gap: 10 }} aria-label="Sửa tài liệu">
      <h2 style={{ fontSize: 16 }}>Sửa thông tin tài liệu</h2>
      <label className={s.label}>Tên tài liệu<input className={ui.field} maxLength={300} value={editing.title} onChange={e => setEditing({ ...editing, title: e.target.value })}/></label>
      <label className={s.label}>Thư mục<input className={ui.field} list="vault-folders" maxLength={100} value={editing.category} onChange={e => setEditing({ ...editing, category: e.target.value })}/></label>
      <label className={s.label}>Ghi chú / từ khóa<input className={ui.field} maxLength={1000} value={editing.note} onChange={e => setEditing({ ...editing, note: e.target.value })}/></label>
      <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
        <label className={s.label} style={{ flex: "0 1 180px" }}>Ngày hết hạn<input className={ui.field} inputMode="numeric" placeholder="dd/mm/yyyy" value={editing.expiresOn} onChange={e => setEditing({ ...editing, expiresOn: maskDay(e.target.value) })}/></label>
        <label className={s.label} style={{ flex: "0 1 220px" }}>Email nhắc trước (ngày)<input className={ui.field} type="number" min={1} max={365} disabled={!isoDay(editing.expiresOn)} value={editing.remindDays} onChange={e => setEditing({ ...editing, remindDays: e.target.value })}/></label>
      </div>
      <p className={s.muted} style={{ fontSize: 12 }}>Có ngày hết hạn: hệ thống gửi email nhắc tới bạn theo số ngày trên, rồi 7 ngày trước và đúng ngày hết hạn.</p>
      <div style={{ display: "flex", gap: 8 }}><button type="button" className={ui.primaryButton} disabled={busy !== null} onClick={saveEdit}>Lưu</button><button type="button" className={ui.secondaryButton} onClick={() => setEditing(null)}>Hủy</button></div>
    </section>}

    {mailing && <section className={ui.card} style={{ marginTop: 16, display: "grid", gap: 10 }} aria-label="Gửi email">
      <h2 style={{ fontSize: 16 }}>Gửi “{mailing.title}” qua email</h2>
      <label className={s.label}>Email người nhận (nhiều địa chỉ cách nhau dấu phẩy)<input className={ui.field} value={mailTo} onChange={e => setMailTo(e.target.value)} placeholder="ketoan@congty.vn, doitac@abc.vn"/></label>
      <label className={s.label}>Lời nhắn (không bắt buộc)<textarea className={ui.field} rows={3} maxLength={2000} value={mailNote} onChange={e => setMailNote(e.target.value)}/></label>
      <p className={s.muted} style={{ fontSize: 12 }}>Tệp gửi đi không còn được mã hóa. Thư trả lời sẽ về email của bạn. Mỗi giờ gửi tối đa 20 địa chỉ.</p>
      <div style={{ display: "flex", gap: 8 }}><button type="button" className={ui.primaryButton} disabled={busy !== null || !mailTo.trim()} onClick={send}>{busy === mailing.id ? "Đang gửi…" : "Gửi"}</button><button type="button" className={ui.secondaryButton} onClick={() => setMailing(null)}>Hủy</button></div>
    </section>}

    {preview && <div role="dialog" aria-modal="true" aria-label={preview.title} onClick={() => setPreview(null)}
      style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,.55)", zIndex: 50, display: "flex", alignItems: "center", justifyContent: "center", padding: 16 }}>
      <div onClick={e => e.stopPropagation()} style={{ background: "var(--background, #fff)", borderRadius: 10, width: "min(1000px, 100%)", height: "min(90vh, 1200px)", display: "flex", flexDirection: "column", overflow: "hidden" }}>
        <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "8px 12px", borderBottom: "1px solid var(--border)" }}>
          <strong style={{ flex: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{preview.title}</strong>
          <a className={ui.secondaryButton} href={preview.url} download={preview.title}>Tải về</a>
          <button type="button" className={s.iconButton} aria-label="Đóng" onClick={() => setPreview(null)}><X size={18}/></button></div>
        {preview.type.startsWith("image/") ? <div style={{ flex: 1, overflow: "auto", textAlign: "center" }}>{/* eslint-disable-next-line @next/next/no-img-element */}<img src={preview.url} alt={preview.title} style={{ maxWidth: "100%" }}/></div>
          : <iframe title={preview.title} src={preview.url} style={{ flex: 1, border: 0, width: "100%" }}/>}
      </div></div>}
  </div></>;
}
