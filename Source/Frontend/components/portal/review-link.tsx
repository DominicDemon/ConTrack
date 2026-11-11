"use client";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { ArrowRight, FileText } from "lucide-react";
import { APP_NAME } from "@/lib/application-branding";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import ReviewView from "./review-view";
import { dateTime } from "@/services/internal-api";
import { customerApi, type ReviewLinkStatus } from "@/services/customer-api";

/** Entry from the emailed link: phone, then one-time code, then the one review the link belongs to. */
export default function ReviewLink({ token }: { token: string }) {
  const [status, setStatus] = useState<ReviewLinkStatus | null>(null); const [closed, setClosed] = useState("");
  const [reviewId, setReviewId] = useState<string | null>(null);
  const [phone, setPhone] = useState(""); const [otp, setOtp] = useState(""); const [otpSent, setOtpSent] = useState(false);
  const [busy, setBusy] = useState(false); const [error, setError] = useState(""); const [notice, setNotice] = useState("");
  const check = useCallback(() => customerApi<ReviewLinkStatus>("/link/status", "POST", { token })
    .then(r => { setStatus(r); setReviewId(r.reviewId); }).catch(e => setClosed((e as Error).message)), [token]);
  useEffect(() => { void check(); }, [check]);
  const lost = useCallback(() => { setReviewId(null); setStatus(null); setOtp(""); setOtpSent(false); void check(); }, [check]);

  async function requestOtp() { setBusy(true); setError(""); setNotice("");
    try { await customerApi("/link/otp", "POST", { token, phone }); setOtpSent(true); setOtp(""); setNotice("Đã gửi mã xác nhận. Mã có hiệu lực trong 5 phút."); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); } }
  async function verify() { setBusy(true); setError(""); setNotice("");
    try { setReviewId((await customerApi<{ reviewId: string }>("/link/verify", "POST", { token, otp })).reviewId); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); } }
  function submit(event: FormEvent) { event.preventDefault(); void (otpSent ? verify() : requestOtp()); }

  if (reviewId) return <ReviewView id={reviewId} onAuthLost={lost}/>;
  return <div className={s.login}>
    <section className={s.loginIntro}>
      <div className={s.brand}><span className={s.brandMark}><FileText size={23}/></span>{APP_NAME}</div>
      <div><span className={s.eyebrow}>Cổng khách hàng</span><h1>Xem và phản hồi<br/>hợp đồng của bạn.</h1><p>Đọc đúng phiên bản nhân viên đã gửi, góp ý theo từng điều khoản rồi chọn đồng ý, yêu cầu sửa hoặc từ chối.</p></div>
      {status && <p className={s.muted}>Link có hiệu lực đến {dateTime(status.expiresAt)}.</p>}
    </section>
    <section className={s.loginForm}>
      {closed ? <><h2>Không mở được link</h2><p role="alert" className={ui.error}>{closed}</p></>
        : !status ? <p className={s.muted}>Đang kiểm tra link…</p>
        : <><h2>Xác nhận số điện thoại</h2><p className={s.muted}>{otpSent ? "Nhập mã 6 số vừa được gửi tới số điện thoại của bạn." : "Nhập số điện thoại ghi trong hợp đồng để nhận mã xác nhận."}</p>
          <form onSubmit={submit}>{error && <p role="alert" className={ui.error}>{error}</p>}{notice && <p role="status" className={ui.notice}>{notice}</p>}
            <label className={s.label}>Số điện thoại<input required disabled={otpSent} inputMode="tel" autoComplete="tel" maxLength={20} className={ui.field} value={phone} onChange={e => setPhone(e.target.value)} placeholder="Ví dụ: 0901234567"/></label>
            {otpSent && <label className={s.label}>Mã xác nhận<input required autoFocus inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" maxLength={6} className={ui.field} value={otp} onChange={e => setOtp(e.target.value)} placeholder="6 chữ số"/></label>}
            <button className={ui.primaryButton} disabled={busy} type="submit">{busy ? "Đang xử lý…" : otpSent ? "Xác nhận" : "Gửi mã xác nhận"}<ArrowRight size={17}/></button>
            {otpSent && <button type="button" className={ui.secondaryButton} disabled={busy} onClick={() => void requestOtp()}>Gửi lại mã</button>}
          </form></>}
      <p className={s.muted} style={{ marginTop: 28 }}>Cần hỗ trợ hoặc cần link mới? Liên hệ nhân viên phụ trách hợp đồng.</p>
    </section>
  </div>;
}
