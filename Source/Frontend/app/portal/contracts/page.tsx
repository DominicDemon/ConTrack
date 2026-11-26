"use client";
import { ListPager } from "@/components/internal/contract-list-pager";
import { SortHeader, useListView } from "@/components/internal/list-tools";
import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowUpRight, Files } from "lucide-react";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import PortalHeader from "@/components/portal/portal-header";
import { dateTime } from "@/services/internal-api";
import { customerApi, customerStatusLabel, documentTypeLabel, handleAuthError, isOpenReview, type CustomerReviewSummary, type CustomerSession } from "@/services/customer-api";

const filters = [["all", "Tất cả"], ["signed", "Đã ký"], ["unsigned", "Chưa ký"]] as const;

export default function CustomerContracts() {
  const router = useRouter();
  const [search, setSearch] = useState(""); const [kind, setKind] = useState("");
  const [session, setSession] = useState<CustomerSession | null>(null);
  const [rows, setRows] = useState<CustomerReviewSummary[] | null>(null); const [error, setError] = useState("");
  const [filter, setFilter] = useState<(typeof filters)[number][0]>("all");
  // Waiting for the customer first, as before; then newest. The headings sort by a column.
  const list = useListView(rows ?? [], { search, fields: r => [r.contractName, r.parentName, r.documentTypeName], filterKey: [filter, kind],
    filter: r => (filter === "all" || Boolean(r.isSigned) === (filter === "signed")) && (!kind || (kind === "CONTRACT") === !r.parentName),
    sorts: { waiting: r => !r.isSigned && isOpenReview(r.status) ? 0 : 1, name: r => r.contractName, version: r => Number(r.versionNo), status: r => r.isSigned ? "~" : r.status, sent: r => r.sentAt },
    initial: { key: "waiting", dir: "asc" } });
  const visibleRows = rows ? list.rows : null;
  useEffect(() => {
    let active = true;
    let loading = false;
    async function load() {
      if (loading) return;
      loading = true;
      try {
        const [me, list] = await Promise.all([customerApi<CustomerSession>("/auth/me"), customerApi<CustomerReviewSummary[]>("/reviews")]);
        if (active) { setSession(me); setRows(list); setError(""); }
      } catch (e) {
        if (active && handleAuthError(e, router.replace)) setError((e as Error).message);
      } finally { loading = false; }
    }
    const onFocus = () => { void load(); };
    const onPageShow = (event: PageTransitionEvent) => { if (event.persisted) void load(); };
    void load();
    window.addEventListener("focus", onFocus);
    window.addEventListener("pageshow", onPageShow);
    return () => {
      active = false;
      window.removeEventListener("focus", onFocus);
      window.removeEventListener("pageshow", onPageShow);
    };
  }, [router]);
  return <><PortalHeader name={session?.name}/><div className={s.page}>
    <span className={s.eyebrow}>Cổng khách hàng</span><h1>Hợp đồng của bạn</h1><p className={s.muted}>Hợp đồng đang chờ bạn phản hồi được xếp lên đầu.</p>
    {error && <p role="alert" className={ui.error} style={{ margin: "16px 0" }}>{error}</p>}
    <section className={ui.card} style={{ marginTop: 18 }}>
      <div className={s.toolbar} role="group" aria-label="Lọc theo tình trạng ký">
        {filters.map(([value, label]) => <button key={value} type="button" aria-pressed={filter === value}
          className={filter === value ? ui.primaryButton : ui.secondaryButton} onClick={() => setFilter(value)}>{label}</button>)}
        <input aria-label="Tìm hợp đồng" className={ui.field} placeholder="Tìm tên hợp đồng hoặc văn bản đi kèm…" value={search} onChange={e => setSearch(e.target.value)}/>
        <select aria-label="Lọc loại văn bản" className={ui.field} style={{ maxWidth: 220 }} value={kind} onChange={e => setKind(e.target.value)}>
          <option value="">Hợp đồng và văn bản đi kèm</option><option value="CONTRACT">Chỉ hợp đồng</option><option value="ATTACHMENT">Chỉ văn bản đi kèm</option></select>
      </div>
      {!visibleRows ? <div className={s.empty}>{error ? "" : "Đang tải…"}</div> : list.total === 0 ? <div className={s.empty}><Files size={42} strokeWidth={1.3}/><strong>{filter === "signed" ? "Chưa có hợp đồng đã ký" : filter === "unsigned" ? "Không có hợp đồng chưa ký" : "Chưa có hợp đồng nào được gửi cho bạn"}</strong></div>
        : <div className={s.tableWrap}><table className={s.table}><thead><tr><SortHeader label="Hợp đồng" column="name" sort={list.sort} onSort={list.sortBy}/><SortHeader label="Phiên bản" column="version" sort={list.sort} onSort={list.sortBy} firstDir="desc"/><SortHeader label="Trạng thái" column="status" sort={list.sort} onSort={list.sortBy}/><SortHeader label="Ngày gửi" column="sent" sort={list.sort} onSort={list.sortBy} firstDir="desc"/><th><span className="sr-only">Mở</span></th></tr></thead>
          <tbody>{list.rows.map(r => <tr key={r.id}><td><Link href={`/portal/contracts/${r.id}`}>{r.contractName}</Link>{r.parentName && <div className={s.muted} style={{ fontSize: 12, marginTop: 4 }}>{r.documentTypeName ?? documentTypeLabel(r.documentType)} · thuộc {r.parentName}</div>}</td><td>{r.versionNo}</td>
            <td><span className={`${s.badge} ${!r.isSigned && isOpenReview(r.status) ? s.warning : ""}`}>{r.isSigned ? "Đã ký" : customerStatusLabel[r.status] ?? r.status}</span></td>
            <td className={s.muted}>{dateTime(r.sentAt)}</td><td><Link href={`/portal/contracts/${r.id}`} aria-label={r.isSigned ? undefined : `Mở ${r.contractName}`}>
              {r.isSigned ? "Xem hợp đồng đã ký" : r.status === "APPROVED" ? "Theo dõi và ký" : <ArrowUpRight size={18}/>}</Link></td></tr>)}</tbody></table></div>}
    {!!rows && <ListPager page={list.page} pageSize={list.pageSize} total={list.total} onPage={list.setPage}/>}</section>
  </div></>;
}
