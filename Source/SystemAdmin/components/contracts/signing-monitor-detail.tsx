"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { ArrowLeft, Check, CircleAlert, Clock3, FileCheck2, RefreshCw, ArrowRight } from "lucide-react";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import { contractSigningMonitorApi } from "@/services/contract-signing-monitor-api";
import { dateTime, documentStatusLabel, type SigningMonitorDetail } from "@/services/internal-api";

const categoryNames: Record<string, string> = { contract: "Hồ sơ", review: "Trao đổi", approval: "Phê duyệt", signature: "Chữ ký", identity: "Xác minh", link: "Liên kết", file: "Tệp", audit: "Nhật ký" };
const signatureStatus = (status: number) => status === 1 ? "Đã ký" : status === 2 ? "Từ chối" : "Đang chờ";

export default function SigningMonitorDetailPage() {
  const params = useParams<{ id: string }>();
  const validId = /^\d+$/.test(params.id);
  const [record, setRecord] = useState<SigningMonitorDetail | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(true);
  const [reload, setReload] = useState(0);
  useEffect(() => {
    if (!validId) return;
    let active = true;
    void contractSigningMonitorApi.detail(params.id).then(data => { if (active) setRecord(data); })
      .catch(e => { if (active) setError((e as Error).message); }).finally(() => { if (active) setBusy(false); });
    return () => { active = false; };
  }, [params.id, reload, validId]);

  if (!validId) return <div className={s.page}><p role="alert" className={ui.error}>Mã hồ sơ không hợp lệ.</p></div>;
  if (busy) return <div className={`grow min-h-0 overflow-y-auto ${s.page}`}><div className={s.signingEmpty}>Đang tải hồ sơ…</div></div>;
  if (error || !record) return <div className={`grow min-h-0 overflow-y-auto ${s.page}`}><Link href="/contract-signing" className={s.signingBack}><ArrowLeft size={16}/> Danh sách theo dõi ký</Link><p role="alert" className={ui.error}>{error || "Không tìm thấy hồ sơ."}</p></div>;
  const signatureRelevant = [3, 4, 5].includes(record.status);
  const signedCount = record.signers.filter(signer => signer.status === 1).length;
  return <div className={`grow min-h-0 overflow-y-auto ${s.page} ${s.signingPage}`} data-ui="signing-detail">
    <Link href="/contract-signing" className={s.signingBack}><ArrowLeft size={16}/> Theo dõi ký</Link>
    <div className={s.signingHeading}><div><div className={s.signingKicker}><FileCheck2 size={15}/> {record.code || `HỒ SƠ #${record.id}`}</div><h1>{record.name}</h1><p className={s.muted}>{record.customerName} <i>·</i> Phụ trách {record.employeeName}</p></div><div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}><Link className={ui.primaryButton} href={`/workspace/contracts/drafts/${encodeURIComponent(record.id)}`} title="Cần đăng nhập bằng tài khoản nhân viên nội bộ; đăng nhập System Admin không tự đăng nhập không gian này."><ArrowRight size={16}/>{record.status === 0 ? "Mở bản nháp để tiếp tục" : "Mở hồ sơ xử lý"}</Link>{record.status === 3 && <Link className={ui.secondaryButton} href="/workspace/contracts/company-signing">Hàng đợi ký công ty</Link>}<button className={ui.secondaryButton} disabled={busy} onClick={() => { setBusy(true); setError(""); setReload(v => v + 1); }}><RefreshCw size={15}/> Làm mới</button></div></div>
    <p className={s.signingFootnote}>Chỉnh sửa hồ sơ cần tài khoản nhân viên nội bộ. Nếu được yêu cầu đăng nhập, sau khi đăng nhập hệ thống sẽ quay lại hồ sơ này.</p>
    {record.status === 0 && <div className={s.signingNotice} role="status"><CircleAlert size={18}/><div><strong>Hồ sơ đang là bản nháp</strong><p>Chưa có chữ ký ở trạng thái này. Mở hồ sơ để hoàn thiện nội dung, lưu nháp rồi gửi khách hàng duyệt.</p></div></div>}
    <div className={s.signingDetailStrip}><div><small>Trạng thái hồ sơ</small><strong>{documentStatusLabel[record.status] ?? `Trạng thái ${record.status}`}</strong></div><div><small>Phiên bản</small><strong>v{record.versionNo}{record.versionLocked ? " · Đã khóa" : ""}</strong></div><div><small>Chữ ký đã ghi nhận</small><strong>{signedCount} / {record.signers.length}</strong></div><div><small>Ngày tạo</small><strong>{dateTime(record.createdAt)}</strong></div></div>
    {signatureRelevant && !record.signers.length && <div className={s.signingNotice} role="status"><CircleAlert size={18}/><div><strong>Chưa có bản ghi chữ ký điện tử trong database mới</strong><p>Trang này đọc các bảng chuẩn của ConTrackDB. Phiên ký, link ký và OTP của service hiện tại chưa được ghi vào các bảng theo dõi này, nên chưa thể hiện được tiến độ ký trực tiếp ở đây.</p></div></div>}
    <div className={s.signingDetailGrid}>
      <section className={s.signingSection}><header><div><h2>Các bên ký</h2><p>Trạng thái chữ ký trên các phiên bản hợp đồng</p></div></header>
        {!record.signers.length ? <div className={s.signingSectionEmpty}>Chưa có bản ghi chữ ký trong ConTrackDB.</div> : <div className={s.signerList}>{record.signers.map(signer => <article key={signer.id} className={s.signerRow}><div><span className={s.signerParty}>{signer.party} · {signer.role} · v{signer.versionNo}</span><strong>{signer.name}</strong><small>{signer.title || signer.email || ""}</small></div><span className={signer.status === 1 ? s.signingStepDone : signer.status === 2 ? s.signingWarning : s.signingStepCurrent}>{signer.status === 1 ? <Check size={14}/> : <Clock3 size={14}/>} {signatureStatus(signer.status)}</span>{signer.signedAt && <time>{dateTime(signer.signedAt)}</time>}{signer.rejectReason && <p className={s.signerReject}>{signer.rejectReason}</p>}</article>)}</div>}
      </section>
      <section className={s.signingSection}><header><div><h2>Bằng chứng hồ sơ</h2><p>Tệp và mã kiểm tra đã lưu</p></div></header>
        {!record.artifacts.length ? <div className={s.signingSectionEmpty}>Chưa có tệp đính kèm.</div> : <div className={s.artifactList}>{record.artifacts.map((file, index) => <article key={`${file.fileName}-${index}`}><strong>{file.fileName}</strong><span>{file.contentType} · {Number(file.size).toLocaleString("vi-VN")} byte{file.primary ? " · Tệp chính" : ""}</span><code title={file.sha256}>SHA-256 {file.sha256}</code><small>Thêm lúc {dateTime(file.addedAt)}</small></article>)}</div>}
        {record.snapshotHash && <div className={s.documentHash}><small>Hash snapshot phiên bản</small><code title={record.snapshotHash}>{record.snapshotHash}</code></div>}
      </section>
    </div>
    <section className={s.signingTimelineSection}><header><div><h2>Diễn biến hồ sơ</h2><p>{record.timeline.length} mốc được ghi nhận · sắp xếp theo thời gian</p></div></header>
      {!record.timeline.length ? <div className={s.signingSectionEmpty}>Chưa có sự kiện.</div> : <ol className={s.signingTimeline}>{record.timeline.map((event, index) => <li key={`${event.at}-${event.category}-${index}`} className={event.state === "failed" ? s.timelineFailed : event.state === "pending" ? s.timelinePending : ""}><span className={s.timelineMarker}>{event.state === "complete" ? <Check size={12}/> : <Clock3 size={12}/>}</span><div><div className={s.timelineMeta}><span>{categoryNames[event.category] ?? event.category}</span><time>{dateTime(event.at)}</time></div><strong>{event.title}</strong>{event.detail && <p>{event.detail}</p>}</div></li>)}</ol>}
    </section>
  </div>;
}
