"use client";
import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { FileText, Files, LogOut, ShieldCheck, Archive, Activity, FileSignature } from "lucide-react";
import { usePathname } from "next/navigation";
import { APP_NAME } from "@/lib/application-branding";
import { internalApi, InternalApiError, type InternalUser } from "@/services/internal-api";
import { InternalTheme, internalStyles as ui } from "./internal-theme";
import s from "./workspace.module.css";
const UserContext = createContext<InternalUser | null>(null);
export const useInternalUser = () => useContext(UserContext)!;
export default function InternalShell({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<InternalUser | null>(null); const [error, setError] = useState(""); const [attempt, retry] = useState(0); const router = useRouter(); const pathname = usePathname();
  useEffect(() => { void internalApi<InternalUser>("/auth/me").then(setUser).catch(e => {
    if (e instanceof InternalApiError && e.status === 401) router.replace(`/internal-login?next=${encodeURIComponent(pathname)}`); else setError(e.message);
  }); }, [router, pathname, attempt]);
  async function logout() { try { await internalApi("/auth/logout", "POST"); router.replace("/"); } catch (e) { setError((e as Error).message); } }
  return <InternalTheme>{!user ? <div className={s.empty}>{error || "Đang mở không gian làm việc…"}{error && <button className={ui.secondaryButton} onClick={() => { setError(""); retry(attempt + 1); }}>Thử lại</button>}</div> : <UserContext.Provider value={user}>
    <div className={s.shell}><aside className={s.sidebar}><div className={s.brand}><span className={s.brandMark}><FileText size={21}/></span>{APP_NAME}</div>
      <nav><span className={s.eyebrow} style={{ padding: "0 12px 8px" }}>Không gian nội bộ</span><Link href="/workspace/contracts/drafts" className={pathname.startsWith("/workspace/contracts/drafts") ? s.navActive : s.navLink}><Files size={19}/>Hợp đồng nháp</Link>{user.canManageAll && <><Link href="/workspace/contracts/company-signing" className={pathname.startsWith("/workspace/contracts/company-signing") ? s.navActive : s.navLink}><FileSignature size={19}/>Ký phía công ty</Link><Link href="/workspace/contracts/signing" className={pathname.startsWith("/workspace/contracts/signing") ? s.navActive : s.navLink}><Activity size={19}/>Theo dõi ký</Link><Link href="/workspace/contracts/archive" className={pathname === "/workspace/contracts/archive" ? s.navActive : s.navLink}><Archive size={19}/>Kho hợp đồng</Link></>}</nav>
      <div className={s.account}><ShieldCheck size={22} color="var(--primary)"/><strong style={{ fontSize: 13 }}>{user.name}</strong><span className={s.muted}>{user.canManageAll ? "Quản lý nháp toàn công ty" : "Nháp do bạn phụ trách"}</span></div>
    </aside><div className={s.main}><header className={s.header}><span style={{ fontWeight: 600, fontSize: 14 }}>Quản lý hợp đồng <span className={s.muted}> / Không gian nội bộ</span></span><div style={{ display: "flex", gap: 14, alignItems: "center" }}><span className={s.muted}>{user.name}</span><button data-leave-workspace className={s.iconButton} onClick={logout} title="Đăng xuất" aria-label="Đăng xuất"><LogOut size={18}/></button></div></header>
      {error && <div role="alert" className={ui.error}>{error}</div>}{children}</div></div>
  </UserContext.Provider>}</InternalTheme>;
}
