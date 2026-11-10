"use client";
import { useState, type FormEvent } from "react";
import { FileText, KeyRound, Eye, EyeOff, LogOut } from "lucide-react";
import { APP_NAME } from "@/lib/application-branding";
import { internalApi, type InternalUser } from "@/services/internal-api";
import { internalStyles as ui } from "./internal-theme";
import s from "./workspace.module.css";

/** Shown by the shell instead of the workspace while the account still has an admin-issued temporary password. */
export default function ChangePassword({ user, onChanged, onLogout }: { user: InternalUser; onChanged: (user: InternalUser) => void; onLogout: () => void }) {
  const [current, setCurrent] = useState(""); const [next, setNext] = useState(""); const [confirm, setConfirm] = useState("");
  const [show, setShow] = useState(false); const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  const mismatch = confirm.length > 0 && confirm !== next;
  async function submit(event: FormEvent) { event.preventDefault(); if (mismatch) return; setBusy(true); setError("");
    try { onChanged(await internalApi<InternalUser>("/auth/change-password", "POST", { currentPassword: current, newPassword: next })); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); } }
  const type = show ? "text" : "password";
  return <div className={s.login}>
    <section className={s.loginIntro}>
      <div className={s.brand}><span className={s.brandMark}><FileText size={23}/></span>{APP_NAME}</div>
      <div><span className={s.eyebrow}>Bảo mật tài khoản</span><h1>Đặt mật khẩu<br/>của riêng bạn.</h1><p>Tài khoản {user.username} đang dùng mật khẩu tạm do quản trị viên cấp. Đổi mật khẩu để bắt đầu làm việc.</p></div>
      <p className={s.muted}>Không ai khác, kể cả quản trị viên, biết mật khẩu mới của bạn.</p>
    </section>
    <section className={s.loginForm}><span className={s.eyebrow}>Lần đăng nhập đầu tiên</span><h2 style={{ marginTop: 12 }}>Đổi mật khẩu</h2><p className={s.muted}>Mật khẩu mới cần tối thiểu 8 ký tự, có cả chữ và số.</p>
      <form onSubmit={submit}>{error && <p role="alert" className={ui.error}>{error}</p>}
        <label className={s.label}>Mật khẩu tạm<div style={{ display: "flex", gap: 8 }}><input required autoComplete="current-password" type={type} maxLength={200} className={ui.field} value={current} onChange={e => setCurrent(e.target.value)} placeholder="Nhập mật khẩu tạm được cấp"/><button type="button" className={s.iconButton} aria-label={show ? "Ẩn mật khẩu" : "Hiện mật khẩu"} onClick={() => setShow(!show)}>{show ? <EyeOff size={18}/> : <Eye size={18}/>}</button></div></label>
        <label className={s.label}>Mật khẩu mới<input required autoComplete="new-password" type={type} minLength={8} maxLength={100} className={ui.field} value={next} onChange={e => setNext(e.target.value)} placeholder="Tối thiểu 8 ký tự, có chữ và số"/></label>
        <label className={s.label}>Nhập lại mật khẩu mới<input required autoComplete="new-password" type={type} maxLength={100} aria-invalid={mismatch} className={ui.field} value={confirm} onChange={e => setConfirm(e.target.value)} placeholder="Nhập lại mật khẩu mới"/>{mismatch && <span role="alert" className={s.muted} style={{ color: "var(--destructive)" }}>Mật khẩu nhập lại không khớp.</span>}</label>
        <button className={ui.primaryButton} disabled={busy || mismatch} type="submit">{busy ? "Đang lưu…" : "Đổi mật khẩu"}<KeyRound size={17}/></button>
        <button className={ui.secondaryButton} disabled={busy} type="button" onClick={onLogout}><LogOut size={17}/>Đăng xuất</button>
      </form>
    </section>
  </div>;
}
