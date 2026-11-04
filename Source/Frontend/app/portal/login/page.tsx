"use client";
import { useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { FileText, ArrowRight, Eye, EyeOff } from "lucide-react";
import { APP_NAME } from "@/lib/application-branding";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import { customerApi, type CustomerSession } from "@/services/customer-api";

export default function CustomerLogin() {
  const router = useRouter();
  const [phone, setPhone] = useState(""); const [password, setPassword] = useState("");
  const [show, setShow] = useState(false); const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  const go = (session: CustomerSession) => router.replace(session.mustChangePassword ? "/portal/change-password" : "/portal/contracts");
  useEffect(() => { void customerApi<CustomerSession>("/auth/me").then(go).catch(() => {}); }, []); // eslint-disable-line react-hooks/exhaustive-deps
  async function submit(event: FormEvent) { event.preventDefault(); setBusy(true); setError("");
    try { go(await customerApi<CustomerSession>("/auth/login", "POST", { phone, password })); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); } }
  return <div className={s.login}>
    <section className={s.loginIntro}>
      <div className={s.brand}><span className={s.brandMark}><FileText size={23}/></span>{APP_NAME}</div>
      <div><span className={s.eyebrow}>Cổng khách hàng</span><h1>Xem và phản hồi<br/>hợp đồng của bạn.</h1><p>Đọc đúng phiên bản nhân viên đã gửi, góp ý theo từng điều khoản rồi chọn đồng ý, yêu cầu sửa hoặc từ chối.</p></div>
      <p className={s.muted}>Tài khoản được tạo khi nhân viên gửi hợp đồng cho bạn qua SMS.</p>
    </section>
    <section className={s.loginForm}><h2>Đăng nhập</h2><p className={s.muted}>Dùng số điện thoại nhận SMS và mật khẩu (hoặc mật khẩu tạm trong tin nhắn).</p>
      <form onSubmit={submit}>{error && <p role="alert" className={ui.error}>{error}</p>}
        <label className={s.label}>Số điện thoại<input required inputMode="tel" autoComplete="tel" maxLength={20} className={ui.field} value={phone} onChange={e => setPhone(e.target.value)} placeholder="Ví dụ: 0901234567"/></label>
        <label className={s.label}>Mật khẩu<div style={{ display: "flex", gap: 8 }}><input required autoComplete="current-password" maxLength={200} type={show ? "text" : "password"} className={ui.field} value={password} onChange={e => setPassword(e.target.value)} placeholder="Nhập mật khẩu"/><button type="button" className={s.iconButton} aria-label={show ? "Ẩn mật khẩu" : "Hiện mật khẩu"} onClick={() => setShow(!show)}>{show ? <EyeOff size={18}/> : <Eye size={18}/>}</button></div></label>
        <button className={ui.primaryButton} disabled={busy} type="submit">{busy ? "Đang đăng nhập…" : "Đăng nhập"}<ArrowRight size={17}/></button>
      </form><p className={s.muted} style={{ marginTop: 28 }}>Quên mật khẩu hoặc mật khẩu tạm hết hạn? Liên hệ nhân viên phụ trách hợp đồng.</p>
    </section>
  </div>;
}
