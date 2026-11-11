"use client";
import { useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { FileText, ArrowRight, Eye, EyeOff, Check } from "lucide-react";
import { APP_NAME } from "@/lib/application-branding";
import { internalApi, type InternalUser } from "@/services/internal-api";
import { InternalTheme, internalStyles as ui } from "./internal-theme";
import s from "./workspace.module.css";

function nextWorkspacePath(): string {
  const next = new URLSearchParams(window.location.search).get("next");
  return next?.startsWith("/workspace/") && !next.startsWith("//") ? next : "/workspace/contracts/drafts";
}

export default function InternalLogin() {
  const router = useRouter();
  const [username, setUsername] = useState(""); const [password, setPassword] = useState("");
  const [show, setShow] = useState(false); const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  useEffect(() => { void internalApi<InternalUser>("/auth/me").then(() => router.replace(nextWorkspacePath())).catch(() => {}); }, [router]);
  async function submit(event: FormEvent) { event.preventDefault(); setBusy(true); setError("");
    try { await internalApi("/auth/login", "POST", { username, password }); router.replace(nextWorkspacePath()); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); } }
  return <InternalTheme><div className={s.login}>
    <section className={s.loginIntro}>
      <div className={s.brand}><span className={s.brandMark}><FileText size={23}/></span>{APP_NAME}</div>
      <div><span className={s.eyebrow}>Không gian làm việc nội bộ</span><h1>Từ bản nháp đầu tiên,<br/>đến một thỏa thuận rõ ràng.</h1><p>Soạn hợp đồng theo mẫu, kiểm tra nội dung và cùng hoàn thiện từng điều khoản trong một không gian thống nhất.</p>
        <div style={{ display: "grid", gap: 16, marginTop: 32 }}>{["Thông tin tập trung, dễ tìm lại", "Soạn và xem trước trên cùng màn hình", "Phân quyền theo trách nhiệm"].map(text => <div key={text} style={{ display: "flex", alignItems: "center", gap: 12, fontSize: 14 }}><Check size={18} color="var(--primary)"/>{text}</div>)}</div>
      </div><p className={s.muted}>Hệ thống quản lý hợp đồng dành cho nhân viên công ty.</p>
    </section>
    <section className={s.loginForm}><span className={s.eyebrow}>Chào mừng trở lại</span><h2 style={{ marginTop: 12 }}>Đăng nhập</h2><p className={s.muted}>Sử dụng tài khoản nhân viên nội bộ để tiếp tục. Tài khoản sysadmin được liên kết với vai trò Giám đốc và dùng chính mật khẩu System Admin.</p><p className={ui.notice} role="note">Sau khi đăng nhập, hệ thống sẽ mở lại hồ sơ bạn vừa chọn.</p>
      <form onSubmit={submit}>{error && <p role="alert" className={ui.error}>{error}</p>}
        <label className={s.label}>Tên đăng nhập<input required autoComplete="username" maxLength={100} className={ui.field} value={username} onChange={e => setUsername(e.target.value)} placeholder="Nhập tên đăng nhập"/></label>
        <label className={s.label}>Mật khẩu<div style={{ display: "flex", gap: 8 }}><input required autoComplete="current-password" type={show ? "text" : "password"} className={ui.field} value={password} onChange={e => setPassword(e.target.value)} placeholder="Nhập mật khẩu"/><button type="button" className={s.iconButton} aria-label={show ? "Ẩn mật khẩu" : "Hiện mật khẩu"} onClick={() => setShow(!show)}>{show ? <EyeOff size={18}/> : <Eye size={18}/>}</button></div></label>
        <button className={ui.primaryButton} disabled={busy} type="submit">{busy ? "Đang đăng nhập…" : "Đăng nhập"}<ArrowRight size={17}/></button>
      </form><p className={s.muted} style={{ marginTop: 28 }}>Chưa có tài khoản? Liên hệ người quản trị nội bộ.</p>
    </section>
  </div></InternalTheme>;
}
