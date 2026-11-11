"use client";
import { useEffect, useState } from "react";
import { Archive, RefreshCw, Files } from "lucide-react";
import { useInternalUser } from "@/components/internal/internal-shell";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import { internalApi, dateTime, documentStatusLabel, type ContractArchiveEntry } from "@/services/internal-api";

export default function ContractArchivePage() {
  const user = useInternalUser();
  const [rows, setRows] = useState<ContractArchiveEntry[]>([]);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(true);
  const [reload, setReload] = useState(0);

  useEffect(() => {
    if (!user.canManageAll) return;
    let active = true;
    const timer = setTimeout(() => {
      setBusy(true);
      setError("");
      const params = new URLSearchParams();
      if (search.trim()) params.set("search", search.trim());
      if (status) params.set("status", status);
      void internalApi<ContractArchiveEntry[]>(`/contract-archive?${params}`)
        .then(data => { if (active) setRows(data); })
        .catch(e => { if (active) setError(e.message); })
        .finally(() => { if (active) setBusy(false); });
    }, 250);
    return () => { active = false; clearTimeout(timer); };
  }, [user.canManageAll, search, status, reload]);

  if (!user.canManageAll) return <div className={s.page}><p role="alert" className={ui.error}>Kho hợp đồng chỉ dành cho quản trị viên công ty.</p></div>;

  return <div className={s.page}>
    <div className={s.titleRow}><div><span className={s.eyebrow}>Quản lý công ty</span><h1>Kho hợp đồng</h1><p className={s.muted}>Theo dõi trạng thái, phiên bản và chữ ký của hồ sơ trên cơ sở dữ liệu công ty.</p></div></div>
    {error && <p role="alert" className={ui.error} style={{ marginBottom: 16 }}>{error}</p>}
    <section className={ui.card}>
      <div className={s.toolbar}>
        <input aria-label="Tìm hợp đồng" className={ui.field} placeholder="Tìm tên, mã hợp đồng hoặc khách hàng…" value={search} onChange={e => setSearch(e.target.value)} />
        <select aria-label="Lọc trạng thái" className={ui.field} style={{ maxWidth: 220 }} value={status} onChange={e => setStatus(e.target.value)}>
          <option value="">Tất cả trạng thái</option>{Object.entries(documentStatusLabel).map(([value, label]) => <option value={value} key={value}>{label}</option>)}
        </select>
        <button className={ui.secondaryButton} disabled={busy} onClick={() => setReload(v => v + 1)} aria-label="Làm mới"><RefreshCw size={16}/></button>
      </div>
      {busy ? <div className={s.empty}>Đang tải kho hợp đồng…</div> : rows.length === 0 ? <div className={s.empty}><Files size={42} strokeWidth={1.3}/><strong>{search || status ? "Không tìm thấy hợp đồng phù hợp" : "Chưa có hồ sơ trong kho"}</strong><p>{search || status ? "Thử đổi từ khóa hoặc bộ lọc." : "Hồ sơ sẽ xuất hiện tại đây sau khi được tạo trong không gian nội bộ."}</p></div> :
        <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Hợp đồng</th><th>Khách hàng</th><th>Người phụ trách</th><th>Trạng thái</th><th>Phiên bản</th><th>Chữ ký</th><th>Cập nhật</th></tr></thead><tbody>
          {rows.map(row => <tr key={row.id}><td>{row.name}<div className={s.muted} style={{ fontSize: 10, marginTop: 4 }}>{row.code}</div></td><td>{row.customerName}</td><td>{row.employeeName}</td><td><span className={s.badge}>{documentStatusLabel[row.status] ?? row.status}</span></td><td>v{row.versionNo}{row.versionLocked && <div className={s.muted}>Đã khóa</div>}</td><td>{row.signatureCount}{row.lastSignedAt && <div className={s.muted}>{dateTime(row.lastSignedAt)}</div>}</td><td className={s.muted}>{dateTime(row.updatedAt)}</td></tr>)}
        </tbody></table></div>}
    </section>
    <p className={s.muted} style={{ marginTop: 14 }}><Archive size={14} style={{ display: "inline", verticalAlign: "-2px", marginRight: 6 }}/>Hiển thị tối đa 200 hồ sơ của công ty.</p>
  </div>;
}
