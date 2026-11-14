"use client";
import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { CheckCircle2, ChevronDown, Clock, FileText, ListFilter, LogOut, MessageSquare, ShieldCheck, UserPlus } from "lucide-react";
import { screensFor, type NavItem } from "./internal-nav";
import { APP_NAME } from "@/lib/application-branding";
import { internalApi, InternalApiError, type InternalUser } from "@/services/internal-api";
import { InternalTheme, internalStyles as ui } from "./internal-theme";
import ChangePassword from "./change-password";
import { AdminNotificationBell } from "./notification-bell";
import s from "./workspace.module.css";
const UserContext = createContext<InternalUser | null>(null);
export const useInternalUser = () => useContext(UserContext)!;

export type SupportTicketTabKey = "assign" | "resolve" | "chat" | "history";

export default function InternalShell({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<InternalUser | null>(null);
  const [error, setError] = useState("");
  const [attempt, retry] = useState(0);
  const [supportMenuOpen, setSupportMenuOpen] = useState(true);
  const [activeTicketTab, setActiveTicketTab] = useState<SupportTicketTabKey>("assign");
  const router = useRouter();
  const path = usePathname();

  const isSupportPath = path.startsWith("/workspace/support-tickets");
  const ticketDetailMatch = path.match(/^\/workspace\/support-tickets\/(\d+)/);
  const currentTicketId = ticketDetailMatch ? ticketDetailMatch[1] : null;

  useEffect(() => {
    if (isSupportPath) {
      setSupportMenuOpen(true);
    }
  }, [isSupportPath]);

  useEffect(() => {
    const handler = (ev: Event) => {
      const detail = (ev as CustomEvent<{ tab?: SupportTicketTabKey }>).detail;
      if (detail?.tab) {
        setActiveTicketTab(detail.tab);
      }
    };
    window.addEventListener("support-ticket-tab-changed", handler);
    return () => window.removeEventListener("support-ticket-tab-changed", handler);
  }, []);

  const nav = (item: NavItem) =>
    (item.href === "/workspace" ? path === item.href : path.startsWith(item.href) && !(item.exclude && path.startsWith(item.exclude)))
      ? s.navActive
      : s.navLink;

  useEffect(() => {
    void internalApi<InternalUser>("/auth/me")
      .then((u) => {
        setUser(u);
        setActiveTicketTab(u.isManager ? "assign" : "resolve");
      })
      .catch((e) => {
        if (e instanceof InternalApiError && e.status === 401)
          router.replace(`/internal-login?next=${encodeURIComponent(path)}`);
        else setError(e.message);
      });
  }, [router, path, attempt]);

  async function logout() {
    try {
      await internalApi("/auth/logout", "POST");
      router.replace("/");
    } catch (e) {
      setError((e as Error).message);
    }
  }

  function handleSelectTicketSection(tab: SupportTicketTabKey) {
    setActiveTicketTab(tab);
    if (typeof window !== "undefined") {
      window.sessionStorage.setItem("contrack_support_ticket_tab", tab);
      window.dispatchEvent(new CustomEvent("support-ticket-select-tab", { detail: { tab } }));
    }
    if (currentTicketId) {
      router.push(`/workspace/support-tickets/${currentTicketId}?tab=${tab}`, { scroll: false });
    } else {
      router.push(`/workspace/support-tickets?tab=${tab}`);
    }
  }

  const supportSubItems: Array<{
    key: SupportTicketTabKey;
    label: string;
    icon: typeof UserPlus;
    managerOnly?: boolean;
  }> = [
    { key: "assign", label: "Phân công xử lý", icon: UserPlus, managerOnly: true },
    {
      key: "resolve",
      label: user?.isManager ? "Xử lý & Báo khách hàng" : "Báo cáo kết quả xử lý",
      icon: CheckCircle2,
    },
    { key: "chat", label: "Nhắn tin với Khách hàng", icon: MessageSquare, managerOnly: true },
    { key: "history", label: "Lịch sử & Thông tin", icon: Clock },
  ];

  return (
    <InternalTheme>
      {!user ? (
        <div className={s.empty}>
          {error || "Đang mở không gian làm việc…"}
          {error && (
            <button
              className={ui.secondaryButton}
              onClick={() => {
                setError("");
                retry(attempt + 1);
              }}
            >
              Thử lại
            </button>
          )}
        </div>
      ) : user.mustChangePassword ? (
        <ChangePassword user={user} onChanged={setUser} onLogout={logout} />
      ) : (
        <UserContext.Provider value={user}>
          <div className={s.shell}>
            <aside className={s.sidebar}>
              <div className={s.brand}>
                <span className={s.brandMark}>
                  <FileText size={21} />
                </span>
                {APP_NAME}
              </div>
              <nav>
                <span className={s.eyebrow} style={{ padding: "0 12px 8px" }}>
                  {user.department ?? "Không gian nội bộ"}
                </span>
                {screensFor(user.permissions).map((item) => {
                  const Icon = item.icon;
                  if (item.href === "/workspace/support-tickets") {
                    const visibleSections = supportSubItems.filter(
                      (sub) => !sub.managerOnly || user.isManager,
                    );
                    return (
                      <div key={item.label} style={{ display: "grid", gap: 4 }}>
                        <div
                          className={nav(item)}
                          style={{
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "space-between",
                            paddingRight: 8,
                            cursor: "pointer",
                          }}
                        >
                          <Link
                            href={item.href}
                            style={{
                              display: "flex",
                              alignItems: "center",
                              gap: 12,
                              color: "inherit",
                              textDecoration: "none",
                              flex: 1,
                              minWidth: 0,
                            }}
                            onClick={() => setSupportMenuOpen(true)}
                          >
                            <Icon size={19} />
                            <span style={{ overflow: "hidden", textOverflow: "ellipsis" }}>
                              {item.label}
                            </span>
                          </Link>
                          <button
                            type="button"
                            onClick={(e) => {
                              e.preventDefault();
                              e.stopPropagation();
                              setSupportMenuOpen((prev) => !prev);
                            }}
                            title="Mở/Đóng mục lục Hỗ trợ khách hàng"
                            style={{
                              border: 0,
                              background: "transparent",
                              color: "inherit",
                              cursor: "pointer",
                              display: "grid",
                              placeItems: "center",
                              padding: 4,
                              borderRadius: 6,
                            }}
                          >
                            <ChevronDown
                              size={16}
                              style={{
                                transform: supportMenuOpen ? "rotate(180deg)" : "rotate(0deg)",
                                transition: "transform 0.18s ease",
                              }}
                            />
                          </button>
                        </div>

                        {supportMenuOpen && (
                          <div
                            style={{
                              marginLeft: 14,
                              paddingLeft: 10,
                              borderLeft: "2px solid #d5e6e4",
                              display: "grid",
                              gap: 3,
                              marginTop: 2,
                              marginBottom: 4,
                            }}
                          >
                            <Link
                              href="/workspace/support-tickets"
                              style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 8,
                                padding: "8px 10px",
                                borderRadius: 7,
                                fontSize: 12.5,
                                fontWeight: path === "/workspace/support-tickets" ? 700 : 500,
                                color:
                                  path === "/workspace/support-tickets"
                                    ? "var(--primary)"
                                    : "var(--muted-foreground)",
                                background:
                                  path === "/workspace/support-tickets"
                                    ? "var(--secondary)"
                                    : "transparent",
                                textDecoration: "none",
                              }}
                            >
                              <ListFilter size={14} />
                              <span>Danh sách phiếu hỗ trợ</span>
                            </Link>

                            {visibleSections.map((sub) => {
                              const SubIcon = sub.icon;
                              const isSubActive =
                                Boolean(currentTicketId) && activeTicketTab === sub.key;
                              return (
                                <button
                                  key={sub.key}
                                  type="button"
                                  onClick={() => handleSelectTicketSection(sub.key)}
                                  style={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 8,
                                    width: "100%",
                                    textAlign: "left",
                                    padding: "8px 10px",
                                    borderRadius: 7,
                                    border: isSubActive
                                      ? "1px solid #99f6e4"
                                      : "1px solid transparent",
                                    fontSize: 12.5,
                                    fontWeight: isSubActive ? 700 : 500,
                                    color: isSubActive
                                      ? "#0f766e"
                                      : "var(--muted-foreground)",
                                    background: isSubActive ? "#f0fdfa" : "transparent",
                                    cursor: "pointer",
                                    transition: "all 0.15s ease",
                                  }}
                                >
                                  <SubIcon size={14} style={{ flexShrink: 0 }} />
                                  <span style={{ overflow: "hidden", textOverflow: "ellipsis" }}>
                                    {sub.label}
                                  </span>
                                </button>
                              );
                            })}
                          </div>
                        )}
                      </div>
                    );
                  }
                  return (
                    <Link key={item.label} href={item.href} className={nav(item)}>
                      <Icon size={19} />
                      {item.label}
                    </Link>
                  );
                })}
              </nav>
              <div className={s.account}>
                <ShieldCheck size={22} color="var(--primary)" />
                <strong style={{ fontSize: 13 }}>{user.name}</strong>
                <span className={s.muted}>
                  {user.isManager ? "Trưởng phòng" : "Nhân viên"}
                  {user.department ? ` · ${user.department}` : ""}
                </span>
              </div>
            </aside>
            <div className={s.main}>
              <header className={s.header}>
                <span style={{ fontWeight: 600, fontSize: 14 }}>
                  Quản lý hợp đồng <span className={s.muted}> / Không gian nội bộ</span>
                </span>
                <div style={{ display: "flex", gap: 14, alignItems: "center" }}>
                  {user.permissions.some((p) => p.startsWith("ticket.")) && (
                    <AdminNotificationBell />
                  )}
                  <span className={s.muted}>{user.name}</span>
                  <button
                    data-leave-workspace
                    className={s.iconButton}
                    onClick={logout}
                    title="Đăng xuất"
                    aria-label="Đăng xuất"
                  >
                    <LogOut size={18} />
                  </button>
                </div>
              </header>
              {error && (
                <div role="alert" className={ui.error}>
                  {error}
                </div>
              )}
              {children}
            </div>
          </div>
        </UserContext.Provider>
      )}
    </InternalTheme>
  );
}
