"use client";
import { useEffect, useState } from "react";
import { Building2, RefreshCw, UserCheck, UsersRound } from "lucide-react";
import { SummaryCards } from "@/components/ui/custom/summary-cards";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import { DepartmentFormDialog } from "@/components/internal/catalog-dialogs";
import s from "@/components/internal/workspace.module.css";
import { internalApi, type DepartmentRow } from "@/services/internal-api";

export default function DepartmentsPage() {
  const [rows, setRows] = useState<DepartmentRow[]>([]); const [error, setError] = useState(""); const [notice, setNotice] = useState("");
  const [busy, setBusy] = useState(true); const [reload, setReload] = useState(0); const [editing, setEditing] = useState<DepartmentRow | null>(null);
  useEffect(() => { let active = true;
    void internalApi<DepartmentRow[]>("/org/departments").then(list => { if (active) setRows(list); })
      .catch(e => { if (active) setError(e.message); }).finally(() => { if (active) setBusy(false); });
    return () => { active = false; }; }, [reload]);
  const members = rows.reduce((sum, r) => sum + r.memberCount, 0);

  return <div className={s.page}><div className={s.titleRow}><div><span className={s.eyebrow}>Quản trị</span><h1>Phòng ban</h1><p className={s.muted}>Mỗi phòng có một quản lý; phòng ban quyết định màn hình nhân viên được dùng.</p></div>
    <button className={ui.secondaryButton} disabled={busy} onClick={() => { setBusy(true); setError(""); setReload(reload + 1); }} aria-label="Làm mới"><RefreshCw size={16}/></button></div>
    <div style={{ marginBottom: 24 }}><SummaryCards isLoading={busy} items={[
      { title: "Phòng ban", value: rows.length, icon: <Building2 size={24}/>, iconWrapperClassName: "bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-400" },
      { title: "Đã có quản lý", value: rows.filter(r => r.managerName).length, icon: <UserCheck size={24}/>, iconWrapperClassName: "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-400" },
      { title: "Nhân viên đã xếp phòng", value: members, icon: <UsersRound size={24}/>, iconWrapperClassName: "bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-400" },
    ]}/></div>
    {error && <p role="alert" className={ui.error} style={{ marginBottom: 16 }}>{error}</p>}
    {notice && <p role="status" className={ui.notice} style={{ marginBottom: 16 }}>{notice}</p>}
    <section className={ui.card}>
      {busy ? <div className={s.empty}>Đang tải phòng ban…</div> : rows.length === 0 ? <div className={s.empty}><Building2 size={42} strokeWidth={1.3}/><strong>Chưa có phòng ban</strong><p>Chạy script 005-department-roles.sql để tạo các phòng ban.</p></div>
      : <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Phòng ban</th><th>Quản lý</th><th>Nhân viên</th><th>Chức vụ</th><th>Trạng thái</th><th><span className="sr-only">Thao tác</span></th></tr></thead>
        <tbody>{rows.map(r => <tr key={r.id}><td><strong>{r.name}</strong><div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{r.code}{r.description ? ` · ${r.description}` : ""}</div></td>
          <td>{r.managerName ?? <span className={`${s.badge} ${s.warning}`}>Chưa có quản lý</span>}</td>
          <td>{r.memberCount}</td>
          <td><div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>{r.staffRoleName && <span className={s.badge}>{r.staffRoleName}</span>}<span className={s.badge}>{r.managerRoleName}</span></div></td>
          <td><span className={`${s.badge} ${r.status === "ACTIVE" ? "" : s.warning}`}>{r.status === "ACTIVE" ? "Đang hoạt động" : "Ngừng hoạt động"}</span></td>
          <td><div style={{ display: "flex", justifyContent: "flex-end" }}><button className={ui.secondaryButton} onClick={() => setEditing(r)}>Sửa</button></div></td></tr>)}</tbody></table></div>}
    </section>
    {editing && <DepartmentFormDialog department={editing} onClose={() => setEditing(null)}
      onSaved={row => { setEditing(null); setRows(list => list.map(r => r.id === row.id ? row : r)); setNotice(`Đã cập nhật ${row.name}.`); }}/>}
  </div>;
}
