"use client";
import { useEffect, useState } from "react";
import { Contact, RefreshCw, UserCheck, UserPlus, UserX } from "lucide-react";
import { SummaryCards } from "@/components/ui/custom/summary-cards";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import { CustomerFormDialog } from "@/components/internal/catalog-dialogs";
import s from "@/components/internal/workspace.module.css";
import { internalApi, dateTime, type CustomerRow } from "@/services/internal-api";

export default function CustomersPage() {
  const [rows, setRows] = useState<CustomerRow[]>([]); const [search, setSearch] = useState(""); const [status, setStatus] = useState("");
  const [error, setError] = useState(""); const [notice, setNotice] = useState(""); const [busy, setBusy] = useState(true); const [acting, setActing] = useState(false); const [reload, setReload] = useState(0);
  const [form, setForm] = useState<{ customer: CustomerRow | null } | null>(null);
  useEffect(() => { let active = true;
    void internalApi<CustomerRow[]>("/crm/customers").then(list => { if (active) setRows(list); })
      .catch(e => { if (active) setError(e.message); }).finally(() => { if (active) setBusy(false); });
    return () => { active = false; }; }, [reload]);
  const term = search.trim().toLowerCase();
  const shown = rows.filter(r => (!status || (status === "ACTIVE") === r.active) && (!term || [r.name, r.phone, r.email ?? ""].some(v => v.toLowerCase().includes(term))));
  const locked = rows.filter(r => !r.active).length;
  const replace = (row: CustomerRow) => setRows(list => list.some(r => r.id === row.id) ? list.map(r => r.id === row.id ? row : r) : [...list, row].sort((a, b) => a.name.localeCompare(b.name, "vi")));
  const toggle = (row: CustomerRow) => {
    if (!confirm(row.active ? `Khóa khách hàng ${row.name}? Khách sẽ không đăng nhập được cổng khách hàng và không nhận được hợp đồng mới.` : `Mở khóa khách hàng ${row.name}?`)) return;
    setActing(true); setError(""); setNotice("");
    void internalApi<CustomerRow>(`/crm/customers/${row.id}/status`, "PUT", { active: !row.active })
      .then(r => { replace(r); setNotice(r.active ? `Đã mở khóa ${r.name}.` : `Đã khóa ${r.name}.`); }).catch(e => setError(e.message)).finally(() => setActing(false)); };

  return <div className={s.page}><div className={s.titleRow}><div><span className={s.eyebrow}>Kinh doanh</span><h1>Khách hàng</h1><p className={s.muted}>Thông tin liên hệ khách dùng để soạn hợp đồng và đăng nhập cổng khách hàng.</p></div>
    <button className={ui.primaryButton} disabled={busy} onClick={() => setForm({ customer: null })}><UserPlus size={18}/>Thêm khách hàng</button></div>
    <div style={{ marginBottom: 24 }}><SummaryCards isLoading={busy} items={[
      { title: "Khách hàng", value: rows.length, icon: <Contact size={24}/>, iconWrapperClassName: "bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-400" },
      { title: "Đang hoạt động", value: rows.length - locked, icon: <UserCheck size={24}/>, iconWrapperClassName: "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-400" },
      { title: "Đã khóa", value: locked, icon: <UserX size={24}/>, iconWrapperClassName: "bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-400" },
    ]}/></div>
    {error && <p role="alert" className={ui.error} style={{ marginBottom: 16 }}>{error}</p>}
    {notice && <p role="status" className={ui.notice} style={{ marginBottom: 16 }}>{notice}</p>}
    <section className={ui.card}><div className={s.toolbar}><input aria-label="Tìm khách hàng" className={ui.field} placeholder="Tìm tên, số điện thoại hoặc email…" value={search} onChange={e => setSearch(e.target.value)}/>
      <select aria-label="Lọc trạng thái" className={ui.field} style={{ maxWidth: 220 }} value={status} onChange={e => setStatus(e.target.value)}><option value="">Tất cả trạng thái</option><option value="ACTIVE">Đang hoạt động</option><option value="LOCKED">Đã khóa</option></select>
      <button className={ui.secondaryButton} disabled={busy} onClick={() => { setBusy(true); setError(""); setReload(reload + 1); }} aria-label="Làm mới"><RefreshCw size={16}/></button></div>
      {busy ? <div className={s.empty}>Đang tải danh sách khách hàng…</div> : shown.length === 0 ? <div className={s.empty}><Contact size={42} strokeWidth={1.3}/><strong>{search || status ? "Không tìm thấy khách hàng phù hợp" : "Chưa có khách hàng"}</strong><p>{search || status ? "Thử đổi từ khóa hoặc bộ lọc." : "Thêm khách hàng đầu tiên để soạn hợp đồng."}</p></div>
      : <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Khách hàng</th><th>Liên hệ</th><th>Pháp nhân</th><th>Trạng thái</th><th>Ngày tạo</th><th><span className="sr-only">Thao tác</span></th></tr></thead>
        <tbody>{shown.map(r => <tr key={r.id}><td><strong>{r.name}</strong>{r.address && <div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{r.address}</div>}</td>
          <td>{r.phone}<div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{r.email ?? "Chưa có email"}</div></td>
          <td>{r.businessCount ? <span className={s.badge}>{r.businessCount} pháp nhân</span> : <span className={s.muted}>Cá nhân</span>}</td>
          <td><span className={`${s.badge} ${r.active ? "" : s.warning}`}>{r.active ? "Đang hoạt động" : "Đã khóa"}</span></td>
          <td className={s.muted}>{dateTime(r.createdAt)}</td>
          <td><div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <button className={ui.secondaryButton} disabled={acting} onClick={() => setForm({ customer: r })}>Sửa</button>
            <button className={ui.secondaryButton} disabled={acting} onClick={() => toggle(r)}>{r.active ? "Khóa" : "Mở khóa"}</button>
          </div></td></tr>)}</tbody></table></div>}
    </section>
    {form && <CustomerFormDialog customer={form.customer} onClose={() => setForm(null)}
      onSaved={row => { const created = form.customer === null; setForm(null); replace(row); setNotice(created ? `Đã thêm ${row.name}.` : `Đã cập nhật ${row.name}.`); }}/>}
  </div>;
}
