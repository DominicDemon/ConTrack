"use client";
import { Suspense, useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { ArrowLeft, FileText, KeyRound, MailCheck } from "lucide-react";
import { APP_NAME } from "@/lib/application-branding";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import { customerApi, type CustomerSession } from "@/services/customer-api";

/** Forgotten password, or the first one from the welcome mail (?welcome=1): a code to the email of the account, then a password. */
export default function CustomerForgotPasswordPage() {
  return <Suspense><CustomerForgotPassword/></Suspense>;
}

function CustomerForgotPassword() {
  const router = useRouter();
  const welcome = useSearchParams().has("welcome");
  const [phone, setPhone] = useState(""); const [code, setCode] = useState(""); const [next, setNext] = useState(""); const [again, setAgain] = useState("");
  const [sent, setSent] = useState<string | null>(null); const [wait, setWait] = useState(0);
  const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  const weak = next.length > 0 && (next.length < 8 || !/\p{L}/u.test(next) || !/\d/.test(next));
  useEffect(() => { if (wait <= 0) return; const timer = setTimeout(() => setWait(wait - 1), 1000); return () => clearTimeout(timer); }, [wait]);

  async function send(event?: FormEvent) {
    event?.preventDefault(); setError("");
    if (!phone.trim()) { setError("Nhập số điện thoại đăng nhập."); return; }
    setBusy(true);
    try { setSent((await customerApi<{ message: string }>("/auth/forgot-password", "POST", { phone: phone.trim() })).message); setWait(60); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  async function reset(event: FormEvent) {
    event.preventDefault(); setError("");
    if (!/^\d{6}$/.test(code.trim())) { setError("Nhập mã 6 số trong email."); return; }
    if (weak) { setError("Mật khẩu mới cần tối thiểu 8 ký tự, có cả chữ và số."); return; }
    if (next !== again) { setError("Mật khẩu nhập lại không khớp."); return; }
    setBusy(true);
    try { await customerApi<CustomerSession>("/auth/reset-password", "POST", { phone: phone.trim(), code: code.trim(), newPassword: next }); router.replace("/portal/contracts"); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }

  return <div className={s.login}>
    <section className={s.loginIntro}>
      <div className={s.brand}><span className={s.brandMark}><FileText size={23}/></span>{APP_NAME}</div>
      <div><span className={s.eyebrow}>Cổng khách hàng</span><h1>{welcome ? <>Đặt mật khẩu<br/>cho tài khoản.</> : <>Đặt lại<br/>mật khẩu.</>}</h1><p>Mã xác nhận được gửi tới email của tài khoản, là email bạn nhận link hợp đồng.</p></div>
      <p className={s.muted}>Tài khoản chưa có email thì liên hệ nhân viên phụ trách hợp đồng để được cấp mật khẩu tạm.</p>
    </section>
    <section className={s.loginForm}><h2 style={{ display: "flex", gap: 10, alignItems: "center" }}><KeyRound/>{welcome ? "Đặt mật khẩu" : "Quên mật khẩu"}</h2>
      {error && <p role="alert" className={ui.error}>{error}</p>}
      {sent === null ? <form onSubmit={send}>
        <p className={s.muted}>Nhập số điện thoại bạn dùng để đăng nhập.</p>
        <label className={s.label}>Số điện thoại<input required inputMode="tel" autoComplete="tel" maxLength={20} className={ui.field} value={phone} onChange={e => setPhone(e.target.value)} placeholder="Ví dụ: 0901234567"/></label>
        <button className={ui.primaryButton} disabled={busy} type="submit">{busy ? "Đang gửi…" : "Gửi mã qua email"}<MailCheck size={17}/></button>
      </form> : <form onSubmit={reset}>
        <p className={ui.notice}>{sent} Không thấy thư thì xem cả mục Spam.</p>
        <label className={s.label}>Mã xác nhận (6 số)<input required inputMode="numeric" autoComplete="one-time-code" maxLength={6} className={ui.field} value={code} onChange={e => setCode(e.target.value.replace(/\D/g, ""))} style={{ letterSpacing: 4 }}/></label>
        <label className={s.label}>Mật khẩu mới<input required type="password" autoComplete="new-password" minLength={8} maxLength={100} className={ui.field} value={next} onChange={e => setNext(e.target.value)}/></label>
        <p className={weak ? ui.error : s.muted}>Tối thiểu 8 ký tự, có cả chữ và số.</p>
        <label className={s.label}>Nhập lại mật khẩu mới<input required type="password" autoComplete="new-password" maxLength={100} className={ui.field} value={again} onChange={e => setAgain(e.target.value)}/></label>
        <button className={ui.primaryButton} disabled={busy} type="submit">{busy ? "Đang lưu…" : "Đặt mật khẩu mới và đăng nhập"}</button>
        <button className={ui.secondaryButton} disabled={busy || wait > 0} type="button" onClick={() => void send()}>{wait > 0 ? `Gửi lại mã sau ${wait} giây` : "Gửi lại mã"}</button>
      </form>}
      <p className={s.muted} style={{ marginTop: 28 }}><Link href="/portal/login" style={{ display: "inline-flex", gap: 6, alignItems: "center" }}><ArrowLeft size={15}/>Quay lại đăng nhập</Link></p>
    </section>
  </div>;
}
