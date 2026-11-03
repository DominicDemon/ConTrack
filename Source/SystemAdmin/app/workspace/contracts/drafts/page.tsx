"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { FilePlus2, Files, CircleCheck, ClipboardList, CircleAlert, RefreshCw, ArrowUpRight } from "lucide-react";
import { SummaryCards } from "@/components/ui/custom/summary-cards";
import { useInternalUser } from "@/components/internal/internal-shell";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import { internalApi, dateTime, documentStatusLabel, reviewStatusLabel, type DraftSummary, type EmployeeOption } from "@/services/internal-api";
export default function DraftList() {
  const user = useInternalUser(); const [rows, setRows] = useState<DraftSummary[]>([]); const [employees, setEmployees] = useState<EmployeeOption[]>([]);
  const [search, setSearch] = useState(""); const [owner, setOwner] = useState(""); const [status, setStatus] = useState(""); const [error, setError] = useState(""); const [busy, setBusy] = useState(true); const [reload, setReload] = useState(0);
  useEffect(() => { if (user.canManageAll) void internalApi<EmployeeOption[]>("/employees").then(setEmployees).catch(e => setError(e.message)); }, [user.canManageAll]);
  useEffect(() => { let active = true; const timer = setTimeout(() => { setBusy(true); setError(""); const params = new URLSearchParams(); if (search.trim()) params.set("search", search.trim()); if (owner) params.set("ownerId", owner); if (status) params.set("status", status);
    void internalApi<DraftSummary[]>(`/contract-drafts?${params}`).then(data => { if (active) setRows(data); }).catch(e => { if (active) setError(e.message); }).finally(() => { if (active) setBusy(false); });
  }, 250); return () => { active = false; clearTimeout(timer); }; }, [search, owner, status, reload]);
  const complete = rows.filter(r => r.missingCount === 0).length;
  return <div className={s.page}><div className={s.titleRow}><div><span className={s.eyebrow}>Hồ sơ hợp đồng</span><h1>Hồ sơ hợp đồng</h1><p className={s.muted}>Bắt đầu từ mẫu và hoàn thiện nội dung cùng đội ngũ của bạn.</p></div><Link href="/workspace/contracts/drafts/new" className={ui.primaryButton}><FilePlus2 size={18}/>Tạo hợp đồng nháp</Link></div>
    <div style={{ marginBottom: 24 }}><SummaryCards isLoading={busy} items={[
      { title: "Bản nháp trong kết quả", value: rows.length, icon: <ClipboardList size={24}/>, iconWrapperClassName: "bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-400" },
      { title: "Cần bổ sung nội dung", value: rows.length - complete, icon: <CircleAlert size={24}/>, iconWrapperClassName: "bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-400" },
      { title: "Đã đủ trường thông tin", value: complete, icon: <CircleCheck size={24}/>, iconWrapperClassName: "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-400" },
    ]}/></div>
    {error && <p role="alert" className={ui.error} style={{ marginBottom: 16 }}>{error}</p>}
    <section className={ui.card}><div className={s.toolbar}><input aria-label="Tìm hợp đồng nháp" className={ui.field} placeholder="Tìm tên hợp đồng, mã hoặc khách hàng…" value={search} onChange={e => setSearch(e.target.value)}/>
      {user.canManageAll && <select aria-label="Lọc người phụ trách" className={ui.field} style={{ maxWidth: 240 }} value={owner} onChange={e => setOwner(e.target.value)}><option value="">Tất cả người phụ trách</option>{employees.map(e => <option value={e.id} key={e.id}>{e.name}</option>)}</select>}
      <select aria-label="Lọc trạng thái" className={ui.field} style={{ maxWidth: 220 }} value={status} onChange={e => setStatus(e.target.value)}><option value="">Tất cả trạng thái</option>{[0, 1, 7, 2, 6].filter(v => v !== 2 || user.canManageAll).map(v => <option key={v} value={v}>{documentStatusLabel[v]}</option>)}</select>
      <button className={ui.secondaryButton} disabled={busy} onClick={() => setReload(reload + 1)} aria-label="Làm mới"><RefreshCw size={16}/></button></div>
      {busy ? <div className={s.empty}>Đang tải bản nháp…</div> : rows.length === 0 ? <div className={s.empty}><Files size={42} strokeWidth={1.3}/><strong>{search || owner || status ? "Không tìm thấy bản nháp phù hợp" : "Bắt đầu với hợp đồng đầu tiên"}</strong><p>{search || owner || status ? "Thử đổi từ khóa hoặc bộ lọc." : "Chọn mẫu, điền thông tin và lưu lại để tiếp tục bất cứ lúc nào."}</p><Link className={ui.secondaryButton} href="/workspace/contracts/drafts/new">Tạo bản nháp</Link></div> : <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Hợp đồng</th><th>Khách hàng</th><th>Người phụ trách</th><th>Trạng thái</th><th>Hoàn thiện</th><th>Cập nhật</th><th><span className="sr-only">Mở</span></th></tr></thead><tbody>{rows.map(r => <tr key={r.id}><td><Link href={`/workspace/contracts/drafts/${r.id}`}>{r.name}</Link><div className={s.muted} style={{ fontSize: 10, marginTop: 4 }}>{r.code}</div></td><td>{r.customerName}</td><td>{r.employeeName}</td><td><span className={s.badge}>{documentStatusLabel[r.status] ?? r.status}</span>{r.latestReviewStatus && <div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{reviewStatusLabel[r.latestReviewStatus] ?? r.latestReviewStatus}</div>}</td><td><span className={`${s.badge} ${r.missingCount ? s.warning : ""}`}>{r.missingCount ? `Còn thiếu ${r.missingCount} mục` : "Đủ thông tin"}</span></td><td className={s.muted}>{dateTime(r.updatedAt)}</td><td><Link href={`/workspace/contracts/drafts/${r.id}`} aria-label={`Mở ${r.name}`}><ArrowUpRight size={18}/></Link></td></tr>)}</tbody></table></div>}
    </section><p className={s.muted} style={{ marginTop: 14 }}>Hiển thị tối đa 200 bản nháp theo bộ lọc. “Đủ thông tin” chưa có nghĩa là hợp đồng đã được phê duyệt.</p>
  </div>;
}
