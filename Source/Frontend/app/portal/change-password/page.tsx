"use client";
import { useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { KeyRound } from "lucide-react";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import PortalHeader from "@/components/portal/portal-header";
import { customerApi, handleAuthError, type CustomerSession } from "@/services/customer-api";

export default function CustomerChangePassword() {
  const router = useRouter();
  const [session, setSession] = useState<CustomerSession | null>(null);
  const [current, setCurrent] = useState(""); const [next, setNext] = useState(""); const [again, setAgain] = useState("");
  const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  useEffect(() => { void customerApi<CustomerSession>("/auth/me").then(setSession).catch(e => { if (handleAuthError(e, router.replace)) setError(e.message); }); }, [router]);
  const weak = next.length > 0 && (next.length < 8 || !/\p{L}/u.test(next) || !/\d/.test(next));
  async function submit(event: FormEvent) {
    event.preventDefault(); setError("");
    if (next !== again) { setError("Mật khẩu nhập lại không khớp."); return; }
    setBusy(true);
    try { await customerApi("/auth/change-password", "POST", { currentPassword: current, newPassword: next }); router.replace("/portal/contracts"); }
    catch (e) { if (handleAuthError(e, router.replace)) setError((e as Error).message); } finally { setBusy(false); }
  }
  return <><PortalHeader name={session?.name}/><div className={s.page} style={{ maxWidth: 520, margin: "0 auto" }}>
    <span className={s.eyebrow}>Bảo mật tài khoản</span><h1 style={{ display: "flex", gap: 10, alignItems: "center" }}><KeyRound/>Đổi mật khẩu</h1>
    <p className={s.muted}>{session?.mustChangePassword ? "Bạn đang dùng mật khẩu tạm. Hãy đặt mật khẩu mới trước khi xem hợp đồng." : "Đặt mật khẩu mới cho tài khoản của bạn."}</p>
    <form className={ui.card} onSubmit={submit} style={{ display: "grid", gap: 14, marginTop: 18 }}>
      {error && <p role="alert" className={ui.error}>{error}</p>}
      <label className={s.label}>{session?.mustChangePassword ? "Mật khẩu tạm trong SMS" : "Mật khẩu hiện tại"}<input required type="password" autoComplete="current-password" maxLength={200} className={ui.field} value={current} onChange={e => setCurrent(e.target.value)}/></label>
      <label className={s.label}>Mật khẩu mới<input required type="password" autoComplete="new-password" minLength={8} maxLength={100} aria-describedby="password-rule" className={ui.field} value={next} onChange={e => setNext(e.target.value)}/></label>
      <p id="password-rule" className={weak ? ui.error : s.muted}>Tối thiểu 8 ký tự, có cả chữ và số, khác mật khẩu hiện tại.</p>
      <label className={s.label}>Nhập lại mật khẩu mới<input required type="password" autoComplete="new-password" maxLength={100} className={ui.field} value={again} onChange={e => setAgain(e.target.value)}/></label>
      <button className={ui.primaryButton} disabled={busy || weak} type="submit">{busy ? "Đang lưu…" : "Đổi mật khẩu"}</button>
    </form>
  </div></>;
}
