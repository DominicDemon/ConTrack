"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { CheckCircle2, FilePlus2, Files, Hourglass, Trash2, XCircle } from "lucide-react";
import { SummaryCards } from "@/components/ui/custom/summary-cards";
import { internalApi, dateTime, type EmployeeOption } from "@/services/internal-api";
import { useInternalUser } from "./internal-shell";
import { internalStyles as ui } from "./internal-theme";
import ContractTabs from "./contract-tabs";
import s from "./workspace.module.css";

/** Every step a contract goes through, in order; the list filters on them. */
const stages: Record<string, string> = {
  DRAFT: "Đang soạn", OFFICE: "Hành chính kiểm tra", RETURNED: "Trả lại chỉnh sửa", APPROVED: "Chờ Sale gửi", OFFICE_SEND: "Chờ Hành chính gửi",
  CUSTOMER: "Chờ khách phản hồi", CHANGES: "Khách yêu cầu xử lý", FINAL_CHECK: "Duyệt bản chốt", SIGNATURE: "Chờ ký",
  PAPER_SIGNATURE: "Chờ nộp bản ký giấy", PAPER_REVIEW: "Chờ xác nhận bản ký giấy", SIGNED: "Đã ký", CANCELLED: "Đã hủy", REJECTED: "Bị từ chối",
};
const finished = ["SIGNED", "CANCELLED", "REJECTED"];
type Row = { id: string; code: string; name: string; customer: string; ownerId: string; owner: string; stage: string; department: string; enteredAt: string | null;
  lastActor: string | null; status: number; canDelete: boolean; createdAt: string | null };
type Page = { items: Row[]; total: number; page: number; pageSize: number; counts: Record<string, number>; serverTime: string };

export default function ProgressList() {
  // Quản lý hợp đồng: every contract whatever its status; a rejected or cancelled one can be deleted for good.
  const user = useInternalUser();
  const [data, setData] = useState<Page | null>(null); const [error, setError] = useState(""); const [notice, setNotice] = useState("");
  const [busy, setBusy] = useState(true); const [acting, setActing] = useState(false); const [employees, setEmployees] = useState<EmployeeOption[]>([]);
  const [search, setSearch] = useState(""); const [owner, setOwner] = useState(""); const [stage, setStage] = useState("");
  const [from, setFrom] = useState(""); const [to, setTo] = useState(""); const [page, setPage] = useState(1); const [reload, setReload] = useState(0);
  const badRange = !!from && !!to && from > to;
  useEffect(() => { if (user.canManageAll) internalApi<EmployeeOption[]>("/employees").then(setEmployees).catch(e => setError(e.message)); }, [user.canManageAll]);
  useEffect(() => { let active = true; if (badRange) return;
    const timer = setTimeout(() => {
      const params = new URLSearchParams({ page: String(page) });
      if (search.trim()) params.set("search", search.trim()); if (owner) params.set("ownerId", owner); if (stage) params.set("stage", stage);
      if (from) params.set("from", from); if (to) params.set("to", to);
      setBusy(true);
      internalApi<Page>(`/contract-progress?${params}`).then(v => { if (active) { setData(v); setError(""); } })
        .catch(e => { if (active) setError(e.message); }).finally(() => { if (active) setBusy(false); });
    }, 250);
    return () => { active = false; clearTimeout(timer); }; }, [search, owner, stage, page, reload, from, to, badRange]);

  const counts = data?.counts ?? {};
  const count = (keys: string[]) => keys.reduce((n, k) => n + (counts[k] ?? 0), 0);
  const total = Object.values(counts).reduce((n, v) => n + v, 0);
  const filtered = !!(search || owner || stage || from || to);
  const filter = (next: string) => { setStage(next); setPage(1); };
  const remove = (row: Row) => {
    if (!confirm(`Xóa hẳn ${row.name}${row.code ? ` (${row.code})` : ""}? Mọi phiên bản, bình luận, tệp và lịch sử gửi khách của hợp đồng sẽ bị xóa và không khôi phục được.`)) return;
    setActing(true); setError(""); setNotice("");
    void internalApi<void>(`/contract-drafts/${row.id}`, "DELETE").then(() => { setNotice(`Đã xóa hẳn ${row.name}.`); setReload(n => n + 1); })
      .catch(e => setError(e.message)).finally(() => setActing(false));
  };

  return <div className={s.page}>
    <div className={s.titleRow}><div><span className={s.eyebrow}>Hợp đồng</span><h1>Quản lý hợp đồng</h1><p className={s.muted}>Mọi hợp đồng và văn bản ở mọi trạng thái, từ lúc soạn đến khi ký xong hoặc đóng.</p></div>
      {(user.canWrite || user.canCheck) && <Link href="/workspace/contracts/drafts/new" className={ui.primaryButton}><FilePlus2 size={18}/>Tạo hợp đồng</Link>}</div>
    <ContractTabs/>
    <div style={{ marginBottom: 24 }}><SummaryCards isLoading={busy && !data} items={[
      { title: "Tổng hợp đồng", value: total, icon: <Files size={24}/>, iconWrapperClassName: "bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-400" },
      { title: "Đang xử lý", value: total - count(finished), icon: <Hourglass size={24}/>, iconWrapperClassName: "bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-400" },
      { title: "Đã ký", value: count(["SIGNED"]), icon: <CheckCircle2 size={24}/>, iconWrapperClassName: "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-400" },
      { title: "Đã hủy / bị từ chối", value: count(["CANCELLED", "REJECTED"]), icon: <XCircle size={24}/>, iconWrapperClassName: "bg-rose-100 text-rose-700 dark:bg-rose-500/15 dark:text-rose-400" },
    ]}/></div>
    {error && <p role="alert" className={ui.error} style={{ marginBottom: 16 }}>{error}</p>}
    {notice && <p role="status" className={ui.notice} style={{ marginBottom: 16 }}>{notice}</p>}
    <section className={ui.card}>
      <div className={s.toolbar}>
        <input aria-label="Tìm hợp đồng" className={ui.field} placeholder="Tên, số hợp đồng, khách hàng hoặc mã số thuế…" value={search} onChange={e => { setSearch(e.target.value); setPage(1); }}/>
        <select aria-label="Lọc trạng thái" className={ui.field} style={{ maxWidth: 240 }} value={stage} onChange={e => filter(e.target.value)}>
          <option value="">Tất cả trạng thái</option>{Object.entries(stages).map(([key, label]) => <option value={key} key={key}>{label}{counts[key] ? ` (${counts[key]})` : ""}</option>)}</select>
        {user.canManageAll && <select aria-label="Nhân viên phụ trách" className={ui.field} style={{ maxWidth: 220 }} value={owner} onChange={e => { setOwner(e.target.value); setPage(1); }}>
          <option value="">Mọi nhân viên</option>{employees.map(e => <option key={e.id} value={e.id}>{e.name}</option>)}</select>}
        <input aria-label="Tạo từ ngày" title="Tạo từ ngày" className={ui.field} style={{ maxWidth: 170 }} type="date" value={from} max={to || undefined} aria-invalid={badRange} onChange={e => { setFrom(e.target.value); setPage(1); }}/>
        <input aria-label="Tạo đến ngày" title="Tạo đến ngày" className={ui.field} style={{ maxWidth: 170 }} type="date" value={to} min={from || undefined} aria-invalid={badRange} onChange={e => { setTo(e.target.value); setPage(1); }}/>
      </div>
      {badRange && <p role="alert" className={ui.error} style={{ marginBottom: 12 }}>“Từ ngày” phải trước hoặc bằng “Đến ngày”.</p>}
      {busy && !data ? <div className={s.empty}>Đang tải hợp đồng…</div> : !data || data.items.length === 0 ? <div className={s.empty}><Files size={42} strokeWidth={1.3}/>
        <strong>{filtered ? "Không tìm thấy hợp đồng phù hợp" : "Chưa có hợp đồng"}</strong><p>{filtered ? "Thử đổi từ khóa hoặc bộ lọc." : "Bấm Tạo hợp đồng để soạn hợp đồng đầu tiên."}</p></div>
      : <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Hợp đồng</th><th>Khách hàng</th><th>Phụ trách</th><th>Trạng thái</th><th>Bộ phận đang xử lý</th><th>Cập nhật</th><th><span className="sr-only">Thao tác</span></th></tr></thead>
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
      {!!data && data.total > data.pageSize && <div className={s.toolbar} style={{ justifyContent: "space-between", marginTop: 12, marginBottom: 0 }}>
        <span className={s.muted}>Trang {page} · {data.total} hợp đồng</span>
        <div style={{ display: "flex", gap: 8 }}><button className={ui.secondaryButton} disabled={page === 1 || busy} onClick={() => setPage(p => p - 1)}>Trang trước</button>
          <button className={ui.secondaryButton} disabled={busy || page * data.pageSize >= data.total} onClick={() => setPage(p => p + 1)}>Trang sau</button></div></div>}
    </section>
  </div>;
}
