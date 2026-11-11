"use client";
import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { Activity, AlertTriangle, Check, Clock3, FilePlus2, RefreshCw, Search } from "lucide-react";
import { useInternalUser } from "@/components/internal/internal-shell";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import { internalApi, dateTime, documentStatusLabel, type SigningMonitorEntry } from "@/services/internal-api";

const tabs = [
  { id: "all", label: "Tất cả" },
  { id: "waiting", label: "Đang chờ ký" },
  { id: "signed", label: "Đã có chữ ký" },
  { id: "untracked", label: "Thiếu dữ liệu ký" },
] as const;
type SigningDemoResult = { draft: { id: string; code: string }; customerPortalPhone: string; customerPortalPassword: string };

export default function ContractSigningMonitorPage() {
  const user = useInternalUser();
  const [rows, setRows] = useState<SigningMonitorEntry[]>([]);
  const [search, setSearch] = useState("");
  const [tab, setTab] = useState<(typeof tabs)[number]["id"]>("all");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(true);
  const [reload, setReload] = useState(0);
  const [demoAvailable, setDemoAvailable] = useState(false);
  const [demoBusy, setDemoBusy] = useState(false);
  const [demo, setDemo] = useState<SigningDemoResult | null>(null);

  useEffect(() => {
    if (!user.canManageAll) return;
    let active = true;
    void internalApi<{ available: boolean }>("/contract-signing-demo/availability")
      .then(result => { if (active) setDemoAvailable(result.available); })
      .catch(() => { if (active) setDemoAvailable(false); });
    return () => { active = false; };
  }, [user.canManageAll]);

  useEffect(() => {
    if (!user.canManageAll) return;
    let active = true;
    const timer = setTimeout(() => {
      setBusy(true); setError("");
      const params = new URLSearchParams();
      if (search.trim()) params.set("search", search.trim());
      void internalApi<SigningMonitorEntry[]>(`/contract-signing-monitor?${params}`)
        .then(result => { if (active) setRows(result); })
        .catch(e => { if (active) setError(e.message); })
        .finally(() => { if (active) setBusy(false); });
    }, 200);
    return () => { active = false; clearTimeout(timer); };
  }, [user.canManageAll, search, reload]);

  const counts = useMemo(() => ({
    all: rows.length,
    waiting: rows.filter(x => x.status === 3).length,
    signed: rows.filter(x => x.signedCount > 0).length,
    untracked: rows.filter(x => [3, 4, 5].includes(x.status) && x.signatureCount === 0).length,
  }), [rows]);
  const visible = rows.filter(row => tab === "all"
    || (tab === "waiting" && row.status === 3)
    || (tab === "signed" && row.signedCount > 0)
    || (tab === "untracked" && [3, 4, 5].includes(row.status) && row.signatureCount === 0));

  async function createSigningDemo() {
    setDemoBusy(true); setError(""); setDemo(null);
    try {
      const result = await internalApi<SigningDemoResult>("/contract-signing-demo", "POST");
      setDemo(result);
      setTab("all");
      setReload(value => value + 1);
    } catch (e) { setError((e as Error).message); }
    finally { setDemoBusy(false); }
  }

  if (!user.canManageAll) return <div className={s.page}><p role="alert" className={ui.error}>Theo dõi ký chỉ dành cho quản trị viên công ty.</p></div>;

  return <div className={`${s.page} ${s.signingPage}`} data-ui="signing-monitor">
    <div className={s.signingHeading}>
      <div><div className={s.signingKicker}><Activity size={15}/> VẬN HÀNH HỢP ĐỒNG</div><h1>Theo dõi ký hợp đồng</h1><p className={s.muted}>Xem tiến độ của từng hồ sơ, chữ ký đã ghi nhận và các bước đang chờ.</p></div>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
        <Link href="/workspace/contracts/drafts/new" className={ui.primaryButton}><FilePlus2 size={16}/>Tạo hợp đồng nháp</Link>
        {demoAvailable && <button className={ui.primaryButton} disabled={demoBusy} onClick={() => void createSigningDemo()}><FilePlus2 size={16}/>{demoBusy ? "Đang chuẩn bị demo…" : "Tạo demo toàn luồng ký"}</button>}
        <button className={ui.secondaryButton} disabled={busy || demoBusy} onClick={() => setReload(v => v + 1)}><RefreshCw size={15}/> Làm mới</button>
      </div>
    </div>

    {demo && <section className={ui.card} style={{ margin: "16px 0" }} aria-label="Hướng dẫn demo toàn luồng ký">
      <h2 style={{ margin: "0 0 8px", fontSize: 18 }}>Demo đã sẵn sàng: {demo.draft.code}</h2>
      <p className={s.muted}>Bản nháp và khách hàng thử nghiệm đã được tạo. Hoàn tất theo đúng quy trình để hợp đồng xuất hiện trên trang theo dõi.</p>
      <p className={s.muted}>Sau khi khách duyệt trên Portal, nếu bản nháp vẫn hiện trạng thái cũ, bấm <strong>Làm mới trạng thái</strong> trong trang bản nháp trước khi phê duyệt nội bộ.</p>
      <ol style={{ display: "grid", gap: 8, margin: "14px 0", paddingLeft: 22 }}>
        <li><Link href={`/workspace/contracts/drafts/${demo.draft.id}`} target="_blank" rel="noreferrer">Mở bản nháp</Link> trong tab mới, rồi chọn <strong>Gửi khách xem</strong>. Giữ tab theo dõi này mở để xem thông tin đăng nhập Portal.</li>
        <li>Khách đăng nhập <a href="http://localhost:3000/portal/login" target="_blank" rel="noreferrer">Portal khách hàng</a> bằng thông tin dưới đây, mở hợp đồng và chọn đồng ý.</li>
        <li>Quay lại bản nháp để <strong>phê duyệt nội bộ</strong>, sau đó tạo phiên ký.</li>
        <li>Mở mục <strong>Ký phía công ty</strong>, ký PDF. Sao chép link khách hàng được cấp.</li>
        <li>Khách mở link, yêu cầu OTP, nhập mã Development hiện trên trang, xem PDF và ký. Trạng thái cuối cùng cùng PDF sẽ xuất hiện tại đây và trong Kho hợp đồng.</li>
      </ol>
      <dl className={s.fields} style={{ marginTop: 12 }}>
        <div><dt>Số điện thoại Portal</dt><dd><code>{demo.customerPortalPhone}</code></dd></div>
        <div><dt>Mật khẩu Portal thử nghiệm</dt><dd><code>{demo.customerPortalPassword}</code></dd></div>
      </dl>
      <p className={s.muted} style={{ marginBottom: 0 }}>Tài khoản này chỉ được tạo trong Development; SMS review dùng số thử nghiệm. OTP ký điện tử sẽ hiện trên trang nếu eSMS tắt.</p>
    </section>}

    <div className={s.signingMetrics} aria-label="Tổng quan tiến độ ký">
      <div><strong>{counts.waiting}</strong><span>Chờ ký</span></div>
      <div><strong>{counts.signed}</strong><span>Đã có chữ ký</span></div>
      <div className={counts.untracked ? s.signingMetricAlert : ""}><strong>{counts.untracked}</strong><span>Thiếu dữ liệu chữ ký điện tử</span></div>
      <div><strong>{counts.all}</strong><span>Hồ sơ đang theo dõi</span></div>
    </div>

    <div className={s.signingTabs} role="tablist" aria-label="Lọc tiến độ ký">
      {tabs.map(item => <button key={item.id} role="tab" aria-selected={tab === item.id} className={tab === item.id ? s.signingTabActive : s.signingTab} onClick={() => setTab(item.id)}>{item.label}<span>{counts[item.id]}</span></button>)}
    </div>

    <div className={s.signingToolbar}><label className={s.signingSearch}><Search size={16}/><input aria-label="Tìm hợp đồng" placeholder="Tìm theo mã, tên hợp đồng hoặc khách hàng" value={search} onChange={e => setSearch(e.target.value)}/></label><span className={s.muted}>{visible.length} hồ sơ · cập nhật tối đa 200</span></div>
    {error && <p role="alert" className={ui.error}>{error}</p>}
    {busy ? <div className={s.signingEmpty}>Đang tải tiến độ ký…</div> : visible.length === 0 ? <div className={s.signingEmpty}><Activity size={28}/><strong>{rows.length ? "Không có hồ sơ ở trạng thái này" : "Chưa có hồ sơ để theo dõi"}</strong><span>{rows.length ? "Thử chọn một trạng thái khác." : "Tạo một bản nháp hợp đồng để bắt đầu thử luồng ký điện tử."}</span>{rows.length === 0 && <Link href="/workspace/contracts/drafts/new" className={ui.primaryButton}><FilePlus2 size={16}/>Tạo bản nháp để thử luồng ký</Link>}</div> :
      <div className={s.signingRows}>
        {visible.map(row => {
          const untracked = [3, 4, 5].includes(row.status) && row.signatureCount === 0;
          return <Link className={s.signingRow} key={row.id} href={`/workspace/contracts/signing/${row.id}`}>
            <div className={s.signingIdentity}><span className={s.signingCode}>{row.code || `#${row.id}`}</span><strong>{row.name}</strong><span>{row.customerName} <i>·</i> Phụ trách {row.employeeName}</span></div>
            <div className={s.signingProgress} aria-label={`${row.signedCount} trên ${row.signatureCount} bên đã ký`}>
              <span className={row.companySigned ? s.signingStepDone : row.status === 3 && !row.customerSigned ? s.signingStepCurrent : s.signingStepWait}>{row.companySigned ? <Check size={13}/> : <Clock3 size={13}/>} Công ty</span>
              <span className={s.signingConnector}/>
              <span className={row.customerSigned ? s.signingStepDone : row.companySigned ? s.signingStepCurrent : s.signingStepWait}>{row.customerSigned ? <Check size={13}/> : <Clock3 size={13}/>} Khách hàng</span>
            </div>
            <div className={s.signingState}>{untracked ? <span className={s.signingWarning}><AlertTriangle size={14}/> Thiếu dữ liệu ký điện tử</span> : <span className={s.badge}>{documentStatusLabel[row.status] ?? `Trạng thái ${row.status}`}</span>}<small>{row.signatureCount ? `${row.signedCount}/${row.signatureCount} chữ ký` : `Phiên bản ${row.versionNo}`}</small></div>
            <div className={s.signingUpdated}><small>Cập nhật</small><span>{dateTime(row.lastSignedAt ?? row.updatedAt)}</span></div>
          </Link>;
        })}
      </div>}
    <p className={s.signingFootnote}>Trang này tổng hợp dữ liệu hợp đồng và chữ ký được lưu trong database công ty. Hồ sơ ở trạng thái chờ ký nhưng chưa có bản ghi chữ ký được đánh dấu riêng để dễ phát hiện.</p>
  </div>;
}
