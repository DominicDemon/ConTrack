"use client";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { FileText, KeyRound, LogOut } from "lucide-react";
import { APP_NAME } from "@/lib/application-branding";
import s from "@/components/internal/workspace.module.css";
import { customerApi } from "@/services/customer-api";

/** With onLogout the customer came from an emailed link: that session has no contract list or password page. */
export default function PortalHeader({ name, onLogout }: { name?: string; onLogout?: () => void }) {
  const router = useRouter();
  async function logout() { try { await customerApi("/auth/logout", "POST"); } finally { if (onLogout) onLogout(); else router.replace("/portal/login"); } }
  return <header className={s.header}>
    {onLogout ? <span className={s.brand} style={{ fontSize: 15 }}><span className={s.brandMark}><FileText size={18}/></span>{APP_NAME}</span>
      : <Link href="/portal/contracts" className={s.brand} style={{ fontSize: 15 }}><span className={s.brandMark}><FileText size={18}/></span>{APP_NAME}</Link>}
    <div style={{ display: "flex", gap: 10, alignItems: "center" }}>{name && <span className={s.muted}>{name}</span>}
      {!onLogout && <Link href="/portal/change-password" className={s.iconButton} title="Đổi mật khẩu" aria-label="Đổi mật khẩu"><KeyRound size={18}/></Link>}
      <button className={s.iconButton} onClick={logout} title="Đăng xuất" aria-label="Đăng xuất"><LogOut size={18}/></button></div>
  </header>;
}
