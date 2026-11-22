"use client";
import { ThemeToggle } from "@/components/theme-toggle";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { FileScan, FileText, Files, KeyRound, LifeBuoy, LogOut, UserRound } from "lucide-react";
import { APP_NAME } from "@/lib/application-branding";
import s from "@/components/internal/workspace.module.css";
import { customerApi } from "@/services/customer-api";
import { CustomerNotificationBell } from "./customer-notification-bell";

/** With onLogout the customer came from an emailed link: that session has no contract list or password page. */
export default function PortalHeader({ name, onLogout, mustChangePassword = false }: { name?: string; onLogout?: () => void; mustChangePassword?: boolean }) {
  const router = useRouter(); const path = usePathname();
  const fullAccess = !onLogout && !mustChangePassword;
  const nav = (href: string) => path.startsWith(href) ? s.navActive : s.navLink;
  async function logout() { try { await customerApi("/auth/logout", "POST"); } finally { if (onLogout) onLogout(); else router.replace("/portal/login"); } }
  return <header className={s.header}>
    {!fullAccess ? <span className={s.brand} style={{ fontSize: 15 }}><span className={s.brandMark}><FileText size={18}/></span>{APP_NAME}</span>
      : <><Link href="/portal/contracts" className={s.brand} style={{ fontSize: 15 }}><span className={s.brandMark}><FileText size={18}/></span>{APP_NAME}</Link>
        <nav className={s.portalNav} aria-label="Cổng khách hàng"><Link href="/portal/contracts" className={nav("/portal/contracts")} aria-current={path.startsWith("/portal/contracts") ? "page" : undefined}><Files size={17}/>Hợp đồng của tôi</Link>
          <Link href="/portal/support" className={nav("/portal/support")} aria-current={path.startsWith("/portal/support") ? "page" : undefined}><LifeBuoy size={17}/>Hỗ trợ</Link>
          <Link href="/portal/scans" className={nav("/portal/scans")} aria-current={path.startsWith("/portal/scans") ? "page" : undefined}><FileScan size={17}/>Bản scan</Link></nav></>}
    <div style={{ display: "flex", gap: 10, alignItems: "center" }}><ThemeToggle/>{fullAccess && <CustomerNotificationBell/>}{name && (onLogout ? <span className={s.muted}>{name}</span>
        : <Link href="/portal/profile" className={s.muted} title="Hồ sơ của tôi" style={{ display: "flex", alignItems: "center", gap: 6 }}><UserRound size={16}/>{name}</Link>)}
      {fullAccess && <Link href="/portal/change-password" className={s.iconButton} title="Đổi mật khẩu" aria-label="Đổi mật khẩu"><KeyRound size={18}/></Link>}
      <button className={s.iconButton} onClick={logout} title="Đăng xuất" aria-label="Đăng xuất"><LogOut size={18}/></button></div>
  </header>;
}
