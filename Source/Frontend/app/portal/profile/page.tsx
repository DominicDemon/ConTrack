"use client";
import { useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Building2, KeyRound, Mail, UserRound } from "lucide-react";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import PortalHeader from "@/components/portal/portal-header";
import { customerApi, handleAuthError } from "@/services/customer-api";

/** GET /api/customer/profile. */
type Profile = {
  name: string; phone: string; email: string | null; emailVerifiedAt: string | null; address: string | null; taxCode: string | null;
  createdAt: string; lastLoginAt: string | null;
  businesses: { legalName: string; taxCode: string; address: string | null; representative: string | null }[];
};

const when = (value: string | null) => value ? new Date(value).toLocaleString("vi-VN", { dateStyle: "short", timeStyle: "short" }) : "—";

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return <div style={{ display: "grid", gridTemplateColumns: "minmax(120px, 34%) 1fr", gap: 12, padding: "8px 0", borderBottom: "1px solid var(--border)" }}>
    <span className={s.muted}>{label}</span><span style={{ overflowWrap: "anywhere" }}>{value}</span></div>;
}

/** New email: portal password, then a code sent to the new address proves it is the customer's. */
function EmailForm({ current, onSaved }: { current: string | null; onSaved: (profile: Profile) => void }) {
  const router = useRouter();
  const [email, setEmail] = useState(""); const [password, setPassword] = useState(""); const [code, setCode] = useState("");
  const [sentTo, setSentTo] = useState<string | null>(null); const [wait, setWait] = useState(0);
  const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  useEffect(() => { if (wait <= 0) return; const timer = setTimeout(() => setWait(wait - 1), 1000); return () => clearTimeout(timer); }, [wait]);

  async function send() {
    setError("");
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) { setError("Nhập email mới hợp lệ."); return; }
    if (!password) { setError("Nhập mật khẩu của bạn."); return; }
    setBusy(true);
    try { setSentTo((await customerApi<{ sentTo: string }>("/profile/email/code", "POST", { password, newEmail: email.trim() })).sentTo); setWait(60); }
    catch (e) { if (handleAuthError(e, router.replace)) setError((e as Error).message); } finally { setBusy(false); }
  }
  async function confirm(event: FormEvent) {
    event.preventDefault(); setError("");
    if (!/^\d{6}$/.test(code.trim())) { setError("Nhập mã 6 số đã gửi tới email mới."); return; }
    setBusy(true);
    try { onSaved(await customerApi<Profile>("/profile/email", "POST", { code: code.trim() })); }
    catch (e) { if (handleAuthError(e, router.replace)) setError((e as Error).message); } finally { setBusy(false); }
  }

  return <form onSubmit={confirm} style={{ display: "grid", gap: 14 }}>
    {error && <p role="alert" className={ui.error}>{error}</p>}
    <label className={s.label}>{current ? "Email mới" : "Email"}<input type="email" autoComplete="email" maxLength={100} className={ui.field} value={email} disabled={busy || sentTo !== null} onChange={e => setEmail(e.target.value)} placeholder="ten@congty.vn"/></label>
    <label className={s.label}>Mật khẩu của bạn<input type="password" autoComplete="current-password" maxLength={200} className={ui.field} value={password} disabled={busy || sentTo !== null} onChange={e => setPassword(e.target.value)}/></label>
    {sentTo === null
      ? <button type="button" className={ui.primaryButton} disabled={busy} onClick={send}>{busy ? "Đang gửi…" : "Gửi mã tới email mới"}</button>
      : <>
        <label className={s.label}>Mã xác nhận (6 số, đã gửi tới {sentTo})<input inputMode="numeric" autoComplete="one-time-code" maxLength={6} className={ui.field} value={code} disabled={busy} onChange={e => setCode(e.target.value.replace(/\D/g, ""))} style={{ letterSpacing: 4, maxWidth: 180 }}/></label>
        <p className={s.muted}>Mã có hiệu lực 5 phút. Không thấy thư thì xem cả mục Spam.</p>
        <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
          <button type="submit" className={ui.primaryButton} disabled={busy}>{busy ? "Đang lưu…" : "Xác nhận đổi email"}</button>
          <button type="button" className={ui.secondaryButton} disabled={busy || wait > 0} onClick={send}>{wait > 0 ? `Gửi lại sau ${wait} giây` : "Gửi lại mã"}</button>
          <button type="button" className={ui.secondaryButton} disabled={busy} onClick={() => { setSentTo(null); setCode(""); setError(""); }}>Sửa email</button>
        </div>
      </>}
  </form>;
}

export default function CustomerProfilePage() {
  const router = useRouter();
  const [profile, setProfile] = useState<Profile | null>(null);
  const [error, setError] = useState(""); const [notice, setNotice] = useState("");
  const [round, setRound] = useState(0);
  useEffect(() => { void customerApi<Profile>("/profile").then(setProfile).catch(e => { if (handleAuthError(e, router.replace)) setError(e.message); }); }, [router]);

  return <><PortalHeader name={profile?.name}/><div className={s.page} style={{ maxWidth: 720, margin: "0 auto" }}>
    <span className={s.eyebrow}>Tài khoản</span><h1 style={{ display: "flex", gap: 10, alignItems: "center" }}><UserRound/>Hồ sơ của tôi</h1>
    <p className={s.muted}>Tên, số điện thoại đăng nhập và mã số thuế do nhân viên phụ trách cập nhật. Cần sửa thì gửi yêu cầu ở mục <Link href="/portal/support">Hỗ trợ</Link>.</p>
    {error && <p role="alert" className={ui.error}>{error}</p>}
    {notice && <p role="status" className={ui.card} style={{ marginTop: 12 }}>{notice}</p>}
    {!profile ? !error && <p className={s.muted} style={{ marginTop: 18 }}>Đang tải…</p> : <>
      <section className={ui.card} style={{ marginTop: 18 }}>
        <h2 style={{ fontSize: 16, marginBottom: 8 }}>Thông tin</h2>
        <Row label="Họ tên / tên khách hàng" value={profile.name}/>
        <Row label="Số điện thoại đăng nhập" value={profile.phone}/>
        <Row label="Email" value={<>{profile.email ?? "Chưa có"}{profile.email && <span className={s.muted}>{profile.emailVerifiedAt ? " · đã xác nhận" : " · chưa xác nhận"}</span>}</>}/>
        <Row label="Địa chỉ" value={profile.address || "—"}/>
        <Row label="Mã số thuế cá nhân" value={profile.taxCode || "—"}/>
        <Row label="Tham gia từ" value={when(profile.createdAt)}/>
        <Row label="Đăng nhập gần nhất" value={when(profile.lastLoginAt)}/>
      </section>
      {profile.businesses.length > 0 && <section className={ui.card} style={{ marginTop: 18 }}>
        <h2 style={{ fontSize: 16, marginBottom: 8, display: "flex", gap: 8, alignItems: "center" }}><Building2 size={18}/>Doanh nghiệp</h2>
        {profile.businesses.map(b => <div key={b.taxCode + b.legalName} style={{ padding: "8px 0", borderBottom: "1px solid var(--border)" }}>
          <strong>{b.legalName}</strong>
          <div className={s.muted}>Mã số thuế: {b.taxCode}{b.representative ? ` · Đại diện: ${b.representative}` : ""}</div>
          {b.address && <div className={s.muted}>{b.address}</div>}
        </div>)}
      </section>}
      <section className={ui.card} style={{ marginTop: 18 }}>
        <h2 style={{ fontSize: 16, marginBottom: 4, display: "flex", gap: 8, alignItems: "center" }}><Mail size={18}/>Đổi email</h2>
        <p className={s.muted} style={{ marginBottom: 14 }}>Link xem hợp đồng được gửi tới email này. Đổi email cần mật khẩu và mã gửi tới email mới; email cũ sẽ nhận thư báo.</p>
        <EmailForm key={round} current={profile.email} onSaved={saved => { setProfile(saved); setRound(round + 1); setNotice("Đã đổi email."); }}/>
      </section>
      <section className={ui.card} style={{ marginTop: 18, display: "flex", justifyContent: "space-between", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
        <span style={{ display: "flex", gap: 8, alignItems: "center" }}><KeyRound size={18}/>Mật khẩu</span>
        <Link href="/portal/change-password" className={ui.secondaryButton}>Đổi mật khẩu</Link>
      </section>
    </>}
  </div></>;
}
