"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import { CalendarClock } from "lucide-react";
import { useInternalUser } from "@/components/internal/internal-shell";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import { screensFor } from "@/components/internal/internal-nav";
import { internalApi } from "@/services/internal-api";
import s from "@/components/internal/workspace.module.css";

type ExpiringContract = { kind: "CONTRACT_DOCUMENT" | "CONTRACT"; id: string; code: string; name: string; expireDate: string; daysLeft: number; employeeName: string | null };

/** Contracts that end within 30 days; the same ones get reminder emails 30 and 7 days before. */
function ExpiringContracts({ showOwner }: { showOwner: boolean }) {
  const [items, setItems] = useState<ExpiringContract[] | null>(null);
  const [error, setError] = useState("");
  useEffect(() => { internalApi<ExpiringContract[]>("/contracts/expiring").then(setItems, e => setError((e as Error).message)); }, []);
  if (error) return <section className={ui.card}><p role="alert" className={ui.error}>Không tải được hợp đồng sắp hết hạn: {error}</p></section>;
  if (!items?.length) return null;
  return <section className={ui.card} aria-labelledby="expiring-title"><h2 id="expiring-title" style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 16, marginBottom: 10 }}><CalendarClock size={18}/>Hợp đồng sắp hết hạn (30 ngày tới)</h2>
    <ul style={{ display: "grid", gap: 8, listStyle: "none", padding: 0, margin: 0 }}>{items.map(c => <li key={`${c.kind}-${c.id}`} style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 10, justifyContent: "space-between" }}>
      <Link href={c.kind === "CONTRACT_DOCUMENT" ? `/workspace/contracts/drafts/${c.id}` : "/workspace/scanned-contracts"}><strong>{c.code}</strong> · {c.name}</Link>
      <span style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 8 }}>
        {showOwner && c.employeeName && <span className={s.muted}>{c.employeeName}</span>}
        <span className={s.muted}>Hết hạn {c.expireDate.split("-").reverse().join("/")}</span>
        <span className={`${s.badge} ${c.daysLeft <= 7 ? s.warning : ""}`}>{c.daysLeft === 0 ? "Hết hạn hôm nay" : `Còn ${c.daysLeft} ngày`}</span>
      </span></li>)}</ul>
  </section>;
}

/** Tổng quan: the screens this employee's department and position open, until a real dashboard moves to MASTER. */
export default function WorkspaceHome() {
  const user = useInternalUser();
  const screens = screensFor(user.permissions).filter(item => item.href !== "/workspace");
  return <div className={s.page}><div className={s.titleRow}><div><span className={s.eyebrow}>{user.department ?? "Không gian nội bộ"}</span><h1>Xin chào, {user.name}</h1>
    <p className={s.muted}>{user.isManager ? "Quản lý" : "Nhân viên"}{user.department ? ` · ${user.department}` : ""}. Đây là các màn hình bạn được sử dụng.</p></div></div>
    <ExpiringContracts showOwner={user.canManageAll}/>
    <section className={ui.card}><div style={{ display: "grid", gap: 12, gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))" }}>
      {screens.map(({ label, icon: Icon, href }) => <Link key={label} href={href} className={s.navActive}><Icon size={19}/>{label}</Link>)}
    </div>{screens.length === 0 && <div className={s.empty}><strong>Tài khoản chưa được mở màn hình nào</strong><p>Liên hệ Ban Giám Đốc để được xếp phòng ban.</p></div>}</section>
  </div>;
}
