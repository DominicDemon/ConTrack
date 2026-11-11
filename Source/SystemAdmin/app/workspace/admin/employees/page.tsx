"use client";
import { useEffect, useState } from "react";
import { UserPlus, UsersRound, UserCheck, UserX, RefreshCw } from "lucide-react";
import { SummaryCards } from "@/components/ui/custom/summary-cards";
import { useInternalUser } from "@/components/internal/internal-shell";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import { EmployeeFormDialog, TemporaryPasswordDialog } from "@/components/internal/employee-dialogs";
import s from "@/components/internal/workspace.module.css";
import { internalApi, dateTime, type EmployeeAdminRow, type DepartmentOption, type TemporaryPasswordResult } from "@/services/internal-api";

export default function EmployeesPage() {
  const user = useInternalUser();
  const [rows, setRows] = useState<EmployeeAdminRow[]>([]); const [departments, setDepartments] = useState<DepartmentOption[]>([]);
  const [search, setSearch] = useState(""); const [status, setStatus] = useState(""); const [department, setDepartment] = useState(""); const [error, setError] = useState(""); const [notice, setNotice] = useState("");
  const [busy, setBusy] = useState(true); const [acting, setActing] = useState(false); const [reload, setReload] = useState(0);
  const [form, setForm] = useState<{ employee: EmployeeAdminRow | null } | null>(null); const [issued, setIssued] = useState<TemporaryPasswordResult | null>(null);
  useEffect(() => { let active = true;
    void Promise.all([internalApi<EmployeeAdminRow[]>("/admin/employees"), internalApi<DepartmentOption[]>("/admin/departments")])
      .then(([employees, departmentList]) => { if (active) { setRows(employees); setDepartments(departmentList); } })
      .catch(e => { if (active) setError(e.message); }).finally(() => { if (active) setBusy(false); });
    return () => { active = false; }; }, [reload]);
  const departmentName = (id: string | null) => departments.find(d => d.id === id)?.name;
  const term = search.trim().toLowerCase();
  const shown = rows.filter(r => (!status || r.status === status) && (!department || r.departmentId === department)
    && (!term || [r.name, r.username, r.code, r.email ?? "", r.phone ?? ""].some(v => v.toLowerCase().includes(term))));
  const locked = rows.filter(r => r.status !== "ACTIVE").length;
  const replace = (row: EmployeeAdminRow) => setRows(list => list.some(r => r.id === row.id) ? list.map(r => r.id === row.id ? row : r) : [...list, row].sort((a, b) => a.name.localeCompare(b.name, "vi")));
  // Manager seats change with a position, so the department list (who manages what) is reloaded after each save.
  const refreshDepartments = () => void internalApi<DepartmentOption[]>("/admin/departments").then(setDepartments).catch(e => setError(e.message));
  async function act(run: () => Promise<void>) { setActing(true); setError(""); setNotice(""); try { await run(); } catch (e) { setError((e as Error).message); } finally { setActing(false); } }
  const toggle = (row: EmployeeAdminRow) => { const unlock = row.status !== "ACTIVE";
    if (!confirm(unlock ? `Mở khóa tài khoản ${row.username}?` : `Khóa tài khoản ${row.username}? Nhân viên sẽ bị đăng xuất ở lần thao tác kế tiếp.`)) return;
    void act(async () => { replace(await internalApi<EmployeeAdminRow>(`/admin/employees/${row.id}/status`, "PUT", { active: unlock })); setNotice(unlock ? `Đã mở khóa ${row.username}.` : `Đã khóa ${row.username}.`); }); };
  const reset = (row: EmployeeAdminRow) => {
    if (!confirm(`Cấp mật khẩu tạm mới cho ${row.username}? Mật khẩu hiện tại sẽ không dùng được nữa.`)) return;
    void act(async () => { const result = await internalApi<TemporaryPasswordResult>(`/admin/employees/${row.id}/reset-password`, "POST"); replace(result.employee); setIssued(result); }); };

  return <div className={s.page}><div className={s.titleRow}><div><span className={s.eyebrow}>Quản trị</span><h1>Nhân viên & phân quyền</h1><p className={s.muted}>Tạo tài khoản, xếp phòng ban và chức vụ, khóa quyền truy cập của nhân viên nội bộ.</p></div><button className={ui.primaryButton} disabled={busy || departments.length === 0} onClick={() => setForm({ employee: null })}><UserPlus size={18}/>Tạo tài khoản</button></div>
    <div style={{ marginBottom: 24 }}><SummaryCards isLoading={busy} items={[
      { title: "Tài khoản nhân viên", value: rows.length, icon: <UsersRound size={24}/>, iconWrapperClassName: "bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-400" },
      { title: "Đang hoạt động", value: rows.length - locked, icon: <UserCheck size={24}/>, iconWrapperClassName: "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-400" },
      { title: "Đã khóa", value: locked, icon: <UserX size={24}/>, iconWrapperClassName: "bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-400" },
    ]}/></div>
    {error && <p role="alert" className={ui.error} style={{ marginBottom: 16 }}>{error}</p>}
    {notice && <p role="status" className={ui.notice} style={{ marginBottom: 16 }}>{notice}</p>}
    <section className={ui.card}><div className={s.toolbar}><input aria-label="Tìm nhân viên" className={ui.field} placeholder="Tìm tên, tên đăng nhập, mã, email hoặc SĐT…" value={search} onChange={e => setSearch(e.target.value)}/>
      <select aria-label="Lọc phòng ban" className={ui.field} style={{ maxWidth: 220 }} value={department} onChange={e => setDepartment(e.target.value)}><option value="">Tất cả phòng ban</option>{departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}</select>
      <select aria-label="Lọc trạng thái" className={ui.field} style={{ maxWidth: 220 }} value={status} onChange={e => setStatus(e.target.value)}><option value="">Tất cả trạng thái</option><option value="ACTIVE">Đang hoạt động</option><option value="LOCKED">Đã khóa</option></select>
      <button className={ui.secondaryButton} disabled={busy} onClick={() => { setBusy(true); setError(""); setReload(reload + 1); }} aria-label="Làm mới"><RefreshCw size={16}/></button></div>
      {busy ? <div className={s.empty}>Đang tải danh sách nhân viên…</div> : shown.length === 0 ? <div className={s.empty}><UsersRound size={42} strokeWidth={1.3}/><strong>{search || status || department ? "Không tìm thấy nhân viên phù hợp" : "Chưa có tài khoản nhân viên"}</strong><p>{search || status || department ? "Thử đổi từ khóa hoặc bộ lọc." : "Tạo tài khoản đầu tiên và xếp nhân viên vào phòng ban."}</p></div>
      : <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Nhân viên</th><th>Liên hệ</th><th>Phòng ban · Chức vụ</th><th>Trạng thái</th><th>Đăng nhập gần nhất</th><th><span className="sr-only">Thao tác</span></th></tr></thead>
        <tbody>{shown.map(r => { const self = r.id === user.id; const reason = self ? "Không thể tự thao tác trên tài khoản của chính mình" : undefined; const name = departmentName(r.departmentId);
          return <tr key={r.id}><td><strong>{r.name}</strong>{self && <span className={s.muted}> (bạn)</span>}<div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{r.username} · {r.code}</div></td>
            <td>{r.email ?? <span className={s.muted}>Chưa có email</span>}<div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{r.phone ?? "Chưa có SĐT"}</div></td>
            <td>{name ? <><span>{name}</span><div style={{ marginTop: 4 }}><span className={s.badge}>{r.isManager ? "Quản lý" : "Nhân viên"}</span></div></> : <span className={`${s.badge} ${s.warning}`}>Chưa xếp phòng ban</span>}</td>
            <td><span className={`${s.badge} ${r.status === "ACTIVE" ? "" : s.warning}`}>{r.status === "ACTIVE" ? "Đang hoạt động" : "Đã khóa"}</span>{r.mustChangePassword && <div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>Chờ đổi mật khẩu tạm</div>}</td>
            <td className={s.muted}>{r.lastLoginAt ? dateTime(r.lastLoginAt) : "Chưa đăng nhập"}</td>
            <td><div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
              <button className={ui.secondaryButton} disabled={acting || self} title={reason} onClick={() => setForm({ employee: r })}>Đổi phòng ban</button>
              <button className={ui.secondaryButton} disabled={acting || self} title={reason} onClick={() => reset(r)}>Cấp mật khẩu tạm</button>
              <button className={ui.secondaryButton} disabled={acting || self} title={reason} onClick={() => toggle(r)}>{r.status === "ACTIVE" ? "Khóa" : "Mở khóa"}</button>
            </div></td></tr>; })}</tbody></table></div>}
    </section><p className={s.muted} style={{ marginTop: 14 }}>Phòng ban và chức vụ quyết định màn hình nhân viên được dùng; thay đổi có hiệu lực ngay ở thao tác kế tiếp. Mỗi phòng có một quản lý. Bạn không thể đổi phòng ban, khóa hay cấp lại mật khẩu cho chính mình.</p>
    {form && <EmployeeFormDialog employee={form.employee} departments={departments} onClose={() => setForm(null)}
      onSaved={result => { setForm(null); refreshDepartments(); if ("temporaryPassword" in result) { replace(result.employee); setIssued(result); } else { replace(result); setNotice(`Đã cập nhật phòng ban của ${result.username}.`); } }}/>}
    {issued && <TemporaryPasswordDialog result={issued} onClose={() => setIssued(null)}/>}
  </div>;
}
