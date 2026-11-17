"use client";
import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { Activity, AlertTriangle, Check, Clock3, FilePlus2, RefreshCw, Search } from "lucide-react";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import { contractSigningMonitorApi } from "@/services/contract-signing-monitor-api";
import { dateTime, documentStatusLabel, type SigningMonitorEntry } from "@/services/internal-api";

const tabs = [
  { id: "all", label: "Tất cả" }, { id: "waiting", label: "Đang chờ ký" },
  { id: "signed", label: "Đã có chữ ký" }, { id: "untracked", label: "Thiếu dữ liệu ký" },
] as const;

export default function SigningMonitorList() {
  const [rows, setRows] = useState<SigningMonitorEntry[]>([]);
  const [search, setSearch] = useState("");
  const [tab, setTab] = useState<(typeof tabs)[number]["id"]>("all");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(true);
  const [reload, setReload] = useState(0);
  useEffect(() => {
    let active = true;
    const timer = setTimeout(() => {
      setBusy(true); setError("");
      void contractSigningMonitorApi.list(search.trim()).then(data => { if (active) setRows(data); })
        .catch(e => { if (active) setError((e as Error).message); }).finally(() => { if (active) setBusy(false); });
    }, 200);
    return () => { active = false; clearTimeout(timer); };
  }, [search, reload]);

  const counts = useMemo(() => ({ all: rows.length, waiting: rows.filter(x => x.status === 3).length,
    signed: rows.filter(x => x.signedCount > 0).length, untracked: rows.filter(x => [3, 4, 5].includes(x.status) && x.signatureCount === 0).length }), [rows]);
  const visible = rows.filter(row => tab === "all" || (tab === "waiting" && row.status === 3)
    || (tab === "signed" && row.signedCount > 0) || (tab === "untracked" && [3, 4, 5].includes(row.status) && row.signatureCount === 0));

  return <div className={`grow min-h-0 overflow-y-auto ${s.page} ${s.signingPage}`} data-ui="signing-monitor">
    <div className={s.signingHeading}><div><div className={s.signingKicker}><Activity size={15}/> VẬN HÀNH HỢP ĐỒNG</div><h1>Theo dõi ký hợp đồng</h1><p className={s.muted}>Tiến độ từng bên ký, sự kiện đã ghi nhận và bước đang chờ.</p></div>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}><Link href="/workspace/contracts/drafts/new" className={ui.primaryButton}><FilePlus2 size={16}/>Tạo bản nháp để thử luồng ký</Link><button className={ui.secondaryButton} disabled={busy} onClick={() => setReload(v => v + 1)}><RefreshCw size={15}/> Làm mới</button></div></div>
    <div className={s.signingMetrics} aria-label="Tổng quan tiến độ ký"><div><strong>{counts.waiting}</strong><span>Chờ ký</span></div><div><strong>{counts.signed}</strong><span>Đã có chữ ký</span></div><div className={counts.untracked ? s.signingMetricAlert : ""}><strong>{counts.untracked}</strong><span>Thiếu dữ liệu chữ ký điện tử</span></div><div><strong>{counts.all}</strong><span>Hồ sơ theo dõi</span></div></div>
    <div className={s.signingTabs} role="tablist" aria-label="Lọc tiến độ ký">{tabs.map(item => <button key={item.id} role="tab" aria-selected={tab === item.id} className={tab === item.id ? s.signingTabActive : s.signingTab} onClick={() => setTab(item.id)}>{item.label}<span>{counts[item.id]}</span></button>)}</div>
    <div className={s.signingToolbar}><label className={s.signingSearch}><Search size={16}/><input aria-label="Tìm hợp đồng" placeholder="Tìm theo mã, tên hợp đồng hoặc khách hàng" value={search} onChange={e => setSearch(e.target.value)}/></label><span className={s.muted}>{visible.length} hồ sơ · tối đa 200</span></div>
    {error && <p role="alert" className={ui.error}>{error}</p>}
    {busy ? <div className={s.signingEmpty}>Đang tải tiến độ ký…</div> : visible.length === 0 ? <div className={s.signingEmpty}><Activity size={28}/><strong>{rows.length ? "Không có hồ sơ ở trạng thái này" : "Chưa có hồ sơ để theo dõi"}</strong><span>{rows.length ? "Thử chọn trạng thái khác." : "Tạo bản nháp trước, sau đó gửi khách xem và bắt đầu các bước ký."}</span>{rows.length === 0 && <Link href="/workspace/contracts/drafts/new" className={ui.primaryButton}><FilePlus2 size={16}/>Tạo bản nháp để thử luồng ký</Link>}</div> :
      <div className={s.signingRows}>{visible.map(row => {
        const untracked = [3, 4, 5].includes(row.status) && row.signatureCount === 0;
        return <Link className={s.signingRow} key={row.id} href={`/contract-signing/${row.id}`}>
          <div className={s.signingIdentity}><span className={s.signingCode}>{row.code || `#${row.id}`}</span><strong>{row.name}</strong><span>{row.customerName} <i>·</i> Phụ trách {row.employeeName}</span></div>
          <div className={s.signingProgress} aria-label={`${row.signedCount} trên ${row.signatureCount} chữ ký đã ghi nhận`}>
            <span className={row.companySigned ? s.signingStepDone : row.status === 3 && !row.customerSigned ? s.signingStepCurrent : s.signingStepWait}>{row.companySigned ? <Check size={13}/> : <Clock3 size={13}/>} Công ty</span><span className={s.signingConnector}/>
            <span className={row.customerSigned ? s.signingStepDone : row.companySigned ? s.signingStepCurrent : s.signingStepWait}>{row.customerSigned ? <Check size={13}/> : <Clock3 size={13}/>} Khách hàng</span>
          </div>
          <div className={s.signingState}>{untracked ? <span className={s.signingWarning}><AlertTriangle size={14}/> Thiếu dữ liệu ký điện tử</span> : <span className={s.badge}>{documentStatusLabel[row.status] ?? `Trạng thái ${row.status}`}</span>}<small>{row.signatureCount ? `${row.signedCount}/${row.signatureCount} chữ ký` : `Phiên bản ${row.versionNo}`}</small></div>
          <div className={s.signingUpdated}><small>Cập nhật</small><span>{dateTime(row.lastSignedAt ?? row.updatedAt)}</span></div>
        </Link>;
      })}</div>}
    <p className={s.signingFootnote}>Dữ liệu được giới hạn trong hồ sơ công ty và lấy từ ConTrackDB. Trạng thái thiếu bằng chứng được đánh dấu riêng.</p>
  </div>;
}
