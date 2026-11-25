"use client";

import { useEffect, useMemo, useState } from "react";
import { ArrowDownToLine, BarChart3, CalendarRange, FileText, RefreshCw, Search } from "lucide-react";
import { useInternalUser } from "@/components/internal/internal-shell";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import { documentStatusLabel, internalApi, money, type ContractArchiveEntry } from "@/services/internal-api";

const dayInput = (date: Date) => {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 10);
};
const initialFrom = () => { const date = new Date(); date.setDate(date.getDate() - 29); return dayInput(date); };
const initialTo = () => dayInput(new Date());
const PAGE_SIZE = 10;
const dayStart = (value: string) => value ? new Date(`${value}T00:00:00`).toISOString() : "";
const dayEnd = (value: string) => value ? new Date(`${value}T23:59:59.999`).toISOString() : "";
const dateText = (value: string | null) => value ? new Date(value.endsWith("Z") ? value : `${value}Z`).toLocaleDateString("vi-VN") : "—";

function saveCsv(rows: ContractArchiveEntry[]) {
  const cols: Array<[string, (row: ContractArchiveEntry) => string | number]> = [
    ["Mã hợp đồng", row => row.code], ["Tên hợp đồng", row => row.name], ["Khách hàng", row => row.customerName],
    ["Người phụ trách", row => row.employeeName], ["Trạng thái", row => documentStatusLabel[row.status] ?? row.status],
    ["Giá trị", row => row.totalAmount], ["Tiền tệ", row => row.currencyCode], ["Ngày tạo", row => dateText(row.createdAt)],
    ["Ngày hiệu lực", row => dateText(row.effectiveDate)], ["Ngày hết hạn", row => dateText(row.expireDate)],
  ];
  const quote = (value: string | number) => `"${String(value).replace(/"/g, '""')}"`;
  const csv = [cols.map(([name]) => quote(name)).join(","), ...rows.map(row => cols.map(([, get]) => quote(get(row))).join(","))].join("\r\n");
  const url = URL.createObjectURL(new Blob(["\uFEFF", csv], { type: "text/csv;charset=utf-8" }));
  const anchor = document.createElement("a"); anchor.href = url; anchor.download = `bao-cao-hop-dong-${dayInput(new Date())}.csv`; anchor.click(); URL.revokeObjectURL(url);
}

export default function ContractReportsPage() {
  const user = useInternalUser();
  const [rows, setRows] = useState<ContractArchiveEntry[]>([]);
  const [from, setFrom] = useState(initialFrom);
  const [to, setTo] = useState(initialTo);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [party, setParty] = useState("");
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState("");
  const [reload, setReload] = useState(0);
  const [page, setPage] = useState(1);

  useEffect(() => {
    if (!user.canManageAll) return;
    if (from && to && from > to) { setError("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc."); setBusy(false); return; }
    let active = true;
    const timer = setTimeout(() => {
      setBusy(true); setError("");
      const params = new URLSearchParams({ limit: "5000" });
      if (search.trim()) params.set("search", search.trim());
      if (status) params.set("status", status);
      if (party) params.set("partyType", party);
      if (from) params.set("createdFrom", dayStart(from));
      if (to) params.set("createdTo", dayEnd(to));
      void internalApi<ContractArchiveEntry[]>(`/contract-archive?${params}`)
        .then(data => { if (active) { setRows(data); setPage(1); } })
        .catch(e => { if (active) setError((e as Error).message); })
        .finally(() => { if (active) setBusy(false); });
    }, 180);
    return () => { active = false; clearTimeout(timer); };
  }, [user.canManageAll, from, to, search, status, party, reload]);

  const statusTotals = useMemo(() => Object.entries(documentStatusLabel).map(([key, label]) => ({
    key: Number(key), label, count: rows.filter(row => row.status === Number(key)).length,
  })).filter(item => item.count > 0), [rows]);
  const amounts = useMemo(() => {
    const totals = new Map<string, number>();
    rows.forEach(row => totals.set(row.currencyCode || "VND", (totals.get(row.currencyCode || "VND") ?? 0) + row.totalAmount));
    return [...totals.entries()];
  }, [rows]);
  const pageCount = Math.max(1, Math.ceil(rows.length / PAGE_SIZE));
  const visibleRows = rows.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  const activeStatuses = new Set([3, 4]);
  const expired = rows.filter(row => row.expireDate && new Date(row.expireDate) < new Date() && activeStatuses.has(row.status)).length;
  const expiring = rows.filter(row => {
    if (!row.expireDate || !activeStatuses.has(row.status)) return false;
    const days = (new Date(row.expireDate).getTime() - Date.now()) / 86_400_000;
    return days >= 0 && days <= 30;
  }).length;

  if (!user.canManageAll) return <div className={s.page}><p role="alert" className={ui.error}>Báo cáo toàn công ty chỉ dành cho Ban Giám Đốc.</p></div>;

  return <div className={s.page}>
    <div className={s.titleRow}><div><span className={s.eyebrow}>Ban Giám Đốc · Báo cáo điều hành</span><h1>Báo cáo tình trạng hợp đồng</h1><p className={s.muted}>Theo dõi danh mục, tiến độ, giá trị và thời hạn hợp đồng theo ngày tạo.</p></div></div>
    <section className={ui.card}>
      <div className={s.toolbar}>
        <label className={s.muted}>Từ ngày tạo <input aria-label="Từ ngày tạo" className={ui.field} type="date" value={from} onChange={e => setFrom(e.target.value)} /></label>
        <label className={s.muted}>Đến ngày tạo <input aria-label="Đến ngày tạo" className={ui.field} type="date" value={to} onChange={e => setTo(e.target.value)} /></label>
        <label className={s.muted}><Search size={14} style={{ verticalAlign: "-2px" }} /> Tìm kiếm <input aria-label="Tìm hợp đồng" className={ui.field} placeholder="Mã, tên hoặc khách hàng" value={search} onChange={e => setSearch(e.target.value)} /></label>
        <select aria-label="Lọc trạng thái" className={ui.field} value={status} onChange={e => setStatus(e.target.value)}><option value="">Tất cả trạng thái</option>{Object.entries(documentStatusLabel).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select>
        <select aria-label="Lọc loại khách hàng" className={ui.field} value={party} onChange={e => setParty(e.target.value)}><option value="">Mọi loại khách hàng</option><option value="PERSON">Khách cá nhân</option><option value="BUSINESS">Khách doanh nghiệp</option></select>
        <button className={ui.secondaryButton} disabled={busy} onClick={() => setReload(v => v + 1)}><RefreshCw size={15} /> Cập nhật</button>
        <button className={ui.secondaryButton} disabled={busy || rows.length === 0} onClick={() => saveCsv(rows)}><ArrowDownToLine size={15} /> Xuất CSV</button>
      </div>
      {error && <p role="alert" className={ui.error} style={{ marginTop: 12 }}>{error}</p>}
      {rows.length === 5000 && <p className={s.muted} style={{ marginTop: 12 }}>Kết quả đã chạm giới hạn 5.000 hồ sơ. Hãy thu hẹp khoảng thời gian hoặc bộ lọc để xem chính xác hơn.</p>}
    </section>

    <section style={{ display: "grid", gap: 12, gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))" }}>
      {[
        { label: "Tổng hợp đồng", value: rows.length, note: "Trong khoảng ngày tạo" },
        { label: "Chờ ký", value: rows.filter(row => row.status === 3).length, note: "Đang chờ hoàn tất chữ ký" },
        { label: "Sắp hết hạn", value: expiring, note: "Trong 30 ngày tới" },
        { label: "Đã quá hạn", value: expired, note: "Ngày hết hạn đã qua" },
      ].map(card => <section className={ui.card} key={card.label}><span className={s.muted}>{card.label}</span><div style={{ fontSize: 28, fontWeight: 750, margin: "8px 0 4px" }}>{busy ? "—" : card.value.toLocaleString("vi-VN")}</div><span className={s.muted}>{card.note}</span></section>)}
    </section>

    <section style={{ display: "grid", gap: 16, gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))" }}>
      <section className={ui.card}><h2 style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 16, margin: "0 0 14px" }}><BarChart3 size={18}/>Phân bổ trạng thái</h2>
        {statusTotals.length ? <div style={{ display: "grid", gap: 12 }}>{statusTotals.map(item => <div key={item.key}><div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 5 }}><span>{item.label}</span><strong>{item.count}</strong></div><div style={{ height: 7, borderRadius: 10, background: "var(--secondary)" }}><div style={{ width: `${Math.max(4, item.count / Math.max(1, rows.length) * 100)}%`, height: 7, borderRadius: 10, background: "var(--primary)" }}/></div></div>)}</div> : <p className={s.empty}>Không có hợp đồng trong khoảng đã chọn.</p>}
      </section>
      <section className={ui.card}><h2 style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 16, margin: "0 0 14px" }}><FileText size={18}/>Giá trị theo tiền tệ</h2>
        {amounts.length ? amounts.map(([currency, total]) => <div key={currency} style={{ display: "flex", justifyContent: "space-between", padding: "12px 0", borderBottom: "1px solid var(--border)" }}><span>{currency}</span><strong>{money(total)} {currency}</strong></div>) : <p className={s.empty}>Chưa có số liệu giá trị hợp đồng.</p>}
        <p className={s.muted} style={{ marginTop: 12 }}>Mỗi loại tiền tệ được tổng hợp riêng, không quy đổi.</p>
      </section>
    </section>

    <section className={ui.card}>
      <div className={s.titleRow} style={{ marginBottom: 12 }}><div><h2 style={{ fontSize: 17, margin: 0 }}>Danh sách hợp đồng</h2><p className={s.muted}>{rows.length.toLocaleString("vi-VN")} hồ sơ · tối đa 5.000 hồ sơ mỗi lượt</p></div><CalendarRange size={18}/></div>
      {busy ? <div className={s.empty}>Đang tải báo cáo…</div> : rows.length === 0 ? <div className={s.empty}>Không tìm thấy hợp đồng phù hợp với bộ lọc.</div> :
        <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Mã / Hợp đồng</th><th>Khách hàng</th><th>Người phụ trách</th><th>Trạng thái</th><th>Giá trị</th><th>Ngày tạo</th><th>Hiệu lực</th><th>Hết hạn</th></tr></thead><tbody>{visibleRows.map(row => <tr key={row.id}>
          <td><strong>{row.code || `#${row.id}`}</strong><div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{row.name}</div></td><td>{row.customerName}</td><td>{row.employeeName}</td><td><span className={s.badge}>{documentStatusLabel[row.status] ?? row.status}</span></td><td>{money(row.totalAmount)} {row.currencyCode}</td><td>{dateText(row.createdAt)}</td><td>{dateText(row.effectiveDate)}</td><td>{dateText(row.expireDate)}</td>
        </tr>)}</tbody></table></div>}
      {rows.length > PAGE_SIZE && <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: 14 }}><span className={s.muted}>Trang {page} / {pageCount}</span><div style={{ display: "flex", gap: 8 }}><button className={ui.secondaryButton} disabled={page <= 1} onClick={() => setPage(value => value - 1)}>Trước</button><button className={ui.secondaryButton} disabled={page >= pageCount} onClick={() => setPage(value => value + 1)}>Sau</button></div></div>}
    </section>
  </div>;
}
