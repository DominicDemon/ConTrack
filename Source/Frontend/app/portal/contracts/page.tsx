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

export default function CustomerContracts() {
  const router = useRouter();
  const [session, setSession] = useState<CustomerSession | null>(null);
  const [rows, setRows] = useState<CustomerReviewSummary[] | null>(null); const [error, setError] = useState("");
  useEffect(() => {
    void Promise.all([customerApi<CustomerSession>("/auth/me"), customerApi<CustomerReviewSummary[]>("/reviews")])
      .then(([me, list]) => { setSession(me); setRows(list); })
      .catch(e => { if (handleAuthError(e, router.replace)) setError(e.message); });
  }, [router]);
  return <><PortalHeader name={session?.name}/><div className={s.page}>
    <span className={s.eyebrow}>Cổng khách hàng</span><h1>Hợp đồng của bạn</h1><p className={s.muted}>Hợp đồng đang chờ bạn phản hồi được xếp lên đầu.</p>
    {error && <p role="alert" className={ui.error} style={{ margin: "16px 0" }}>{error}</p>}
    <section className={ui.card} style={{ marginTop: 18 }}>
      {!rows ? <div className={s.empty}>{error ? "" : "Đang tải…"}</div> : rows.length === 0 ? <div className={s.empty}><Files size={42} strokeWidth={1.3}/><strong>Chưa có hợp đồng nào được gửi cho bạn</strong></div>
        : <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Hợp đồng</th><th>Phiên bản</th><th>Trạng thái</th><th>Ngày gửi</th><th><span className="sr-only">Mở</span></th></tr></thead>
          <tbody>{rows.map(r => <tr key={r.id}><td><Link href={`/portal/contracts/${r.id}`}>{r.contractName}</Link></td><td>{r.versionNo}</td>
            <td><span className={`${s.badge} ${isOpenReview(r.status) ? s.warning : ""}`}>{customerStatusLabel[r.status] ?? r.status}</span></td>
            <td className={s.muted}>{dateTime(r.sentAt)}</td><td><Link href={`/portal/contracts/${r.id}`} aria-label={`Mở ${r.contractName}`}>
              {r.status === "APPROVED" ? "Theo dõi và ký" : <ArrowUpRight size={18}/>}</Link></td></tr>)}</tbody></table></div>}
    </section>
  </div></>;
}
