"use client";
import { useEffect, useState } from "react";
import { Contact, RefreshCw, UserCheck, UserPlus, UserX, Mail } from "lucide-react";
import { SummaryCards } from "@/components/ui/custom/summary-cards";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import { CustomerFormDialog, CustomerCredentialsDialog } from "@/components/internal/catalog-dialogs";
import s from "@/components/internal/workspace.module.css";
import { useInternalUser } from "@/components/internal/internal-shell";
import { internalApi, dateTime, type CustomerOwnerOption, type CustomerRow } from "@/services/internal-api";
import { ListPager } from "@/components/scanned-contracts/list-pager";
import { SortHeader, useListView } from "@/components/internal/list-tools";

export default function CustomersPage() {
  // Sale: the customers they look after. Hành chính and Ban Giám Đốc (customer.manage.all): every customer and who looks after it.
  const all = useInternalUser().permissions.includes("customer.manage.all");
  const [owners, setOwners] = useState<CustomerOwnerOption[]>([]); const [owner, setOwner] = useState("");
  const [rows, setRows] = useState<CustomerRow[]>([]); const [search, setSearch] = useState(""); const [status, setStatus] = useState(""); const [kind, setKind] = useState("");
  const [error, setError] = useState(""); const [notice, setNotice] = useState(""); const [busy, setBusy] = useState(true); const [acting, setActing] = useState(false); const [reload, setReload] = useState(0);
  const [form, setForm] = useState<{ customer: CustomerRow | null } | null>(null);
  const [credentialsCustomer, setCredentialsCustomer] = useState<CustomerRow | null>(null);
  useEffect(() => { let active = true;
    void internalApi<CustomerRow[]>("/crm/customers").then(list => { if (active) setRows(list); })
      .catch(e => { if (active) setError(e.message); }).finally(() => { if (active) setBusy(false); });
    return () => { active = false; }; }, [reload]);
  useEffect(() => { if (all) void internalApi<CustomerOwnerOption[]>("/crm/owners").then(setOwners).catch(() => setOwners([])); }, [all]);
  const list = useListView(rows, { search, fields: r => [r.name, r.phone, r.email, r.address, r.taxIdentifier], filterKey: [status, owner, kind],
    filter: r => (!status || (status === "ACTIVE") === r.active) && (!owner || (owner === "NONE" ? !r.ownerId : r.ownerId === owner))
      && (!kind || (kind === "BUSINESS") === r.businessCount > 0),
    sorts: { name: r => r.name, contact: r => r.phone, business: r => r.businessCount, owner: r => r.ownerName, status: r => r.active, created: r => r.createdAt },
    initial: { key: "created", dir: "desc" } });
  const filtered = !!(search || status || owner || kind);
  const locked = rows.filter(r => !r.active).length;
  const replace = (row: CustomerRow) => setRows(list => list.some(r => r.id === row.id) ? list.map(r => r.id === row.id ? row : r) : [...list, row].sort((a, b) => a.name.localeCompare(b.name, "vi")));
  const toggle = (row: CustomerRow) => {
    if (!confirm(row.active ? `Khóa khách hàng ${row.name}? Khách sẽ không đăng nhập được cổng khách hàng và không nhận được hợp đồng mới.` : `Mở khóa khách hàng ${row.name}?`)) return;
    setActing(true); setError(""); setNotice("");
    void internalApi<CustomerRow>(`/crm/customers/${row.id}/status`, "PUT", { active: !row.active })
      .then(r => { replace(r); setNotice(r.active ? `Đã mở khóa ${r.name}.` : `Đã khóa ${r.name}.`); }).catch(e => setError(e.message)).finally(() => setActing(false)); };
  const invite = (row: CustomerRow) => {
    setActing(true); setError(""); setNotice("");
    void internalApi<{ sentTo: string }>(`/crm/customers/${row.id}/invite`, "POST")
      .then(r => setNotice(`Đã gửi email mời tới ${r.sentTo}. Khách bấm "Đặt mật khẩu" trong thư để tạo mật khẩu.`)).catch(e => setError(e.message)).finally(() => setActing(false)); };
  const transfer = (row: CustomerRow, ownerId: string) => {
    const target = owners.find(o => o.id === ownerId)?.name ?? "chưa giao ai";
    if (!confirm(`Chuyển khách hàng ${row.name} sang ${target}? Người phụ trách cũ sẽ không còn thấy khách này.`)) return;
    setActing(true); setError(""); setNotice("");
    void internalApi<CustomerRow>(`/crm/customers/${row.id}/owner`, "PUT", { ownerId: ownerId || null })
      .then(r => { replace(r); setNotice(`Đã chuyển ${r.name} sang ${r.ownerName ?? "chưa giao ai"}.`); }).catch(e => setError(e.message)).finally(() => setActing(false)); };

  return <div className={s.page}><div className={s.titleRow}><div><span className={s.eyebrow}>{all ? "Toàn bộ khách hàng" : "Khách hàng bạn phụ trách"}</span><h1>Khách hàng</h1><p className={s.muted}>{all ? "Mọi khách hàng của công ty. Chọn người phụ trách để giao hoặc chuyển khách cho nhân viên kinh doanh." : "Khách bạn tạo hoặc được giao. Khách của nhân viên khác do Phòng Hành Chính - Pháp Chế chuyển cho bạn."}</p></div>
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
      <select aria-label="Lọc loại khách hàng" className={ui.field} style={{ maxWidth: 220 }} value={kind} onChange={e => setKind(e.target.value)}><option value="">Cá nhân và doanh nghiệp</option><option value="PERSON">Cá nhân</option><option value="BUSINESS">Có pháp nhân</option></select>
      {all && <select aria-label="Lọc người phụ trách" className={ui.field} style={{ maxWidth: 240 }} value={owner} onChange={e => setOwner(e.target.value)}><option value="">Mọi người phụ trách</option><option value="NONE">Chưa giao ai</option>{owners.map(o => <option key={o.id} value={o.id}>{o.name}</option>)}</select>}
      <button className={ui.secondaryButton} disabled={busy} onClick={() => { setBusy(true); setError(""); setReload(reload + 1); }} aria-label="Làm mới"><RefreshCw size={16}/></button></div>
      {busy ? <div className={s.empty}>Đang tải danh sách khách hàng…</div> : list.total === 0 ? <div className={s.empty}><Contact size={42} strokeWidth={1.3}/><strong>{filtered ? "Không tìm thấy khách hàng phù hợp" : "Chưa có khách hàng"}</strong><p>{filtered ? "Thử đổi từ khóa hoặc bộ lọc." : "Thêm khách hàng đầu tiên để soạn hợp đồng."}</p></div>
      : <div className={s.tableWrap}><table className={s.table}><thead><tr><SortHeader label="Khách hàng" column="name" sort={list.sort} onSort={list.sortBy}/><SortHeader label="Liên hệ" column="contact" sort={list.sort} onSort={list.sortBy}/>
          <SortHeader label="Pháp nhân" column="business" sort={list.sort} onSort={list.sortBy} firstDir="desc"/>{all && <SortHeader label="Người phụ trách" column="owner" sort={list.sort} onSort={list.sortBy}/>}
          <SortHeader label="Trạng thái" column="status" sort={list.sort} onSort={list.sortBy} firstDir="desc"/><SortHeader label="Ngày tạo" column="created" sort={list.sort} onSort={list.sortBy} firstDir="desc"/><th><span className="sr-only">Thao tác</span></th></tr></thead>
        <tbody>{list.rows.map(r => <tr key={r.id}><td><strong>{r.name}</strong>{r.address && <div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{r.address}</div>}</td>
          <td>{r.phone}<div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{r.email ?? "Chưa có email"}</div></td>
          <td>{r.businessCount ? <span className={s.badge}>{r.businessCount} pháp nhân</span> : <span className={s.muted}>Cá nhân</span>}</td>
          {all && <td><select aria-label={`Người phụ trách ${r.name}`} className={ui.field} style={{ minWidth: 170 }} disabled={acting} value={r.ownerId ?? ""} onChange={e => transfer(r, e.target.value)}>
            <option value="">Chưa giao ai</option>{owners.map(o => <option key={o.id} value={o.id}>{o.name}</option>)}
            {r.ownerId && !owners.some(o => o.id === r.ownerId) && <option value={r.ownerId}>{r.ownerName ?? "Nhân viên khác"}</option>}</select></td>}
          <td><span className={`${s.badge} ${r.active ? "" : s.warning}`}>{r.active ? "Đang hoạt động" : "Đã khóa"}</span></td>
          <td className={s.muted}>{dateTime(r.createdAt)}</td>
          <td><div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <button className={ui.secondaryButton} disabled={acting} onClick={() => setForm({ customer: r })}>Sửa</button>
            {all && r.email && r.active && <button className={ui.secondaryButton} disabled={acting} title="Gửi thông tin tài khoản và mật khẩu tới email khách hàng" onClick={() => setCredentialsCustomer(r)}><Mail size={15}/>Gửi thông tin tài khoản</button>}
            <button className={ui.secondaryButton} disabled={acting} onClick={() => toggle(r)}>{r.active ? "Khóa" : "Mở khóa"}</button>
          </div></td></tr>)}</tbody></table></div>}
      {!busy && <ListPager page={list.page} pageSize={list.pageSize} total={list.total} onPage={list.setPage} label="khách hàng"/>}
    </section>
    {form && <CustomerFormDialog customer={form.customer} onClose={() => setForm(null)}
      onSaved={row => { const created = form.customer === null; setForm(null); replace(row); setNotice(created ? `Đã thêm ${row.name}.` : `Đã cập nhật ${row.name}.`); }}/>}
    {credentialsCustomer && <CustomerCredentialsDialog customer={credentialsCustomer} onClose={() => setCredentialsCustomer(null)}
      onSent={sentTo => {
        const name = credentialsCustomer.name;
        setCredentialsCustomer(null);
        setNotice(`Đã gửi thông tin tài khoản của ${name} tới ${sentTo}. Khách hàng sẽ dùng SĐT và mật khẩu này để đăng nhập và đổi mật khẩu mới.`);
      }}/>}
  </div>;
}
