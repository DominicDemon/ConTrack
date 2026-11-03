"use client";
import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { FileText, Files, LogOut, ShieldCheck } from "lucide-react";
import { APP_NAME } from "@/lib/application-branding";
import { internalApi, InternalApiError, type InternalUser } from "@/services/internal-api";
import { InternalTheme, internalStyles as ui } from "./internal-theme";
import s from "./workspace.module.css";
const UserContext = createContext<InternalUser | null>(null);
export const useInternalUser = () => useContext(UserContext)!;
export default function InternalShell({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<InternalUser | null>(null); const [error, setError] = useState(""); const [attempt, retry] = useState(0); const router = useRouter();
  useEffect(() => { void internalApi<InternalUser>("/auth/me").then(setUser).catch(e => {
    if (e instanceof InternalApiError && e.status === 401) router.replace("/"); else setError(e.message);
  }); }, [router, attempt]);
  async function logout() { try { await internalApi("/auth/logout", "POST"); router.replace("/"); } catch (e) { setError((e as Error).message); } }
  return <InternalTheme>{!user ? <div className={s.empty}>{error || "Đang mở không gian làm việc…"}{error && <button className={ui.secondaryButton} onClick={() => { setError(""); retry(attempt + 1); }}>Thử lại</button>}</div> : <UserContext.Provider value={user}>
    <div className={s.shell}><aside className={s.sidebar}><div className={s.brand}><span className={s.brandMark}><FileText size={21}/></span>{APP_NAME}</div>
      <nav><span className={s.eyebrow} style={{ padding: "0 12px 8px" }}>Không gian nội bộ</span><Link href="/workspace/contracts/drafts" className={s.navActive}><Files size={19}/>Hợp đồng nháp</Link></nav>
      <div className={s.account}><ShieldCheck size={22} color="#007f83"/><strong style={{ fontSize: 13 }}>{user.name}</strong><span className={s.muted}>{user.canManageAll ? "Quản lý nháp toàn công ty" : "Nháp do bạn phụ trách"}</span></div>
    </aside><div className={s.main}><header className={s.header}><span style={{ fontWeight: 600, fontSize: 14 }}>Quản lý hợp đồng <span className={s.muted}> / Không gian nội bộ</span></span><div style={{ display: "flex", gap: 14, alignItems: "center" }}><span className={s.muted}>{user.name}</span><button data-leave-workspace className={s.iconButton} onClick={logout} title="Đăng xuất" aria-label="Đăng xuất"><LogOut size={18}/></button></div></header>
      {error && <div role="alert" className={ui.error}>{error}</div>}{children}</div></div>
  </UserContext.Provider>}</InternalTheme>;
}
