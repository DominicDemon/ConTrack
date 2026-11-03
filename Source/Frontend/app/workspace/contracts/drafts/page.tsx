"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { FilePlus2, Files, FileCheck2, FilePenLine, RefreshCw, ArrowUpRight } from "lucide-react";
import { useInternalUser } from "@/components/internal/internal-shell";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import { internalApi, dateTime, type DraftSummary, type EmployeeOption } from "@/services/internal-api";
export default function DraftList() {
  const user = useInternalUser(); const [rows, setRows] = useState<DraftSummary[]>([]); const [employees, setEmployees] = useState<EmployeeOption[]>([]);
  const [search, setSearch] = useState(""); const [owner, setOwner] = useState(""); const [error, setError] = useState(""); const [busy, setBusy] = useState(true); const [reload, setReload] = useState(0);
  useEffect(() => { if (user.canManageAll) void internalApi<EmployeeOption[]>("/employees").then(setEmployees).catch(e => setError(e.message)); }, [user.canManageAll]);
  useEffect(() => { let active = true; const timer = setTimeout(() => { setBusy(true); setError(""); const params = new URLSearchParams(); if (search.trim()) params.set("search", search.trim()); if (owner) params.set("ownerId", owner);
    void internalApi<DraftSummary[]>(`/contract-drafts?${params}`).then(data => { if (active) setRows(data); }).catch(e => { if (active) setError(e.message); }).finally(() => { if (active) setBusy(false); });
  }, 250); return () => { active = false; clearTimeout(timer); }; }, [search, owner, reload]);
  const complete = rows.filter(r => r.missingCount === 0).length;
  return <div className={s.page}><div className={s.titleRow}><div><span className={s.eyebrow}>Hồ sơ hợp đồng</span><h1>Hợp đồng nháp</h1><p className={s.muted}>Bắt đầu từ mẫu và hoàn thiện nội dung cùng đội ngũ của bạn.</p></div><Link href="/workspace/contracts/drafts/new" className={ui.primaryButton}><FilePlus2 size={18}/>Tạo hợp đồng nháp</Link></div>
    <div className={s.stats}>{[[Files, rows.length, "Bản nháp trong kết quả"], [FilePenLine, rows.length - complete, "Cần bổ sung nội dung"], [FileCheck2, complete, "Đã đủ trường thông tin"]].map(([Icon, n, label], i) => { const Component = Icon as typeof Files; return <div className={s.stat} key={i}><Component size={29} strokeWidth={1.5}/><div><strong>{n as number}</strong><span className={s.muted}>{label as string}</span></div></div>; })}</div>
    {error && <p role="alert" className={ui.error} style={{ marginBottom: 16 }}>{error}</p>}
    <section className={ui.card}><div className={s.toolbar}><input aria-label="Tìm hợp đồng nháp" className={ui.field} placeholder="Tìm tên hợp đồng, mã hoặc khách hàng…" value={search} onChange={e => setSearch(e.target.value)}/>
      {user.canManageAll && <select aria-label="Lọc người phụ trách" className={ui.field} style={{ maxWidth: 240 }} value={owner} onChange={e => setOwner(e.target.value)}><option value="">Tất cả người phụ trách</option>{employees.map(e => <option value={e.id} key={e.id}>{e.name}</option>)}</select>}
      <button className={ui.secondaryButton} disabled={busy} onClick={() => setReload(reload + 1)} aria-label="Làm mới"><RefreshCw size={16}/></button></div>
      {busy ? <div className={s.empty}>Đang tải bản nháp…</div> : rows.length === 0 ? <div className={s.empty}><Files size={42} strokeWidth={1.3}/><strong>{search || owner ? "Không tìm thấy bản nháp phù hợp" : "Bắt đầu với hợp đồng đầu tiên"}</strong><p>{search || owner ? "Thử đổi từ khóa hoặc bộ lọc." : "Chọn mẫu, điền thông tin và lưu lại để tiếp tục bất cứ lúc nào."}</p><Link className={ui.secondaryButton} href="/workspace/contracts/drafts/new">Tạo bản nháp</Link></div> : <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Hợp đồng</th><th>Khách hàng</th><th>Người phụ trách</th><th>Hoàn thiện</th><th>Cập nhật</th><th><span className="sr-only">Mở</span></th></tr></thead><tbody>{rows.map(r => <tr key={r.id}><td><Link href={`/workspace/contracts/drafts/${r.id}`}>{r.name}</Link><div className={s.muted} style={{ fontSize: 10, marginTop: 4 }}>{r.code}</div></td><td>{r.customerName}</td><td>{r.employeeName}</td><td><span className={`${s.badge} ${r.missingCount ? s.warning : ""}`}>{r.missingCount ? `Còn thiếu ${r.missingCount} mục` : "Đủ thông tin"}</span></td><td className={s.muted}>{dateTime(r.updatedAt)}</td><td><Link href={`/workspace/contracts/drafts/${r.id}`} aria-label={`Mở ${r.name}`}><ArrowUpRight size={18}/></Link></td></tr>)}</tbody></table></div>}
    </section><p className={s.muted} style={{ marginTop: 14 }}>Hiển thị tối đa 200 bản nháp theo bộ lọc. “Đủ thông tin” chưa có nghĩa là hợp đồng đã được phê duyệt.</p>
  </div>;
}
