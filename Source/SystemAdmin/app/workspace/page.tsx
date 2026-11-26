"use client";
import { useEffect, useState } from "react";
import { ChevronRight } from "lucide-react";
import { dateTime, internalApi } from "@/services/internal-api";
import { stages } from "@/components/internal/progress-list";
import Link from "next/link";
import { useInternalUser } from "@/components/internal/internal-shell";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import { groupedOverviewRole, isSalesWorkspace, screensFor } from "@/components/internal/internal-nav";
import s from "@/components/internal/workspace.module.css";

const overviewGroups = {
  office: [
    { title: "Hợp đồng", paths: ["contracts", "signed-contracts", "scanned-contracts", "signed-contracts/search"] },
    { title: "Văn bản & hỗ trợ", paths: ["templates", "scan-emails", "support-tickets"] },
    { title: "Khóa", paths: ["my-key", "encryption-keys"] },
  ],
  director: [
    { title: "Hợp đồng & khách hàng", paths: ["contracts", "customers"] },
    { title: "Nhân sự", paths: ["admin/employees", "admin/departments"] },
    { title: "Hệ thống", paths: ["scan-emails", "encryption-keys"] },
  ],
};
/** Tổng quan: the screens this employee's department and position open, until a real dashboard moves to MASTER. */
export default function WorkspaceHome() {
  const user = useInternalUser();
  if (isSalesWorkspace(user.permissions)) return <SalesOverview key={user.id} userId={user.id} name={user.name} department={user.department} permissions={user.permissions}/>;
  const screens = screensFor(user.permissions).filter(item => item.href !== "/workspace");
  const role = groupedOverviewRole(user.permissions);
  if (role) {
    return <div className={`${s.page} ${s.groupedOverview}`}>
      <div className={s.titleRow}><div><span className={s.eyebrow}>{user.department ?? (role === "office" ? "Phòng Hành chính - Pháp chế" : "Ban Giám đốc")}</span><h1>Xin chào, {user.name}</h1></div></div>
      <div className={s.overviewGroups}>
        {overviewGroups[role].map(({ title, paths }) => {
          const links = paths.flatMap(path => screens.filter(item => item.href === `/workspace/${path}`));
          if (!links.length) return null;
          const wide = role === "office" && title === "Hợp đồng";
          return <section key={title} className={`${ui.card} ${s.overviewGroup} ${wide ? s.overviewWide : ""}`}>
            <h2>{title}</h2>
            <nav aria-label={title} className={s.overviewTiles}>
              {links.map(({ href, icon: Icon, label }) => <Link key={href} href={href} className={`${s.navActive} ${s.overviewTile}`}><Icon size={24} aria-hidden="true"/><span>{label}</span></Link>)}
            </nav>
          </section>;
        })}
      </div>
    </div>;
  }
  return <div className={s.page}><div className={s.titleRow}><div><span className={s.eyebrow}>{user.department ?? "Không gian nội bộ"}</span><h1>Xin chào, {user.name}</h1>
    <p className={s.muted}>{user.isManager ? "Quản lý" : "Nhân viên"}{user.department ? ` · ${user.department}` : ""}. Đây là các màn hình bạn được sử dụng.</p></div></div>
    <section className={ui.card}><div style={{ display: "grid", gap: 12, gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))" }}>
      {screens.map(({ label, icon: Icon, href }) => <Link key={label} href={href} className={s.navActive}><Icon size={19}/>{label}</Link>)}
    </div>{screens.length === 0 && <div className={s.empty}><strong>Tài khoản chưa được mở màn hình nào</strong><p>Liên hệ Ban Giám Đốc để được xếp phòng ban.</p></div>}</section>
  </div>;
}

type RecentContract = { id: string; name: string; code: string; customer: string; stage: string; createdAt: string | null };

function SalesOverview({ userId, name, department, permissions }: { userId: string; name: string; department: string | null; permissions: string[] }) {
  const [contracts, setContracts] = useState<RecentContract[] | null>(null);
  const [error, setError] = useState("");
  useEffect(() => {
    let active = true;
    const query = new URLSearchParams({ recent: "true", ownerId: userId });
    void internalApi<{ items: RecentContract[] }>(`/contract-progress?${query}`).then(
      result => { if (active) setContracts(result.items); },
      failure => { if (active) setError(failure.message); },
    );
    return () => { active = false; };
  }, [userId]);

  const screens = screensFor(permissions);
  const destinations = ["/workspace/contracts", "/workspace/customers", "/workspace/support-tickets", "/workspace/catalog"];
  return <div className={s.page}>
    <div className={s.titleRow}><div><span className={s.eyebrow}>{department ?? "Phòng Kinh doanh"}</span><h1>Xin chào, {name}</h1></div></div>
    <nav className={s.salesShortcuts} aria-label="Lối tắt Tổng quan">
      {destinations.map(href => {
        const screen = screens.find(item => item.href === href);
        if (!screen) return null;
        const Icon = screen.icon;
        return <Link key={href} href={href} className={`${s.navActive} ${s.salesShortcut}`}>
          <Icon size={19} aria-hidden="true"/><span>{screen.label}</span><ChevronRight size={16} aria-hidden="true"/>
        </Link>;
      })}
    </nav>
    <section className={ui.card} aria-labelledby="recent-contracts-heading">
      <div className={`${s.toolbar} ${s.recentContractsHeading}`}>
        <h2 id="recent-contracts-heading">Hợp đồng tạo gần đây</h2>
        <Link href="/workspace/contracts/drafts" className={ui.secondaryButton}>Xem tất cả</Link>
      </div>
      {error ? <p role="alert" className={ui.error}>{error}</p> : contracts === null ? <p role="status" className={s.empty}>Đang tải hợp đồng…</p>
        : contracts.length === 0 ? <p className={s.empty}>Chưa có hợp đồng để hiển thị.</p>
        : <div className={s.tableWrap}><table className={s.table}>
          <thead><tr><th scope="col">Hợp đồng</th><th scope="col">Khách hàng</th><th scope="col">Trạng thái</th><th scope="col">Ngày tạo</th><th scope="col"><span className="sr-only">Thao tác</span></th></tr></thead>
          <tbody>{contracts.map(contract => <tr key={contract.id}>
            <td><strong>{contract.name}</strong>{contract.code && <div className={s.recentContractCode}>{contract.code}</div>}</td>
            <td>{contract.customer}</td><td><span className={s.badge}>{stages[contract.stage] ?? contract.stage}</span></td>
            <td>{contract.createdAt ? dateTime(contract.createdAt) : "—"}</td>
            <td><Link href={`/workspace/contracts/drafts/${contract.id}`} className={ui.secondaryButton} aria-label={`Mở ${contract.name}`}>Mở</Link></td>
          </tr>)}</tbody>
        </table></div>}
    </section>
  </div>;
}
