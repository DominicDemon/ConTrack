"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowUpRight, Files } from "lucide-react";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import PortalHeader from "@/components/portal/portal-header";
import { dateTime } from "@/services/internal-api";
import { customerApi, customerStatusLabel, handleAuthError, isOpenReview, type CustomerReviewSummary, type CustomerSession } from "@/services/customer-api";

const filters = [["all", "Tất cả"], ["signed", "Đã ký"], ["unsigned", "Chưa ký"]] as const;

export default function CustomerContracts() {
  const router = useRouter();
  const [session, setSession] = useState<CustomerSession | null>(null);
  const [rows, setRows] = useState<CustomerReviewSummary[] | null>(null); const [error, setError] = useState("");
  const [filter, setFilter] = useState<(typeof filters)[number][0]>("all");
  const visibleRows = rows?.filter(row => filter === "all" || Boolean(row.isSigned) === (filter === "signed"));
  useEffect(() => {
    let active = true;
    let loading = false;
    async function load() {
      if (loading) return;
      loading = true;
      try {
        const [me, list] = await Promise.all([customerApi<CustomerSession>("/auth/me"), customerApi<CustomerReviewSummary[]>("/reviews")]);
        if (active) { setSession(me); setRows(list); setError(""); }
      } catch (e) {
        if (active && handleAuthError(e, router.replace)) setError((e as Error).message);
      } finally { loading = false; }
    }
    const onFocus = () => { void load(); };
    const onPageShow = (event: PageTransitionEvent) => { if (event.persisted) void load(); };
    void load();
    window.addEventListener("focus", onFocus);
    window.addEventListener("pageshow", onPageShow);
    return () => {
      active = false;
      window.removeEventListener("focus", onFocus);
      window.removeEventListener("pageshow", onPageShow);
    };
  }, [router]);
  return <><PortalHeader name={session?.name}/><div className={s.page}>
    <span className={s.eyebrow}>Cổng khách hàng</span><h1>Hợp đồng của bạn</h1><p className={s.muted}>Hợp đồng đang chờ bạn phản hồi được xếp lên đầu.</p>
    {error && <p role="alert" className={ui.error} style={{ margin: "16px 0" }}>{error}</p>}
    <section className={ui.card} style={{ marginTop: 18 }}>
      <div className={s.toolbar} role="group" aria-label="Lọc theo tình trạng ký">
        {filters.map(([value, label]) => <button key={value} type="button" aria-pressed={filter === value}
          className={filter === value ? ui.primaryButton : ui.secondaryButton} onClick={() => setFilter(value)}>{label}</button>)}
      </div>
      {!visibleRows ? <div className={s.empty}>{error ? "" : "Đang tải…"}</div> : visibleRows.length === 0 ? <div className={s.empty}><Files size={42} strokeWidth={1.3}/><strong>{filter === "signed" ? "Chưa có hợp đồng đã ký" : filter === "unsigned" ? "Không có hợp đồng chưa ký" : "Chưa có hợp đồng nào được gửi cho bạn"}</strong></div>
        : <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Hợp đồng</th><th>Phiên bản</th><th>Trạng thái</th><th>Ngày gửi</th><th><span className="sr-only">Mở</span></th></tr></thead>
          <tbody>{visibleRows.map(r => <tr key={r.id}><td><Link href={`/portal/contracts/${r.id}`}>{r.contractName}</Link></td><td>{r.versionNo}</td>
            <td><span className={`${s.badge} ${!r.isSigned && isOpenReview(r.status) ? s.warning : ""}`}>{r.isSigned ? "Đã ký" : customerStatusLabel[r.status] ?? r.status}</span></td>
            <td className={s.muted}>{dateTime(r.sentAt)}</td><td><Link href={`/portal/contracts/${r.id}`} aria-label={r.isSigned ? undefined : `Mở ${r.contractName}`}>
              {r.isSigned ? "Xem hợp đồng đã ký" : r.status === "APPROVED" ? "Theo dõi và ký" : <ArrowUpRight size={18}/>}</Link></td></tr>)}</tbody></table></div>}
    </section>
  </div></>;
}
