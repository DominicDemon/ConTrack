"use client";
import { ListPager } from "@/components/scanned-contracts/list-pager";
import ContractTabs from "@/components/internal/contract-tabs";
import { SortHeader, useListView } from "@/components/internal/list-tools";
import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { Activity, AlertTriangle, Check, Clock3, FilePlus2 } from "lucide-react";
import { useInternalUser } from "@/components/internal/internal-shell";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import { internalApi, dateTime, documentStatusLabel, partyTypeLabel, type SigningMonitorEntry } from "@/services/internal-api";

const tabs = [
  { id: "all", label: "Tất cả" },
  { id: "waiting", label: "Đang chờ ký" },
  { id: "signed", label: "Đã có chữ ký" },
  { id: "untracked", label: "Thiếu dữ liệu ký" },
] as const;
export default function ContractSigningMonitorPage() {
  const user = useInternalUser();
  const [rows, setRows] = useState<SigningMonitorEntry[]>([]);
  const [search, setSearch] = useState("");
  const [party, setParty] = useState("");
  const [tab, setTab] = useState<(typeof tabs)[number]["id"]>("all");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(true);
  useEffect(() => {
    if (!user.canManageAll) return;
    let active = true;
    const timer = setTimeout(() => {
      setBusy(true); setError("");
      const params = new URLSearchParams();
      if (search.trim()) params.set("search", search.trim());
      if (party) params.set("partyType", party);
      void internalApi<SigningMonitorEntry[]>(`/contract-signing-monitor?${params}`)
        .then(result => { if (active) setRows(result); })
        .catch(e => { if (active) setError(e.message); })
        .finally(() => { if (active) setBusy(false); });
    }, 200);
    return () => { active = false; clearTimeout(timer); };
  }, [user.canManageAll, search, party]);

  const counts = useMemo(() => ({
    all: rows.length,
    waiting: rows.filter(x => x.status === 3).length,
    signed: rows.filter(x => x.signedCount > 0).length,
    untracked: rows.filter(x => !x.paper && [3, 4, 5].includes(x.status) && x.signatureCount === 0).length,
  }), [rows]);
  // Search runs on the server (up to 200 contracts); the step filter, sort and pages on this list.
  const list = useListView(rows, { search: "", fields: () => [], filterKey: tab,
    filter: row => tab === "all" || (tab === "waiting" && row.status === 3) || (tab === "signed" && row.signedCount > 0)
      || (tab === "untracked" && !row.paper && [3, 4, 5].includes(row.status) && row.signatureCount === 0),
    sorts: { name: r => r.name, customer: r => r.customerName, progress: r => r.signatureCount ? r.signedCount / r.signatureCount : -1, status: r => r.status, updated: r => r.lastSignedAt ?? r.updatedAt },
    initial: { key: "updated", dir: "desc" } });

  if (!user.canManageAll) return <div className={s.page}><p role="alert" className={ui.error}>Theo dõi ký chỉ dành cho quản trị viên công ty.</p></div>;

  return <div className={s.page} data-ui="signing-monitor"><ContractTabs/>
    <div className={s.titleRow}><div><span className={s.eyebrow}>Vận hành hợp đồng</span><h1>Theo dõi ký hợp đồng</h1><p className={s.muted}>Xem tiến độ của từng hồ sơ, chữ ký đã ghi nhận và các bước đang chờ.</p></div>
      <div className={s.actions}><Link href="/workspace/contracts/drafts/new" className={ui.primaryButton}><FilePlus2 size={18}/>Tạo hợp đồng</Link></div></div>
    <div className={s.signingMetrics} aria-label="Tổng quan tiến độ ký">
      <div><strong>{counts.waiting}</strong><span>Chờ ký</span></div>
      <div><strong>{counts.signed}</strong><span>Đã có chữ ký</span></div>
      <div className={counts.untracked ? s.signingMetricAlert : ""}><strong>{counts.untracked}</strong><span>Thiếu dữ liệu chữ ký điện tử</span></div>
      <div><strong>{counts.all}</strong><span>Hồ sơ đang theo dõi</span></div>
    </div>
    {error && <p role="alert" className={ui.error} style={{ marginBottom: 16 }}>{error}</p>}
    <section className={ui.card}><div className={s.toolbar}>
      <input aria-label="Tìm hợp đồng" className={ui.field} placeholder="Tìm mã, tên hợp đồng hoặc khách hàng…" value={search} onChange={e => setSearch(e.target.value)}/>
      <select aria-label="Lọc tiến độ ký" className={ui.field} style={{ maxWidth: 240 }} value={tab} onChange={e => setTab(e.target.value as typeof tab)}>
        {tabs.map(item => <option key={item.id} value={item.id}>{item.label} ({counts[item.id]})</option>)}</select>
      <select aria-label="Lọc loại khách hàng" className={ui.field} style={{ maxWidth: 240 }} value={party} onChange={e => setParty(e.target.value)}><option value="">Tất cả loại khách hàng</option>{Object.entries(partyTypeLabel).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></div>
      {busy ? <div className={s.empty}>Đang tải tiến độ ký…</div> : list.total === 0 ? <div className={s.empty}><Activity size={42} strokeWidth={1.3}/>
        <strong>{rows.length ? "Không có hồ sơ phù hợp" : "Chưa có hồ sơ để theo dõi"}</strong><p>{rows.length ? "Thử đổi từ khóa hoặc bộ lọc." : "Tạo một hợp đồng để bắt đầu luồng ký."}</p></div>
      : <div className={s.tableWrap}><table className={s.table}><thead><tr><SortHeader label="Hợp đồng" column="name" sort={list.sort} onSort={list.sortBy}/><SortHeader label="Khách hàng · Phụ trách" column="customer" sort={list.sort} onSort={list.sortBy}/><SortHeader label="Tiến độ ký" column="progress" sort={list.sort} onSort={list.sortBy} firstDir="desc"/><SortHeader label="Trạng thái" column="status" sort={list.sort} onSort={list.sortBy}/><SortHeader label="Cập nhật" column="updated" sort={list.sort} onSort={list.sortBy} firstDir="desc"/><th><span className="sr-only">Mở</span></th></tr></thead>
        <tbody>{list.rows.map(row => { const untracked = !row.paper && [3, 4, 5].includes(row.status) && row.signatureCount === 0;
          return <tr key={row.id}>
            <td><Link href={`/workspace/contracts/signing/${row.id}`}><strong>{row.name}</strong></Link><div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{row.code || `#${row.id}`}</div></td>
            <td>{row.customerName}<div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>Phụ trách {row.employeeName}</div></td>
            <td>{row.paper ? <span className={[4, 5].includes(row.status) ? s.signingStepDone : row.status === 3 ? s.signingStepCurrent : s.signingStepWait}>{[4, 5].includes(row.status) ? <Check size={13}/> : <Clock3 size={13}/>} Ký giấy, đóng dấu</span>
              : <div className={s.signingProgress} aria-label={`${row.signedCount} trên ${row.signatureCount} bên đã ký`}>
                <span className={row.companySigned ? s.signingStepDone : row.status === 3 && !row.customerSigned ? s.signingStepCurrent : s.signingStepWait}>{row.companySigned ? <Check size={13}/> : <Clock3 size={13}/>} Công ty</span>
                <span className={s.signingConnector}/>
                <span className={row.customerSigned ? s.signingStepDone : row.companySigned ? s.signingStepCurrent : s.signingStepWait}>{row.customerSigned ? <Check size={13}/> : <Clock3 size={13}/>} Khách hàng</span></div>}</td>
            <td>{untracked ? <span className={`${s.badge} ${s.warning}`}><AlertTriangle size={13}/> Thiếu dữ liệu ký</span> : <span className={s.badge}>{documentStatusLabel[row.status] ?? `Trạng thái ${row.status}`}</span>}</td>
            <td className={s.muted}>{dateTime(row.lastSignedAt ?? row.updatedAt)}</td>
            <td><div style={{ display: "flex", justifyContent: "flex-end" }}><Link className={ui.secondaryButton} href={`/workspace/contracts/signing/${row.id}`}>Xem</Link></div></td></tr>; })}</tbody></table></div>}
      {!busy && <ListPager page={list.page} pageSize={list.pageSize} total={list.total} onPage={list.setPage} label="hợp đồng"/>}
    </section>
    <p className={s.muted} style={{ marginTop: 14 }}>Danh sách lấy tối đa 200 hồ sơ mới nhất. Hồ sơ chờ ký nhưng chưa có bản ghi chữ ký được đánh dấu riêng để dễ phát hiện.</p>
  </div>;
}
