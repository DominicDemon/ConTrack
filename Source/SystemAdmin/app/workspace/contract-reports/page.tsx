"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { AlarmClock, ArrowDownToLine, ArrowUpRight, BarChart3, Coins, FileText, HandCoins, Trophy, TrendingUp, Wallet } from "lucide-react";
import { Bar, BarChart, CartesianGrid, XAxis, YAxis } from "recharts";
import { useInternalUser } from "@/components/internal/internal-shell";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import { SummaryCards } from "@/components/ui/custom/summary-cards";
import { ChartContainer, ChartTooltip, ChartTooltipContent, type ChartConfig } from "@/components/ui/chart";
import { documentStatusLabel, internalApi, money, type ContractArchiveEntry } from "@/services/internal-api";
import { ListPager } from "@/components/scanned-contracts/list-pager";
import { SortHeader, searchText, useListView } from "@/components/internal/list-tools";

const dayInput = (date: Date) => new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 10);
const LIMIT = 5000;
const dayStart = (value: string) => value ? new Date(`${value}T00:00:00`).toISOString() : "";
const dayEnd = (value: string) => value ? new Date(`${value}T23:59:59.999`).toISOString() : "";
const asDate = (value: string) => new Date(value.endsWith("Z") || value.length === 10 ? value : `${value}Z`);
const dateText = (value: string | null) => value ? asDate(value).toLocaleDateString("vi-VN") : "—";
/** One currency is added up: VND, the one contracts are written in. Others are listed apart, never converted. */
const MAIN_CURRENCY = "VND";
const isMain = (row: ContractArchiveEntry) => (row.currencyCode || MAIN_CURRENCY) === MAIN_CURRENCY;
const compactMoney = (value: number) => value >= 1e9 ? `${(value / 1e9).toLocaleString("vi-VN", { maximumFractionDigits: 2 })} tỷ`
  : value >= 1e6 ? `${(value / 1e6).toLocaleString("vi-VN", { maximumFractionDigits: 1 })} triệu` : money(value);
const percent = (part: number, whole: number) => whole > 0 ? Math.round(part / whole * 100) : 0;

/** Quick periods; "custom" shows the two date fields. */
const PRESETS = [["30d", "30 ngày qua"], ["month", "Tháng này"], ["quarter", "Quý này"], ["year", "Năm nay"], ["12m", "12 tháng qua"], ["all", "Toàn bộ"], ["custom", "Tùy chọn ngày"]] as const;
type Preset = (typeof PRESETS)[number][0];
function presetRange(preset: Preset): [string, string] {
  const now = new Date(), today = dayInput(now);
  const back = (days: number) => { const d = new Date(now); d.setDate(d.getDate() - days); return dayInput(d); };
  switch (preset) {
    case "30d": return [back(29), today];
    case "month": return [dayInput(new Date(now.getFullYear(), now.getMonth(), 1)), today];
    case "quarter": return [dayInput(new Date(now.getFullYear(), Math.floor(now.getMonth() / 3) * 3, 1)), today];
    case "year": return [dayInput(new Date(now.getFullYear(), 0, 1)), today];
    case "12m": return [dayInput(new Date(now.getFullYear(), now.getMonth() - 11, 1)), today];
    default: return ["", ""];
  }
}
type Grain = "auto" | "day" | "month" | "year";
const GRAIN_LABEL = { day: "ngày", month: "tháng", year: "năm" } as const;

const countConfig = { value: { label: "Hợp đồng", color: "var(--primary)" } } satisfies ChartConfig;
const moneyConfig = { value: { label: `Đã thu (${MAIN_CURRENCY})`, color: "var(--primary)" } } satisfies ChartConfig;

/** Values summed per day, month or year between two dates; periods without data stay at 0 so the axis is even. */
function buckets(dates: { at: Date; value: number }[], from: string, to: string, grain: Grain) {
  const times = dates.map(d => d.at.getTime());
  const start = from ? new Date(`${from}T00:00:00`) : times.length ? new Date(Math.min(...times)) : new Date();
  const end = to ? new Date(`${to}T00:00:00`) : times.length ? new Date(Math.max(...times)) : new Date();
  const span = (end.getTime() - start.getTime()) / 86_400_000;
  const unit: "day" | "month" | "year" = grain !== "auto" ? grain : span <= 62 ? "day" : span <= 3 * 366 ? "month" : "year";
  const key = (d: Date) => unit === "day" ? dayInput(d) : unit === "month" ? dayInput(d).slice(0, 7) : String(d.getFullYear());
  const label = (k: string) => unit === "day" ? `${k.slice(8, 10)}/${k.slice(5, 7)}` : unit === "month" ? `${k.slice(5, 7)}/${k.slice(0, 4)}` : k;
  const map = new Map<string, number>();
  const cursor = unit === "day" ? new Date(start) : unit === "month" ? new Date(start.getFullYear(), start.getMonth(), 1) : new Date(start.getFullYear(), 0, 1);
  for (let guard = 0; cursor <= end && guard < 2000; guard++) {
    map.set(key(cursor), 0);
    if (unit === "day") cursor.setDate(cursor.getDate() + 1); else if (unit === "month") cursor.setMonth(cursor.getMonth() + 1); else cursor.setFullYear(cursor.getFullYear() + 1);
  }
  dates.forEach(d => { const k = key(d.at); if (map.has(k)) map.set(k, map.get(k)! + d.value); });
  return { unit, points: [...map].map(([k, value]) => ({ key: k, label: label(k), value })) };
}

/** A ranked list of horizontal bars: one hue, longest first, the value and share written beside each bar. */
function RankedBars({ items, empty, format }: { items: { key: string; label: string; value: number; note?: string }[]; empty: string; format: (value: number) => string }) {
  if (!items.length) return <p className={s.muted} style={{ padding: "24px 0", textAlign: "center" }}>{empty}</p>;
  const max = Math.max(...items.map(item => item.value), 1);
  return <ol style={{ listStyle: "none", margin: 0, padding: 0, display: "grid", gap: 14 }}>
    {items.map(item => <li key={item.key} title={`${item.label}: ${format(item.value)}${item.note ? ` · ${item.note}` : ""}`}>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 12, fontSize: 13, marginBottom: 6 }}>
        <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{item.label}</span>
        <span style={{ whiteSpace: "nowrap" }}><strong>{format(item.value)}</strong>{item.note && <span className={s.muted}> · {item.note}</span>}</span>
      </div>
      <div style={{ height: 8, borderRadius: 4, background: "var(--secondary)" }} aria-hidden="true">
        <div style={{ width: `${Math.max(2, item.value / max * 100)}%`, height: 8, borderRadius: 4, background: "var(--primary)" }}/>
      </div>
    </li>)}
  </ol>;
}

/** Share received as a thin bar with the percentage beside it. */
function Collected({ paid, scheduled }: { paid: number; scheduled: number }) {
  const share = percent(paid, scheduled);
  return <div style={{ display: "flex", alignItems: "center", gap: 8, minWidth: 120 }} title={`Đã thu ${share}%`}>
    <div style={{ flex: 1, height: 6, borderRadius: 3, background: "var(--secondary)" }} aria-hidden="true"><div style={{ width: `${share}%`, height: 6, borderRadius: 3, background: "var(--primary)" }}/></div>
    <span style={{ fontSize: 12, minWidth: 34, textAlign: "right" }}>{share}%</span>
  </div>;
}

function TimeChart({ title, icon, note, data, config, unit, format, ariaLabel }: { title: string; icon: React.ReactNode; note: string; data: { label: string; value: number }[];
  config: ChartConfig; unit: "day" | "month" | "year"; format: (value: number) => string; ariaLabel: string }) {
  const empty = data.every(point => point.value === 0);
  return <section className={ui.card}>
    <h2 style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 16, margin: "0 0 4px" }}>{icon}{title} theo {GRAIN_LABEL[unit]}</h2>
    <p className={s.muted} style={{ margin: "0 0 12px" }}>{note}</p>
    {empty ? <div className={s.empty}><BarChart3 size={36} strokeWidth={1.3}/><strong>Chưa có số liệu trong khoảng này</strong><p>Thử mở rộng khoảng thời gian hoặc bỏ bớt bộ lọc.</p></div>
    : <ChartContainer config={config} className="h-[240px] w-full" aria-label={ariaLabel}>
      <BarChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }} barCategoryGap={data.length > 20 ? 2 : "20%"}>
        <CartesianGrid vertical={false} strokeDasharray="3 3"/>
        <XAxis dataKey="label" tickLine={false} axisLine={false} tickMargin={8} minTickGap={16}/>
        <YAxis allowDecimals={false} tickLine={false} axisLine={false} width={56} tickFormatter={value => compactMoney(Number(value)).replace(" triệu", "tr").replace(" tỷ", "tỷ")}/>
        <ChartTooltip cursor={{ fill: "var(--secondary)" }} content={<ChartTooltipContent formatter={value => <span><strong>{format(Number(value))}</strong></span>}
          labelFormatter={label => `${unit === "day" ? "Ngày" : unit === "month" ? "Tháng" : "Năm"} ${label}`}/>}/>
        <Bar dataKey="value" fill="var(--color-value)" radius={[4, 4, 0, 0]} maxBarSize={40}/>
      </BarChart>
    </ChartContainer>}
  </section>;
}

type MoneyRow = { key: string; label: string; sub?: string; count: number; value: number; scheduled: number; paid: number; overdue: number; href: string };
const TABS = [["contract", "Theo hợp đồng"], ["customer", "Theo khách hàng"], ["employee", "Theo nhân viên"], ["month", "Theo tháng"]] as const;
type Tab = (typeof TABS)[number][0];
const PAY_STATES = [["", "Mọi tình trạng thu"], ["PAID", "Đã thu đủ"], ["PARTIAL", "Thu một phần"], ["UNPAID", "Chưa thu"], ["OVERDUE", "Có đợt quá hạn"]] as const;
const payState = (row: ContractArchiveEntry) => row.payments.paid >= row.payments.scheduled ? "PAID" : row.payments.paid > 0 ? "PARTIAL" : "UNPAID";

function saveCsv(rows: ContractArchiveEntry[]) {
  const cols: Array<[string, (row: ContractArchiveEntry) => string | number]> = [
    ["Mã hợp đồng", row => row.code], ["Tên hợp đồng", row => row.name], ["Khách hàng", row => row.customerName],
    ["Người phụ trách", row => row.employeeName], ["Trạng thái", row => documentStatusLabel[row.status] ?? row.status],
    ["Giá trị", row => row.totalAmount], ["Tiền tệ", row => row.currencyCode], ["Đã thu", row => row.payments.paid],
    ["Còn phải thu", row => row.payments.scheduled - row.payments.paid], ["Quá hạn", row => row.payments.overdue],
    ["Đợt đã thu", row => `${row.payments.paidInstallments}/${row.payments.installments}`], ["Hạn thu tiếp theo", row => dateText(row.payments.nextDue)],
    ["Ngày tạo", row => dateText(row.createdAt)], ["Ngày hiệu lực", row => dateText(row.effectiveDate)], ["Ngày hết hạn", row => dateText(row.expireDate)],
  ];
  const quote = (value: string | number) => `"${String(value).replace(/"/g, '""')}"`;
  const csv = [cols.map(([name]) => quote(name)).join(","), ...rows.map(row => cols.map(([, get]) => quote(get(row))).join(","))].join("\r\n");
  const url = URL.createObjectURL(new Blob(["﻿", csv], { type: "text/csv;charset=utf-8" }));
  const anchor = document.createElement("a"); anchor.href = url; anchor.download = `bao-cao-hop-dong-${dayInput(new Date())}.csv`; anchor.click(); URL.revokeObjectURL(url);
}

export default function ContractReportsPage() {
  // Ban Giám Đốc's report: real figures for a period — contracts made, money received and still to receive — and who and what they come from.
  const user = useInternalUser();
  const [rows, setRows] = useState<ContractArchiveEntry[]>([]);
  const [preset, setPreset] = useState<Preset>("year");
  const [[from, to], setRange] = useState<[string, string]>(() => presetRange("year"));
  const [grain, setGrain] = useState<Grain>("auto");
  const [search, setSearch] = useState("");
  const [party, setParty] = useState("");
  const [tab, setTab] = useState<Tab>("contract");
  const [pay, setPay] = useState("");
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState("");
  const badRange = !!from && !!to && from > to;
  // "Now" for overdue figures, read once when the page opens.
  const [now] = useState(() => Date.now());

  useEffect(() => {
    if (!user.canManageAll || badRange) return;
    let active = true;
    const timer = setTimeout(() => {
      setBusy(true); setError("");
      const params = new URLSearchParams({ limit: String(LIMIT) });
      if (party) params.set("partyType", party);
      if (from) params.set("createdFrom", dayStart(from));
      if (to) params.set("createdTo", dayEnd(to));
      void internalApi<ContractArchiveEntry[]>(`/contract-archive?${params}`)
        .then(data => { if (active) setRows(data); })
        .catch(e => { if (active) setError((e as Error).message); })
        .finally(() => { if (active) setBusy(false); });
    }, 250);
    return () => { active = false; clearTimeout(timer); };
  }, [user.canManageAll, from, to, party, badRange]);

  const report = useMemo(() => {
    const main = rows.filter(isMain);
    const sum = (list: ContractArchiveEntry[], get: (row: ContractArchiveEntry) => number) => list.reduce((total, row) => total + get(row), 0);
    const created = buckets(rows.map(row => ({ at: asDate(row.createdAt), value: 1 })), from, to, grain);
    const received = buckets(main.flatMap(row => row.payments.received.map(r => ({ at: asDate(r.paidAt), value: r.amount }))), from, to, grain);
    const byStatus = Object.entries(documentStatusLabel).map(([key, label]) => ({ key, label, value: rows.filter(row => row.status === Number(key)).length }))
      .filter(item => item.value > 0).sort((a, b) => b.value - a.value).map(item => ({ ...item, note: `${percent(item.value, rows.length)}%` }));
    const query = (extra: Record<string, string>) => "/workspace/contracts/drafts?" + new URLSearchParams({ ...(from ? { from } : {}), ...(to ? { to } : {}), ...extra });
    // "Thu tiền" counts only contracts with a payment schedule (signed ones). Its own search and collection filter
    // narrow every tab; the figures and charts above do not use them.
    const term = searchText(search);
    const listed = main.filter(row => row.payments.installments > 0
      && (!pay || (pay === "OVERDUE" ? row.payments.overdue > 0 : payState(row) === pay))
      && (!term || [row.code, row.name, row.customerName, row.employeeName].some(v => searchText(v).includes(term))));
    const group = (source: ContractArchiveEntry[], key: (row: ContractArchiveEntry) => string, label: (row: ContractArchiveEntry) => string, href: (row: ContractArchiveEntry) => string): MoneyRow[] => {
      const map = new Map<string, MoneyRow>();
      source.forEach(row => {
        const k = key(row); const g = map.get(k) ?? { key: k, label: label(row), count: 0, value: 0, scheduled: 0, paid: 0, overdue: 0, href: href(row) };
        g.count++; g.value += row.totalAmount; g.scheduled += row.payments.scheduled; g.paid += row.payments.paid; g.overdue += row.payments.overdue; map.set(k, g);
      });
      return [...map.values()];
    };
    const monthOf = (row: ContractArchiveEntry) => dayInput(asDate(row.createdAt)).slice(0, 7);
    const lastDay = (month: string) => dayInput(new Date(Number(month.slice(0, 4)), Number(month.slice(5, 7)), 0));
    const byEmployee = (source: ContractArchiveEntry[]) => group(source, row => String(row.employeeId), row => row.employeeName, row => query({ ownerId: String(row.employeeId) }));
    const people = byEmployee(main);
    return {
      count: rows.length, value: sum(main, row => row.totalAmount), scheduled: sum(main, row => row.payments.scheduled),
      paid: sum(main, row => row.payments.paid), overdue: sum(main, row => row.payments.overdue),
      created, received, byStatus,
      people: [...people].sort((a, b) => b.value - a.value).slice(0, 5).map(p => ({ key: p.key, label: p.label, value: p.value, note: `${p.count} hợp đồng` })),
      others: [...rows.filter(row => !isMain(row)).reduce((m, row) => m.set(row.currencyCode, (m.get(row.currencyCode) ?? 0) + row.totalAmount), new Map<string, number>())],
      billed: listed,
      customer: group(listed, row => row.customerName, row => row.customerName, row => query({ search: row.customerName })),
      employee: byEmployee(listed),
      month: group(listed, monthOf, row => { const m = monthOf(row); return `Tháng ${m.slice(5, 7)}/${m.slice(0, 4)}`; },
        row => { const m = monthOf(row); return "/workspace/contracts/drafts?" + new URLSearchParams({ from: `${m}-01`, to: lastDay(m) }); }),
    };
  }, [rows, from, to, grain, search, pay]);

  const contracts = useListView(report.billed, { search: "", fields: () => [], filterKey: [pay, from, to, party, search],
    sorts: { name: r => r.code || r.name, customer: r => r.customerName, employee: r => r.employeeName, value: r => r.totalAmount, paid: r => r.payments.paid,
      left: r => r.payments.scheduled - r.payments.paid, share: r => r.payments.scheduled ? r.payments.paid / r.payments.scheduled : 0, due: r => r.payments.nextDue },
    initial: { key: "left", dir: "desc" } });
  const groupSorts = { label: (g: MoneyRow) => g.label, count: (g: MoneyRow) => g.count, value: (g: MoneyRow) => g.value, paid: (g: MoneyRow) => g.paid,
    left: (g: MoneyRow) => g.scheduled - g.paid, overdue: (g: MoneyRow) => g.overdue, share: (g: MoneyRow) => g.scheduled ? g.paid / g.scheduled : 0 };
  const customers = useListView(report.customer, { search: "", fields: () => [], filterKey: [from, to, party, search, pay], sorts: groupSorts, initial: { key: "value", dir: "desc" } });
  const employees = useListView(report.employee, { search: "", fields: () => [], filterKey: [from, to, party, search, pay], sorts: groupSorts, initial: { key: "value", dir: "desc" } });
  const months = useListView(report.month, { search: "", fields: () => [], filterKey: [from, to, party, search, pay], sorts: { ...groupSorts, label: (g: MoneyRow) => g.key }, initial: { key: "label", dir: "desc" } });
  const groups = tab === "customer" ? customers : tab === "employee" ? employees : months;

  if (!user.canManageAll) return <div className={s.page}><p role="alert" className={ui.error}>Báo cáo toàn công ty chỉ dành cho Ban Giám Đốc.</p></div>;

  const loading = busy && !rows.length;
  const choosePreset = (next: Preset) => { setPreset(next); if (next !== "custom") setRange(presetRange(next)); };
  const manage = "/workspace/contracts/drafts?" + new URLSearchParams({ ...(from ? { from } : {}), ...(to ? { to } : {}) });
  const tone = (overdue: number) => overdue > 0 ? { color: "var(--destructive)", fontWeight: 600 } : undefined;
  const groupTitle = tab === "customer" ? "Khách hàng" : tab === "employee" ? "Nhân viên" : "Tháng tạo hợp đồng";
  const GS = (label: string, column: keyof typeof groupSorts, first: "asc" | "desc" = "desc", align?: "right") =>
    <SortHeader label={label} column={column} sort={groups.sort} onSort={groups.sortBy} firstDir={first} align={align}/>;
  const CS = (label: string, column: Parameters<typeof contracts.sortBy>[0], first: "asc" | "desc" = "desc", align?: "right") =>
    <SortHeader label={label} column={column} sort={contracts.sort} onSort={contracts.sortBy} firstDir={first} align={align}/>;

  return <div className={s.page}>
    <div className={s.titleRow}><div><span className={s.eyebrow}>Ban Giám Đốc · Báo cáo điều hành</span><h1>Báo cáo hợp đồng</h1>
      <p className={s.muted}>Số liệu thực tế của các hợp đồng tạo trong kỳ: số lượng, giá trị, tiền đã thu và còn phải thu.</p></div>
      <div className={s.actions}>
        <Link className={ui.secondaryButton} href={manage}><ArrowUpRight size={16}/>Xem trong Quản lý hợp đồng</Link>
        <button className={ui.secondaryButton} disabled={busy || rows.length === 0} onClick={() => saveCsv(rows)}><ArrowDownToLine size={16}/>Xuất CSV</button>
      </div></div>

    <section className={ui.card} style={{ marginBottom: 20 }}><div className={s.toolbar} style={{ marginBottom: 0 }}>
      <select aria-label="Khoảng thời gian" className={ui.field} style={{ maxWidth: 180 }} value={preset} onChange={e => choosePreset(e.target.value as Preset)}>
        {PRESETS.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select>
      {preset === "custom" && <>
        <input aria-label="Tạo từ ngày" title="Tạo từ ngày" className={ui.field} style={{ maxWidth: 160 }} type="date" value={from} max={to || undefined} aria-invalid={badRange} onChange={e => setRange([e.target.value, to])}/>
        <input aria-label="Tạo đến ngày" title="Tạo đến ngày" className={ui.field} style={{ maxWidth: 160 }} type="date" value={to} min={from || undefined} aria-invalid={badRange} onChange={e => setRange([from, e.target.value])}/></>}
      <select aria-label="Gom biểu đồ theo" className={ui.field} style={{ maxWidth: 170 }} value={grain} onChange={e => setGrain(e.target.value as Grain)}>
        <option value="auto">Biểu đồ: tự chọn</option><option value="day">Biểu đồ theo ngày</option><option value="month">Biểu đồ theo tháng</option><option value="year">Biểu đồ theo năm</option></select>
      <select aria-label="Lọc loại khách hàng" className={ui.field} style={{ maxWidth: 190 }} value={party} onChange={e => setParty(e.target.value)}><option value="">Mọi loại khách hàng</option><option value="PERSON">Khách cá nhân</option><option value="BUSINESS">Khách doanh nghiệp</option></select>
    </div>
      {badRange && <p role="alert" className={ui.error} style={{ marginTop: 12 }}>“Từ ngày” phải trước hoặc bằng “Đến ngày”.</p>}
      {error && <p role="alert" className={ui.error} style={{ marginTop: 12 }}>{error}</p>}
      {rows.length === LIMIT && <p className={s.muted} style={{ marginTop: 12 }}>Kết quả đã chạm giới hạn {LIMIT.toLocaleString("vi-VN")} hợp đồng. Thu hẹp khoảng thời gian hoặc bộ lọc để số liệu đầy đủ.</p>}
    </section>

    <div style={{ marginBottom: 20 }}><SummaryCards isLoading={loading} count={4} items={[
      { title: `Giá trị ${report.count.toLocaleString("vi-VN")} hợp đồng`, value: compactMoney(report.value), icon: <Coins size={24}/>, iconWrapperClassName: "bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-400" },
      { title: `Đã thu (${percent(report.paid, report.scheduled)}% lịch thu)`, value: compactMoney(report.paid), icon: <HandCoins size={24}/>, iconWrapperClassName: "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-400" },
      { title: "Còn phải thu", value: compactMoney(report.scheduled - report.paid), icon: <Wallet size={24}/>, iconWrapperClassName: "bg-violet-100 text-violet-700 dark:bg-violet-500/15 dark:text-violet-400" },
      { title: "Quá hạn chưa thu", value: compactMoney(report.overdue), icon: <AlarmClock size={24}/>, iconWrapperClassName: "bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-400" },
    ]}/></div>

    {loading ? <section className={ui.card} style={{ marginBottom: 20 }}><div className={s.empty}>Đang tải báo cáo…</div></section> : <>
      <div style={{ display: "grid", gap: 20, gridTemplateColumns: "repeat(auto-fit, minmax(min(100%, 420px), 1fr))", marginBottom: 20 }}>
        <TimeChart title="Hợp đồng tạo mới" icon={<TrendingUp size={18}/>} note="Số hợp đồng tạo trong mỗi kỳ. Di chuột lên cột để xem số liệu." data={report.created.points}
          config={countConfig} unit={report.created.unit} format={value => `${value.toLocaleString("vi-VN")} hợp đồng`} ariaLabel="Biểu đồ số hợp đồng tạo mới theo thời gian"/>
        <TimeChart title="Tiền đã thu" icon={<HandCoins size={18}/>} note={`Các đợt thanh toán Hành chính đã xác nhận, theo ngày nhận tiền (${MAIN_CURRENCY}).`} data={report.received.points}
          config={moneyConfig} unit={report.received.unit} format={value => `${money(value)} ${MAIN_CURRENCY}`} ariaLabel="Biểu đồ tiền đã thu theo thời gian"/>
      </div>
      <div style={{ display: "grid", gap: 20, gridTemplateColumns: "repeat(auto-fit, minmax(min(100%, 340px), 1fr))", marginBottom: 20 }}>
        <section className={ui.card}><h2 style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 16, margin: "0 0 16px" }}><BarChart3 size={18}/>Phân bổ trạng thái</h2>
          <RankedBars items={report.byStatus} empty="Không có hợp đồng trong khoảng đã chọn." format={value => `${value} hợp đồng`}/></section>
        <section className={ui.card}><h2 style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 16, margin: "0 0 16px" }}><Trophy size={18}/>Nhân viên dẫn đầu theo giá trị</h2>
          <RankedBars items={report.people} empty="Chưa có giá trị hợp đồng trong khoảng đã chọn." format={value => `${compactMoney(value)} ${MAIN_CURRENCY}`}/>
          {report.others.length > 0 && <p className={s.muted} style={{ marginTop: 14, fontSize: 12 }}>Không cộng vào tổng: {report.others.map(([currency, total]) => `${money(total)} ${currency}`).join(" · ")} (mỗi loại tiền tính riêng, không quy đổi).</p>}
        </section>
      </div>
    </>}

    <section className={ui.card} aria-labelledby="report-money">
      <div className={s.titleRow} style={{ marginBottom: 12 }}><div><h2 id="report-money" style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 16, margin: 0 }}><Wallet size={18}/>Thu tiền</h2>
        <p className={s.muted} style={{ margin: "4px 0 0" }}>Giá trị, tiền đã thu và còn phải thu ({MAIN_CURRENCY}). Bấm “Xem” để mở các hợp đồng đó trong Quản lý hợp đồng.</p></div></div>
      <div className={s.toolbar} role="group" aria-label="Xem thu tiền theo">
        {TABS.map(([value, label]) => <button key={value} type="button" aria-pressed={tab === value} className={tab === value ? ui.primaryButton : ui.secondaryButton} onClick={() => setTab(value)}>{label}</button>)}
      </div>
      <div className={s.toolbar}>
        <input aria-label="Tìm trong danh sách thu tiền" className={ui.field} placeholder="Tìm mã, tên hợp đồng, khách hàng hoặc nhân viên…" value={search} onChange={e => setSearch(e.target.value)}/>
        <select aria-label="Lọc tình trạng thu" className={ui.field} style={{ maxWidth: 220 }} value={pay} onChange={e => setPay(e.target.value)}>
          {PAY_STATES.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select>
      </div>
      {loading ? <div className={s.empty}>Đang tải…</div> : tab === "contract" ? (contracts.total === 0
        ? <div className={s.empty}><FileText size={42} strokeWidth={1.3}/><strong>{pay || search ? "Không tìm thấy hợp đồng phù hợp" : "Chưa có hợp đồng có lịch thu tiền"}</strong><p>Lịch thu tiền có khi hợp đồng đã ký. Thử đổi khoảng thời gian hoặc bộ lọc.</p></div>
        : <><div className={s.tableWrap}><table className={s.table}><thead><tr>{CS("Hợp đồng", "name", "asc")}{CS("Khách hàng", "customer", "asc")}{CS("Phụ trách", "employee", "asc")}
            {CS("Giá trị", "value", "desc", "right")}{CS("Đã thu", "paid", "desc", "right")}{CS("Còn phải thu", "left", "desc", "right")}{CS("Tiến độ thu", "share")}{CS("Hạn thu tiếp theo", "due", "asc")}</tr></thead>
          <tbody>{contracts.rows.map(row => { const left = row.payments.scheduled - row.payments.paid; return <tr key={row.id}>
            <td><Link href={`/workspace/contracts/drafts/${row.id}`}><strong>{row.code || `#${row.id}`}</strong></Link><div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{row.name}</div></td>
            <td>{row.customerName}</td><td>{row.employeeName}</td>
            <td style={{ textAlign: "right", whiteSpace: "nowrap" }}>{money(row.totalAmount)}</td>
            <td style={{ textAlign: "right", whiteSpace: "nowrap" }}>{money(row.payments.paid)}</td>
            <td style={{ textAlign: "right", whiteSpace: "nowrap", ...tone(row.payments.overdue) }}>{money(left)}{row.payments.overdue > 0 && <div style={{ fontSize: 11, marginTop: 2 }}>Quá hạn {money(row.payments.overdue)}</div>}</td>
            <td><Collected paid={row.payments.paid} scheduled={row.payments.scheduled}/><div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{row.payments.paidInstallments}/{row.payments.installments} đợt</div></td>
            <td className={s.muted} style={row.payments.nextDue && asDate(row.payments.nextDue).getTime() < now ? tone(1) : undefined}>{left > 0 ? dateText(row.payments.nextDue) : "Đã thu đủ"}</td>
          </tr>; })}</tbody></table></div>
          <ListPager page={contracts.page} pageSize={contracts.pageSize} total={contracts.total} onPage={contracts.setPage} label="hợp đồng"/></>)
      : groups.total === 0 ? <div className={s.empty}><FileText size={42} strokeWidth={1.3}/><strong>Chưa có số liệu trong khoảng này</strong><p>Thử đổi khoảng thời gian hoặc bộ lọc.</p></div>
      : <><div className={s.tableWrap}><table className={s.table}><thead><tr>{GS(groupTitle, "label", tab === "month" ? "desc" : "asc")}{GS("Hợp đồng", "count", "desc", "right")}{GS("Giá trị", "value", "desc", "right")}
          {GS("Đã thu", "paid", "desc", "right")}{GS("Còn phải thu", "left", "desc", "right")}{GS("Quá hạn", "overdue", "desc", "right")}{GS("Tỷ lệ thu", "share")}<th><span className="sr-only">Xem</span></th></tr></thead>
        <tbody>{groups.rows.map(g => <tr key={g.key}>
          <td><strong>{g.label}</strong></td><td style={{ textAlign: "right" }}>{g.count}</td>
          <td style={{ textAlign: "right", whiteSpace: "nowrap" }}>{money(g.value)}</td><td style={{ textAlign: "right", whiteSpace: "nowrap" }}>{money(g.paid)}</td>
          <td style={{ textAlign: "right", whiteSpace: "nowrap" }}>{money(g.scheduled - g.paid)}</td>
          <td style={{ textAlign: "right", whiteSpace: "nowrap", ...tone(g.overdue) }}>{g.overdue ? money(g.overdue) : "—"}</td>
          <td><Collected paid={g.paid} scheduled={g.scheduled}/></td>
          <td><div style={{ display: "flex", justifyContent: "flex-end" }}><Link className={ui.secondaryButton} href={g.href}>Xem</Link></div></td>
        </tr>)}</tbody></table></div>
        <ListPager page={groups.page} pageSize={groups.pageSize} total={groups.total} onPage={groups.setPage} label={tab === "customer" ? "khách hàng" : tab === "employee" ? "nhân viên" : "tháng"}/></>}
    </section>
  </div>;
}
