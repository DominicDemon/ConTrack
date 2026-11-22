"use client";
import { useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { KeyRound } from "lucide-react";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import PortalHeader from "@/components/portal/portal-header";
import { customerApi, handleAuthError, type CustomerSession } from "@/services/customer-api";

type KeyStatus = { hasKey: boolean; setAt: string | null; email: string | null; serverKeepsRecoveryKey: boolean };

/** Same rules as the server (FileKeyPolicy): shown while typing. */
const rules = (key: string) => [
  { label: "Từ 12 đến 64 ký tự", ok: key.length >= 12 && key.length <= 64 },
  { label: "Chỉ chữ không dấu, số, ký tự đặc biệt; không dấu cách", ok: key.length > 0 && /^[\x21-\x7e]+$/.test(key) },
  { label: "Có chữ hoa và chữ thường", ok: /[A-Z]/.test(key) && /[a-z]/.test(key) },
  { label: "Có số và ký tự đặc biệt (!@#$…)", ok: /\d/.test(key) && /[^A-Za-z0-9]/.test(key) },
  { label: "Không có 4 ký tự lặp hoặc liền thứ tự (aaaa, 1234, abcd)", ok: key.length > 0 && !hasRun(key) },
  { label: "Không chứa từ dễ đoán (password, 123456, qwerty…)", ok: key.length > 0 && !COMMON.some(word => key.toLowerCase().includes(word)) },
];

const COMMON = ["password", "passw0rd", "matkhau", "123456", "654321", "qwerty", "asdfgh", "zxcvbn", "abc123", "iloveyou",
  "admin", "contrack", "welcome", "letmein", "111111", "000000", "123123", "1q2w3e", "baomat", "hopdong"];

function hasRun(key: string) {
  const lower = key.toLowerCase();
  for (let i = 0; i + 3 < lower.length; i++) {
    const steps = [1, 2, 3].map(j => lower.charCodeAt(i + j) - lower.charCodeAt(i + j - 1));
    if (steps.every(step => step === 0)) return true;
    if ((steps.every(step => step === 1) || steps.every(step => step === -1)) && /[a-z0-9]/.test(lower[i]) && /[a-z0-9]/.test(lower[i + 3])) return true;
  }
  return false;
}

function NewKey({ value, again, onValue, onAgain, disabled }: { value: string; again: string; onValue: (v: string) => void; onAgain: (v: string) => void; disabled: boolean }) {
  return <>
    <label className={s.label}>Key mới<input type="password" autoComplete="new-password" spellCheck={false} maxLength={64} className={ui.field} value={value} disabled={disabled} onChange={e => onValue(e.target.value)} style={{ fontFamily: "monospace" }}/></label>
    <ul style={{ margin: 0, paddingLeft: 18, fontSize: 12 }}>{rules(value).map(rule => <li key={rule.label} style={{ color: rule.ok ? "var(--primary)" : undefined }} className={rule.ok ? undefined : s.muted}>{rule.ok ? "✓ " : ""}{rule.label}</li>)}
</ul>
    <p className={s.muted} style={{ fontSize: 12, margin: 0 }}>Khi lưu, hệ thống kiểm tra thêm: key không chứa số điện thoại, email của bạn và không trùng mật khẩu đăng nhập.</p>
    <label className={s.label}>Nhập lại key mới<input type="password" autoComplete="new-password" spellCheck={false} maxLength={64} className={ui.field} value={again} disabled={disabled} onChange={e => onAgain(e.target.value)} style={{ fontFamily: "monospace" }}/></label>
  </>;
}

/** Create the first key, or a new one after forgetting the old: password, then a code sent to the email. */
function VerifiedForm({ status, replacing, onDone }: { status: KeyStatus; replacing: boolean; onDone: (message: string) => void }) {
  const router = useRouter();
  const [password, setPassword] = useState(""); const [code, setCode] = useState(""); const [value, setValue] = useState(""); const [again, setAgain] = useState("");
  const [sentTo, setSentTo] = useState<string | null>(null); const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  if (!status.email) return <p className={s.muted}>Tài khoản chưa có email để nhận mã. Thêm email ở <Link href="/portal/profile">Hồ sơ của tôi</Link> rồi quay lại.</p>;
  async function send() {
    setError(""); if (!password) { setError("Nhập mật khẩu đăng nhập."); return; }
    setBusy(true);
    try { setSentTo((await customerApi<{ sentTo: string }>("/file-key/code", "POST", { password })).sentTo); }
    catch (e) { if (handleAuthError(e, router.replace)) setError((e as Error).message); } finally { setBusy(false); }
  }
  async function submit(event: FormEvent) {
    event.preventDefault(); setError("");
    if (!/^\d{6}$/.test(code.trim())) { setError("Nhập mã 6 số trong email."); return; }
    if (!rules(value).every(rule => rule.ok)) { setError("Key mới chưa đạt yêu cầu."); return; }
    if (value !== again) { setError("Hai lần nhập key chưa giống nhau."); return; }
    setBusy(true);
    try {
      const result = await customerApi<{ recoveredFiles: number | null }>("/file-key", "POST", { password, code: code.trim(), newKey: value });
      onDone((replacing ? "Đã đặt key mới; key cũ không còn dùng được." : "Đã tạo key.") + (result.recoveredFiles ? ` ${result.recoveredFiles} bản scan đã sẵn sàng.` : ""));
    } catch (e) { if (handleAuthError(e, router.replace)) setError((e as Error).message); } finally { setBusy(false); }
  }
  return <form onSubmit={submit} style={{ display: "grid", gap: 14 }}>
    {error && <p role="alert" className={ui.error}>{error}</p>}
    <label className={s.label}>Mật khẩu đăng nhập<input type="password" autoComplete="current-password" maxLength={200} className={ui.field} value={password} disabled={busy || sentTo !== null} onChange={e => setPassword(e.target.value)}/></label>
    {sentTo === null ? <button type="button" className={ui.primaryButton} disabled={busy} onClick={() => void send()}>{busy ? "Đang gửi…" : `Gửi mã tới ${status.email}`}</button> : <>
      <label className={s.label}>Mã xác nhận (6 số, đã gửi tới {sentTo})<input inputMode="numeric" autoComplete="one-time-code" maxLength={6} className={ui.field} value={code} disabled={busy} onChange={e => setCode(e.target.value.replace(/\D/g, ""))} style={{ letterSpacing: 4, maxWidth: 180 }}/></label>
      <NewKey value={value} again={again} onValue={setValue} onAgain={setAgain} disabled={busy}/>
      <button type="submit" className={ui.primaryButton} disabled={busy}>{busy ? "Đang lưu…" : replacing ? "Đặt key mới" : "Tạo key"}</button>
    </>}
  </form>;
}

function ChangeForm({ onDone }: { onDone: (message: string) => void }) {
  const router = useRouter();
  const [current, setCurrent] = useState(""); const [value, setValue] = useState(""); const [again, setAgain] = useState("");
  const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  async function submit(event: FormEvent) {
    event.preventDefault(); setError("");
    if (!current) { setError("Nhập key hiện tại."); return; }
    if (!rules(value).every(rule => rule.ok)) { setError("Key mới chưa đạt yêu cầu."); return; }
    if (value !== again) { setError("Hai lần nhập key chưa giống nhau."); return; }
    setBusy(true);
    try { await customerApi("/file-key/change", "POST", { currentKey: current, newKey: value }); onDone("Đã đổi key. Từ giờ mở bản scan bằng key mới."); }
    catch (e) { if (handleAuthError(e, router.replace)) setError((e as Error).message); } finally { setBusy(false); }
  }
  return <form onSubmit={submit} style={{ display: "grid", gap: 14 }}>
    {error && <p role="alert" className={ui.error}>{error}</p>}
    <label className={s.label}>Key hiện tại<input type="password" autoComplete="off" spellCheck={false} maxLength={64} className={ui.field} value={current} disabled={busy} onChange={e => setCurrent(e.target.value)} style={{ fontFamily: "monospace" }}/></label>
    <NewKey value={value} again={again} onValue={setValue} onAgain={setAgain} disabled={busy}/>
    <button type="submit" className={ui.primaryButton} disabled={busy}>{busy ? "Đang lưu…" : "Đổi key"}</button>
  </form>;
}

/** Page "Key xem bản scan": the customer creates, changes or resets the key that opens their scans. */
export default function CustomerFileKeyPage() {
  const router = useRouter();
  const [session, setSession] = useState<CustomerSession | null>(null); const [status, setStatus] = useState<KeyStatus | null>(null);
  const [tab, setTab] = useState<"change" | "forgot">("change"); const [notice, setNotice] = useState(""); const [error, setError] = useState(""); const [round, setRound] = useState(0);
  useEffect(() => {
    void Promise.all([customerApi<CustomerSession>("/auth/me"), customerApi<KeyStatus>("/file-key")])
      .then(([me, keyStatus]) => { setSession(me); setStatus(keyStatus); })
      .catch(e => { if (handleAuthError(e, router.replace)) setError(e.message); });
  }, [router, round]);
  const done = (message: string) => { setNotice(message); setRound(round + 1); };

  return <><PortalHeader name={session?.name}/><div className={s.page} style={{ maxWidth: 560, margin: "0 auto" }}>
    <span className={s.eyebrow}>Bảo mật</span><h1 style={{ display: "flex", gap: 10, alignItems: "center" }}><KeyRound/>Key xem bản scan</h1>
    <p className={s.muted}>Bản scan hợp đồng được mã hóa. Bạn tự đặt key để mở; chỉ bạn biết key, công ty không xem được. <Link href="/portal/scans">Xem bản scan</Link></p>
    {error && <p role="alert" className={ui.error}>{error}</p>}
    {notice && <p role="status" className={ui.card} style={{ marginTop: 12 }}>{notice}</p>}
    {!status ? !error && <p className={s.muted}>Đang tải…</p> : !status.hasKey
      ? <section className={ui.card} style={{ marginTop: 18 }}><h2 style={{ fontSize: 16, marginBottom: 12 }}>Tạo key</h2><VerifiedForm key={round} status={status} replacing={false} onDone={done}/></section>
      : <section className={ui.card} style={{ marginTop: 18 }}>
        <p className={s.muted} style={{ marginBottom: 12 }}>Key đang dùng{status.setAt ? `, đặt lúc ${new Date(status.setAt).toLocaleString("vi-VN")}` : ""}.</p>
        <div style={{ display: "flex", gap: 8, marginBottom: 16 }}>
          <button type="button" className={tab === "change" ? ui.primaryButton : ui.secondaryButton} onClick={() => setTab("change")}>Đổi key</button>
          <button type="button" className={tab === "forgot" ? ui.primaryButton : ui.secondaryButton} onClick={() => setTab("forgot")}>Quên key</button>
        </div>
        {tab === "change" ? <ChangeForm key={`c${round}`} onDone={done}/> : <VerifiedForm key={`f${round}`} status={status} replacing onDone={done}/>}
      </section>}
  </div></>;
}
