"use client";
import { useEffect, useState } from "react";
import { ListPager } from "@/components/scanned-contracts/list-pager";
import { SortHeader, type SortState } from "@/components/internal/list-tools";
import Link from "next/link";
import { CalendarClock, CheckCircle2, FilePlus2, Files, Hourglass, Send, Trash2, XCircle } from "lucide-react";
import { SummaryCards } from "@/components/ui/custom/summary-cards";
import { internalApi, dateTime, type EmployeeOption } from "@/services/internal-api";
import { useInternalUser } from "./internal-shell";
import { internalStyles as ui } from "./internal-theme";
import ContractTabs from "./contract-tabs";
import s from "./workspace.module.css";

/** Every step a contract goes through, in order; the list filters on them. */
export const stages: Record<string, string> = {
  DRAFT: "Đang soạn", OFFICE: "Hành chính kiểm tra", RETURNED: "Trả lại chỉnh sửa", APPROVED: "Chờ Sale gửi", OFFICE_SEND: "Chờ Hành chính gửi",
  CUSTOMER: "Chờ khách phản hồi", CHANGES: "Khách yêu cầu xử lý", FINAL_CHECK: "Duyệt bản chốt", SIGNATURE: "Chờ ký",
  PAPER_SIGNATURE: "Chờ nộp bản ký giấy", PAPER_REVIEW: "Chờ xác nhận bản ký giấy", SIGNED: "Đã ký", CANCELLED: "Đã hủy", REJECTED: "Bị từ chối",
};
const finished = ["SIGNED", "CANCELLED", "REJECTED"];
type ExpiringContract = { kind: "CONTRACT_DOCUMENT" | "CONTRACT"; id: string; code: string; name: string; expireDate: string; daysLeft: number; employeeName: string | null };

type Row = { id: string; code: string; name: string; customer: string; ownerId: string; owner: string; stage: string; department: string; enteredAt: string | null;
  lastActor: string | null; status: number; canDelete: boolean; createdAt: string | null };
type Page = { items: Row[]; total: number; page: number; pageSize: number; counts: Record<string, number>; serverTime: string; sentCount: number };

export default function ProgressList() {
  // Quản lý hợp đồng: every contract whatever its status; a rejected or cancelled one can be deleted for good.
  const user = useInternalUser();
  const [data, setData] = useState<Page | null>(null); const [error, setError] = useState(""); const [notice, setNotice] = useState("");
  const [busy, setBusy] = useState(true); const [acting, setActing] = useState(false); const [employees, setEmployees] = useState<EmployeeOption[]>([]);
  const [search, setSearch] = useState(""); const [owner, setOwner] = useState(""); const [stage, setStage] = useState("");
  const [from, setFrom] = useState(""); const [to, setTo] = useState(""); const [page, setPage] = useState(1); const [reload, setReload] = useState(0);
  type Column = "name" | "customer" | "owner" | "stage" | "department" | "updated";
  const [sort, setSort] = useState<SortState<Column>>({ key: "updated", dir: "desc" });
  const sortBy = (key: Column, firstDir: "asc" | "desc" = "asc") => { setPage(1); setSort(old => old.key === key ? { key, dir: old.dir === "asc" ? "desc" : "asc" } : { key, dir: firstDir }); };
  const [items, setItems] = useState<ExpiringContract[] | null>(null);
  const [expiryError, setExpiryError] = useState("");
  const [expiryPage, setExpiryPage] = useState(1);
  const [showExpiry, setShowExpiry] = useState(false);
  useEffect(() => {
    let active = true;
    void internalApi<ExpiringContract[]>("/contracts/expiring").then(
      result => { if (active) setItems(result); },
      error => { if (active) setExpiryError(error.message); },
    );
    return () => { active = false; };
  }, [reload]);
  const badRange = !!from && !!to && from > to;
  useEffect(() => { if (user.canManageAll) internalApi<EmployeeOption[]>("/employees").then(setEmployees).catch(e => setError(e.message)); }, [user.canManageAll]);
  useEffect(() => { let active = true; if (badRange) return;
    const timer = setTimeout(() => {
      const params = new URLSearchParams({ page: String(page) });
      if (search.trim()) params.set("search", search.trim()); if (owner) params.set("ownerId", owner); if (stage) params.set("stage", stage);
      if (from) params.set("from", from); if (to) params.set("to", to);
      params.set("sort", sort.key); params.set("desc", String(sort.dir === "desc"));
      setBusy(true);
      internalApi<Page>(`/contract-progress?${params}`).then(v => { if (active) { setData(v); setError(""); } })
        .catch(e => { if (active) setError(e.message); }).finally(() => { if (active) setBusy(false); });
    }, 250);
    return () => { active = false; clearTimeout(timer); }; }, [search, owner, stage, page, reload, from, to, badRange, sort]);

  const counts = data?.counts ?? {};
  const count = (keys: string[]) => keys.reduce((n, k) => n + (counts[k] ?? 0), 0);
  const total = Object.values(counts).reduce((n, value) => n + value, 0);
  const filtered = !!(search || owner || stage || from || to);
  const filter = (next: string) => { setStage(next); setPage(1); };
  const remove = (row: Row) => {
    if (!confirm(`Xóa hẳn ${row.name}${row.code ? ` (${row.code})` : ""}? Mọi phiên bản, bình luận, tệp và lịch sử gửi khách của hợp đồng sẽ bị xóa và không khôi phục được.`)) return;
    setActing(true); setError(""); setNotice("");
    void internalApi<void>(`/contract-drafts/${row.id}`, "DELETE").then(() => { setNotice(`Đã xóa hẳn ${row.name}.`); setReload(n => n + 1); })
      .catch(e => setError(e.message)).finally(() => setActing(false));
  };

  const renderExpiry = () => {
    if (expiryError) return <section className={ui.card}><p role="alert" className={ui.error}>Không tải được hợp đồng sắp hết hạn: {expiryError}</p></section>;
    if (!items?.length) return <p className={s.muted}>{items ? "Không có hợp đồng hết hạn trong 30 ngày tới." : "Đang tải hợp đồng sắp hết hạn…"}</p>;
    const current = Math.min(expiryPage, Math.ceil(items.length / 10));
    return <section className={ui.card} aria-labelledby="expiring-title"><h2 id="expiring-title" style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 16, marginBottom: 10 }}><CalendarClock size={18}/>Hợp đồng sắp hết hạn (30 ngày tới)</h2>
      <ul style={{ display: "grid", gap: 8, listStyle: "none", padding: 0, margin: 0 }}>{items.slice((current - 1) * 10, current * 10).map(c => <li key={`${c.kind}-${c.id}`} style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 10, justifyContent: "space-between" }}>
        <Link href={c.kind === "CONTRACT_DOCUMENT" ? `/workspace/contracts/drafts/${c.id}` : "/workspace/scanned-contracts"}><strong>{c.code}</strong> · {c.name}</Link>
        <span style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 8 }}>
          {user.canManageAll && c.employeeName && <span className={s.muted}>{c.employeeName}</span>}
          <span className={s.muted}>Hết hạn {c.expireDate.split("-").reverse().join("/")}</span>
          <span className={`${s.badge} ${c.daysLeft <= 7 ? s.warning : ""}`}>{c.daysLeft === 0 ? "Hết hạn hôm nay" : `Còn ${c.daysLeft} ngày`}</span>
        </span></li>)}</ul>
      <ListPager page={current} pageSize={10} total={items.length} onPage={setExpiryPage} label="hợp đồng"/>
    </section>;
  };

  return <div className={s.page}>
    <div className={s.titleRow}><div><span className={s.eyebrow}>Hợp đồng</span><h1>Quản lý hợp đồng</h1><p className={s.muted}>Mọi hợp đồng và văn bản ở mọi trạng thái, từ lúc soạn đến khi ký xong hoặc đóng.</p></div>
      {(user.canWrite || user.canCheck) && <Link href="/workspace/contracts/drafts/new" className={ui.primaryButton}><FilePlus2 size={18}/>Tạo hợp đồng</Link>}</div>
    <div style={{ marginBottom: 16 }}><SummaryCards isLoading={busy && !data} items={[
      { title: "Tổng hợp đồng", value: total, icon: <Files size={24}/>, iconWrapperClassName: "bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-400" },
      { title: "Đang xử lý", value: total - count(finished), icon: <Hourglass size={24}/>, iconWrapperClassName: "bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-400" },
      { title: "Đã ký", value: count(["SIGNED"]), icon: <CheckCircle2 size={24}/>, iconWrapperClassName: "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-400" },
      { title: "Đã hủy / bị từ chối", value: count(["CANCELLED", "REJECTED"]), icon: <XCircle size={24}/>, iconWrapperClassName: "bg-rose-100 text-rose-700 dark:bg-rose-500/15 dark:text-rose-400" },
    ]}/></div>
    <ContractTabs/>
    <div className={s.contractQuickActions}>
    <button type="button" className={`${ui.card} ${s.sentCountCard}`} aria-pressed={stage === "SENT"} onClick={() => filter(stage === "SENT" ? "" : "SENT")}>
      <Send size={18} aria-hidden="true"/><span>Đã gửi khách</span><strong>{data?.sentCount ?? 0}</strong>
    </button>
      <button type="button" className={`${ui.card} ${s.sentCountCard}`} aria-expanded={showExpiry} aria-controls="expiring-contracts" onClick={() => setShowExpiry(value => !value)}>
        <CalendarClock size={18} aria-hidden="true"/><span>Sắp hết hạn</span><strong>{items?.length ?? "—"}</strong>
      </button>
    </div>
    <div id="expiring-contracts" hidden={!showExpiry} style={{ marginBottom: showExpiry ? 16 : undefined }}>{showExpiry && renderExpiry()}</div>
    {error && <p role="alert" className={ui.error} style={{ marginBottom: 16 }}>{error}</p>}
    {notice && <p role="status" className={ui.notice} style={{ marginBottom: 16 }}>{notice}</p>}
    <section className={ui.card}>
      <div className={s.contractFilters}>
        <input aria-label="Tìm hợp đồng" className={ui.field} placeholder="Tên, số hợp đồng, khách hàng hoặc mã số thuế…" value={search} onChange={e => { setSearch(e.target.value); setPage(1); }}/>
        <select aria-label="Lọc trạng thái" className={ui.field} value={stage} onChange={e => filter(e.target.value)}>
          <option value="">Tất cả trạng thái</option><option value="SENT">Đã gửi khách</option>{Object.entries(stages).map(([key, label]) => <option value={key} key={key}>{label}{counts[key] ? ` (${counts[key]})` : ""}</option>)}</select>
        {user.canManageAll && <select aria-label="Nhân viên phụ trách" className={ui.field} value={owner} onChange={e => { setOwner(e.target.value); setPage(1); }}>
          <option value="">Mọi nhân viên</option>{employees.map(e => <option key={e.id} value={e.id}>{e.name}</option>)}</select>}
        <input aria-label="Tạo từ ngày" title="Tạo từ ngày" className={ui.field} type="date" value={from} max={to || undefined} aria-invalid={badRange} onChange={e => { setFrom(e.target.value); setPage(1); }}/>
        <input aria-label="Tạo đến ngày" title="Tạo đến ngày" className={ui.field} type="date" value={to} min={from || undefined} aria-invalid={badRange} onChange={e => { setTo(e.target.value); setPage(1); }}/>
      </div>
      {badRange && <p role="alert" className={ui.error} style={{ marginBottom: 12 }}>“Từ ngày” phải trước hoặc bằng “Đến ngày”.</p>}
      {busy && !data ? <div className={s.empty}>Đang tải hợp đồng…</div> : !data || data.items.length === 0 ? <div className={s.empty}><Files size={42} strokeWidth={1.3}/>
        <strong>{filtered ? "Không tìm thấy hợp đồng phù hợp" : "Chưa có hợp đồng"}</strong><p>{filtered ? "Thử đổi từ khóa hoặc bộ lọc." : "Bấm Tạo hợp đồng để soạn hợp đồng đầu tiên."}</p></div>
      : <div className={s.tableWrap}><table className={s.table}><thead><tr><SortHeader label="Hợp đồng" column="name" sort={sort} onSort={sortBy}/><SortHeader label="Khách hàng" column="customer" sort={sort} onSort={sortBy}/><SortHeader label="Phụ trách" column="owner" sort={sort} onSort={sortBy}/><SortHeader label="Trạng thái" column="stage" sort={sort} onSort={sortBy}/><SortHeader label="Bộ phận đang xử lý" column="department" sort={sort} onSort={sortBy}/><SortHeader label="Cập nhật" column="updated" sort={sort} onSort={sortBy} firstDir="desc"/><th><span className="sr-only">Thao tác</span></th></tr></thead>
        <tbody>{data.items.map(row => <tr key={row.id}>
          <td><Link href={`/workspace/contracts/drafts/${row.id}`}><strong>{row.name}</strong></Link>{row.code && <div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{row.code}</div>}</td>
          <td>{row.customer}</td>
          <td>{row.owner}</td>
          <td><span className={`${s.badge} ${row.stage === "CANCELLED" || row.stage === "REJECTED" ? s.warning : ""}`}>{stages[row.stage] ?? row.stage}</span>
            {row.lastActor && <div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>Duyệt gần nhất · {row.lastActor}</div>}</td>
          <td className={s.muted}>{row.department}</td>
          <td className={s.muted}>{row.enteredAt ?? row.createdAt ? dateTime((row.enteredAt ?? row.createdAt)!) : "—"}</td>
          <td><div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <Link className={ui.secondaryButton} href={`/workspace/contracts/drafts/${row.id}`}>Mở</Link>
            {row.canDelete && <button className={ui.secondaryButton} disabled={acting} onClick={() => remove(row)} aria-label={`Xóa hẳn ${row.name}`}><Trash2 size={15}/>Xóa hẳn</button>}
          </div></td></tr>)}</tbody></table></div>}
      {!!data && <ListPager page={page} pageSize={data.pageSize} total={data.total} onPage={setPage} label="hợp đồng" disabled={busy}/>}
    </section>
  </div>;
}
