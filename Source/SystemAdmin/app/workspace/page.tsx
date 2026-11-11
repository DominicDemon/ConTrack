"use client";
import Link from "next/link";
import { useInternalUser } from "@/components/internal/internal-shell";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import { screensFor } from "@/components/internal/internal-nav";
import s from "@/components/internal/workspace.module.css";

/** Tổng quan: the screens this employee's department and position open, until a real dashboard moves to MASTER. */
export default function WorkspaceHome() {
  const user = useInternalUser();
  const screens = screensFor(user.permissions).filter(item => item.href !== "/workspace");
  return <div className={s.page}><div className={s.titleRow}><div><span className={s.eyebrow}>{user.department ?? "Không gian nội bộ"}</span><h1>Xin chào, {user.name}</h1>
    <p className={s.muted}>{user.isManager ? "Quản lý" : "Nhân viên"}{user.department ? ` · ${user.department}` : ""}. Đây là các màn hình bạn được sử dụng.</p></div></div>
    <section className={ui.card}><div style={{ display: "grid", gap: 12, gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))" }}>
      {screens.map(({ label, icon: Icon, href }) => href
        ? <Link key={label} href={href} className={s.navActive}><Icon size={19}/>{label}</Link>
        : <span key={label} className={s.navSoon} style={{ display: "flex" }} aria-disabled="true"><Icon size={19}/>{label}<span className={s.soonTag}>Sắp có</span></span>)}
    </div>{screens.length === 0 && <div className={s.empty}><strong>Tài khoản chưa được mở màn hình nào</strong><p>Liên hệ Ban Giám Đốc để được xếp phòng ban.</p></div>}</section>
    <p className={s.muted} style={{ marginTop: 14 }}>Các màn “Sắp có” đang được chuyển sang hệ thống mới và sẽ mở dần.</p>
  </div>;
}
