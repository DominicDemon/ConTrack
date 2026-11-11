"use client";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { FileText, Files, KeyRound, LifeBuoy, LogOut } from "lucide-react";
import { APP_NAME } from "@/lib/application-branding";
import s from "@/components/internal/workspace.module.css";
import { customerApi } from "@/services/customer-api";

/** With onLogout the customer came from an emailed link: that session has no contract list or password page. */
export default function PortalHeader({ name, onLogout }: { name?: string; onLogout?: () => void }) {
  const router = useRouter(); const path = usePathname();
  const nav = (href: string) => path.startsWith(href) ? s.navActive : s.navLink;
  async function logout() { try { await customerApi("/auth/logout", "POST"); } finally { if (onLogout) onLogout(); else router.replace("/portal/login"); } }
  return <header className={s.header}>
    {onLogout ? <span className={s.brand} style={{ fontSize: 15 }}><span className={s.brandMark}><FileText size={18}/></span>{APP_NAME}</span>
      : <><Link href="/portal/contracts" className={s.brand} style={{ fontSize: 15 }}><span className={s.brandMark}><FileText size={18}/></span>{APP_NAME}</Link>
        <nav className={s.portalNav} aria-label="Cổng khách hàng"><Link href="/portal/contracts" className={nav("/portal/contracts")} aria-current={path.startsWith("/portal/contracts") ? "page" : undefined}><Files size={17}/>Hợp đồng của tôi</Link>
          <Link href="/portal/support" className={nav("/portal/support")} aria-current={path.startsWith("/portal/support") ? "page" : undefined}><LifeBuoy size={17}/>Hỗ trợ</Link></nav></>}
    <div style={{ display: "flex", gap: 10, alignItems: "center" }}>{name && <span className={s.muted}>{name}</span>}
      {!onLogout && <Link href="/portal/change-password" className={s.iconButton} title="Đổi mật khẩu" aria-label="Đổi mật khẩu"><KeyRound size={18}/></Link>}
      <button className={s.iconButton} onClick={logout} title="Đăng xuất" aria-label="Đăng xuất"><LogOut size={18}/></button></div>
  </header>;
}
