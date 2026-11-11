"use client";
import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { Check, CopyPlus, KeyRound, MessageSquare, Send, XCircle, Handshake, RefreshCw } from "lucide-react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { internalApi, dateTime, documentStatusLabel, reviewStatusLabel, type DraftRecord, type DraftVersionHistoryItem, type ReviewSummary, type ReviewComment, type InternalApprovalState, type SigningMonitorDetail } from "@/services/internal-api";
import { useInternalUser } from "./internal-shell";
import { internalStyles as ui } from "./internal-theme";
import s from "./workspace.module.css";

type Confirm = { title: string; description: string; action: string; note?: { label: string; required?: boolean; max: number }; run: (note: string) => Promise<unknown> };
const customerPortalLoginUrl = process.env.NEXT_PUBLIC_CUSTOMER_PORTAL_URL || "http://localhost:3000/portal/login";

/** Customer review actions and history for a saved draft. All state transitions are re-checked by the API. */
export default function ReviewPanel({ record, dirty, onChanged }: { record: DraftRecord; dirty: boolean; onChanged: () => void }) {
  const user = useInternalUser();
  const [reviews, setReviews] = useState<ReviewSummary[]>([]);
  const [versions, setVersions] = useState<DraftVersionHistoryItem[]>([]);
  const [approval, setApproval] = useState<InternalApprovalState | null>(null);
  const [signingProgress, setSigningProgress] = useState<SigningMonitorDetail | null>(null);
  const [error, setError] = useState(""); const [notice, setNotice] = useState("");
  const [confirm, setConfirm] = useState<Confirm | null>(null); const [note, setNote] = useState(""); const [busy, setBusy] = useState(false);
  const [replyTo, setReplyTo] = useState<{ review: string; comment: ReviewComment } | null>(null); const [reply, setReply] = useState("");
  const [signerName, setSignerName] = useState(record.content.fields.PROVIDER_REPRESENTATIVE_NAME ?? "");
  const [signerTitle, setSignerTitle] = useState(record.content.fields.PROVIDER_REPRESENTATIVE_TITLE ?? "");
  const [signerPhone, setSignerPhone] = useState(record.content.fields.PROVIDER_PHONE ?? "");
  const fetchWorkflow = useCallback(async () => {
    const [reviewRows, approvalState, versionRows] = await Promise.all([
      internalApi<ReviewSummary[]>(`/contract-drafts/${record.id}/reviews`),
      internalApi<InternalApprovalState | null>(`/contract-drafts/${record.id}/internal-approval`),
      internalApi<DraftVersionHistoryItem[]>(`/contract-drafts/${record.id}/versions`),
    ]);
    let signingState: SigningMonitorDetail | null = null;
    if (approvalState?.signingStarted && user.canManageAll) {
      try { signingState = await internalApi<SigningMonitorDetail>(`/contract-signing-monitor/${record.id}`); }
      catch { signingState = null; }
    }
    return { reviewRows, approvalState, signingState, versionRows };
  }, [record.id, user.canManageAll]);
  const load = useCallback(async () => {
    try {
      const state = await fetchWorkflow();
      setReviews(state.reviewRows); setApproval(state.approvalState); setSigningProgress(state.signingState); setVersions(state.versionRows);
    } catch (e) { setError((e as Error).message); }
  }, [fetchWorkflow]);
  useEffect(() => {
    let active = true;
    void fetchWorkflow().then(state => {
      if (!active) return;
      setReviews(state.reviewRows); setApproval(state.approvalState); setSigningProgress(state.signingState); setVersions(state.versionRows); setError("");
    }).catch(e => { if (active) setError((e as Error).message); });
    return () => { active = false; };
  }, [fetchWorkflow, record.rowVersion]);
  const latest = reviews[0];
  const canSendToCustomer = record.status === 0 || record.status === 1;
  const canCreateVersion = (record.canEdit && !["SENT", "VIEWED"].includes(latest?.status ?? ""))
    || (record.status === 1 && record.locked && (latest?.status === "CHANGES_REQUESTED" || (latest?.status === "REJECTED" && latest.followupDecision === "NEGOTIATE")));
  const awaitingDecision = record.status === 7 && latest?.status === "REJECTED" && !latest.followupDecision;
  const awaitingInternalApproval = record.status === 2 && approval?.status === 0;
  const canStartSigning = user.canManageAll && record.status === 3 && approval?.status === 1 && !approval.signingStarted;
  const customerApproved = latest?.status === "APPROVED";
  const signingComplete = record.status === 4 || record.status === 5;
  const companySigned = signingProgress?.signers.some(signer => signer.party === "Công ty" && signer.status === 1) ?? false;
  const customerSigned = signingProgress?.signers.some(signer => signer.party === "Khách hàng" && signer.status === 1) ?? signingComplete;
  const workflowSteps = [
    { title: "Hoàn thiện bản nháp", detail: record.status === 0 ? (record.sendBlockedReason ?? "Điền đủ nội dung, thông tin khách hàng và điều khoản.") : "Phiên bản hợp đồng đã được lưu.", done: record.status !== 0, current: record.status === 0 },
    { title: "Khách hàng duyệt nội dung", detail: customerApproved ? "Khách hàng đã đồng ý phiên bản này." : latest && ["SENT", "VIEWED"].includes(latest.status) ? "Đang chờ khách xem và phản hồi." : "Gửi phiên bản đã hoàn thiện để khách xác nhận nội dung.", done: customerApproved, current: !customerApproved && record.status === 1 },
    { title: "Quản lý duyệt bản chốt", detail: approval?.status === 1 ? "Đã được quản lý phê duyệt." : awaitingInternalApproval ? "Đang chờ quản lý công ty duyệt." : "Chỉ mở sau khi khách hàng đồng ý.", done: approval?.status === 1, current: awaitingInternalApproval },
    { title: "Công ty ký trước", detail: companySigned ? "Đại diện công ty đã ký." : approval?.signingStarted ? "Đang chờ đại diện công ty ký trong hàng đợi." : "Sau khi duyệt, tạo phiên ký và ký với tư cách công ty.", done: companySigned, current: record.status === 3 && !companySigned },
    { title: "Khách hàng ký", detail: customerSigned ? "Khách hàng đã ký, hồ sơ hoàn tất." : companySigned ? "Công ty đã ký. Khách đăng nhập Portal, mở hợp đồng và xác minh OTP để ký." : "Sau khi công ty ký, khách đăng nhập Portal để tiếp tục ký.", done: customerSigned, current: companySigned && !customerSigned },
  ];

  function ask(c: Confirm) { setConfirm(c); setNote(""); setError(""); setNotice(""); }
  async function run() {
    if (!confirm) return; setBusy(true); setError("");
    try { await confirm.run(note.trim()); setConfirm(null); await load(); onChanged(); }
    catch (e) { setError((e as Error).message); setConfirm(null); } finally { setBusy(false); }
  }
  async function act<T>(action: () => Promise<T>, done?: string | ((result: T) => string)) {
    setBusy(true); setError(""); setNotice("");
    try { const result = await action(); if (done) setNotice(typeof done === "string" ? done : done(result)); await load(); } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  async function resetCustomerPassword() {
    await act(() => internalApi<{ smsSimulated: boolean; developmentPhone: string | null; developmentPassword: string | null }>(
      `/customers/${record.content.customerId}/reset-temp-password`, "POST"), result => result.developmentPhone && result.developmentPassword
        ? `SMS không gửi thật trong môi trường Development. Đăng nhập Portal tại ${customerPortalLoginUrl} bằng SĐT ${result.developmentPhone} và mật khẩu tạm ${result.developmentPassword}.`
        : result.smsSimulated
          ? "SMS đang ở chế độ giả lập nên khách không nhận được tin. Cấu hình eSMS thật để gửi mật khẩu."
          : "Đã xếp yêu cầu gửi mật khẩu tạm. Trạng thái này chưa xác nhận SMS đã được giao.");
  }
  async function refreshWorkflow() {
    setBusy(true); setError("");
    try { await load(); onChanged(); }
    finally { setBusy(false); }
  }

  const send = () => ask({ title: "Gửi khách xem", action: "Gửi khách xem",
    description: `Gửi phiên bản ${record.versionNo} cho ${record.customerName} (${record.customerPhoneMasked}). Phiên bản sẽ bị khóa. Khách đăng nhập Portal bằng số điện thoại và mật khẩu để xem, phản hồi hợp đồng.`,
    note: { label: "Lời nhắn cho khách (tùy chọn)", max: 1000 },
    run: n => internalApi(`/contract-drafts/${record.id}/send-review`, "POST", { message: n || null, rowVersion: record.rowVersion, versionRowVersion: record.versionRowVersion }) });
  const newVersion = () => ask({ title: "Tạo phiên bản mới", action: "Tạo phiên bản", description: `Tạo phiên bản ${Number(record.versionNo) + 1} từ nội dung hiện tại. Phiên bản ${record.versionNo} vẫn được giữ trong lịch sử.`,
    note: { label: "Ghi chú thay đổi (tùy chọn)", max: 1000 },
    run: n => internalApi(`/contract-drafts/${record.id}/versions`, "POST", { rowVersion: record.rowVersion, changeNote: n || null }) });
  const followup = (decision: "NEGOTIATE" | "CLOSE") => latest && ask({ title: decision === "NEGOTIATE" ? "Thương lượng tiếp" : "Đóng draft", action: decision === "NEGOTIATE" ? "Thương lượng tiếp" : "Đóng draft",
    description: decision === "NEGOTIATE" ? "Hồ sơ quay lại trạng thái thương lượng; bạn tạo phiên bản mới để sửa và gửi lại." : "Hồ sơ sẽ bị đóng và không thể gửi lại.",
    note: { label: "Ghi chú (tùy chọn)", max: 2000 },
    run: n => internalApi(`/contract-reviews/${latest.id}/followup`, "POST", { decision, note: n || null, rowVersion: latest.rowVersion }) });

  const decideApproval = (approve: boolean) => ask({
    title: approve ? "Phê duyệt hợp đồng" : "Từ chối phê duyệt",
    action: approve ? "Duyệt, chuyển sang ký" : "Từ chối phê duyệt",
    description: approve
      ? `Xác nhận đã kiểm tra hợp đồng ${record.code}, phiên bản ${record.versionNo}. Hồ sơ sẽ chuyển sang bước ký.`
      : "Ghi rõ lý do để lưu vào nhật ký hồ sơ.",
    note: approve
      ? { label: "Ghi chú kiểm tra (tùy chọn)", max: 2000 }
      : { label: "Lý do từ chối (bắt buộc)", required: true, max: 2000 },
    run: comment => internalApi(`/contract-drafts/${record.id}/internal-approval`, "POST", { approve, comment: comment || null }),
  });
  async function startSigning() {
    if (signerName.trim().length < 2) { setError("Nhập họ tên người đại diện ký (ít nhất 2 ký tự)."); return; }
    if (!signerPhone.trim()) { setError("Nhập số điện thoại người đại diện ký."); return; }
    setBusy(true); setError(""); setNotice("");
    try {
      await internalApi(`/contract-drafts/${record.id}/start-signing`, "POST", {
        signerName: signerName.trim(), signerPhone: signerPhone.trim(), signerTitle: signerTitle.trim() || null,
      });
      setNotice("Đã tạo phiên ký. Hợp đồng đang ở hàng đợi ký của công ty.");
      await load(); onChanged();
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  return <section className={ui.card} style={{ margin: "0 0 16px" }} aria-label="Gửi khách xem">
    <div className={s.workflowGuide} aria-label="Các bước ký hợp đồng">
      <div className={s.workflowGuideHeader}><div><strong>Quy trình ký hợp đồng</strong><p>Khách duyệt nội dung → quản lý duyệt bản chốt → công ty ký trước → khách hàng ký sau.</p></div>{approval?.signingStarted && !signingComplete && <Link className={ui.secondaryButton} href="/workspace/contracts/company-signing">Mở hàng đợi ký công ty</Link>}</div>
      <ol className={s.workflowSteps}>{workflowSteps.map((step, index) => <li key={step.title} className={`${s.workflowStep} ${step.done ? s.workflowStepDone : step.current ? s.workflowStepCurrent : ""}`}>
        <span className={s.workflowStepNumber}>{step.done ? <Check size={14}/> : index + 1}</span><div><strong>{step.title}</strong><p>{step.detail}</p></div>
      </li>)}</ol>
    </div>
    <div style={{ display: "flex", flexWrap: "wrap", gap: 10, alignItems: "center", justifyContent: "space-between" }}>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 8, alignItems: "center" }}>
        <strong style={{ fontSize: 14 }}>Phiên bản {record.versionNo}</strong>
        <span className={s.badge}>{documentStatusLabel[record.status] ?? record.status}</span>
        {latest && <span className={`${s.badge} ${["CHANGES_REQUESTED", "REJECTED"].includes(latest.status) ? s.warning : ""}`}>{reviewStatusLabel[latest.status] ?? latest.status}</span>}
        {record.locked && <span className={s.muted}>Đã khóa — chỉ xem</span>}
      </div>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
        <button className={ui.secondaryButton} disabled={busy || dirty} title={dirty ? "Lưu nháp trước khi tải lại trạng thái" : "Tải trạng thái khách hàng và phê duyệt mới nhất"} onClick={() => void refreshWorkflow()}><RefreshCw size={15}/>Làm mới trạng thái</button>
        {canSendToCustomer && <button className={ui.primaryButton} aria-describedby={record.sendBlockedReason ? "send-review-block-reason" : undefined} disabled={busy || dirty || !!record.sendBlockedReason} title={record.sendBlockedReason || (dirty ? "Lưu nháp trước khi gửi" : undefined)} onClick={send}><Send size={16}/>Gửi khách xem</button>}
        {canCreateVersion && <button className={ui.secondaryButton} disabled={busy} onClick={newVersion}><CopyPlus size={16}/>Tạo phiên bản mới để chỉnh sửa</button>}
        {awaitingDecision && <><button className={ui.secondaryButton} disabled={busy} onClick={() => followup("NEGOTIATE")}><Handshake size={16}/>Thương lượng tiếp</button>
          <button className={ui.secondaryButton} disabled={busy} onClick={() => followup("CLOSE")}><XCircle size={16}/>Đóng draft</button></>}
        {user.canManageAll && awaitingInternalApproval && <>
          <button className={ui.primaryButton} disabled={busy || dirty} onClick={() => decideApproval(true)}><Check size={16}/>Duyệt, chuyển sang ký</button>
          <button className={ui.secondaryButton} disabled={busy || dirty} onClick={() => decideApproval(false)}><XCircle size={16}/>Từ chối</button>
        </>}
        {canStartSigning && <button className={ui.primaryButton} disabled={busy || dirty || signerName.trim().length < 2 || !signerPhone.trim()} onClick={() => void startSigning()}>
          <Send size={16}/>Tạo phiên ký
        </button>}
        {latest && <button className={ui.secondaryButton} disabled={busy} title="Chỉ dùng khi khách chưa đổi mật khẩu tạm và mất tin nhắn"
          onClick={() => void resetCustomerPassword()}><KeyRound size={16}/>Cấp lại mật khẩu tạm</button>}
      </div>
    </div>
    {latest && <div className={s.customerPortalAccess}>
      <div><strong>Cách khách mở hợp đồng</strong><p>Khách mở Portal, đăng nhập bằng số điện thoại và mật khẩu, rồi chọn hợp đồng trong mục “Hợp đồng của bạn”. Link này mở trang đăng nhập, không phải link công khai riêng cho hợp đồng.</p><p className={s.muted}>Trạng thái “đang chờ khách” xác nhận yêu cầu đã được tạo, không xác nhận SMS đã giao thành công.</p></div>
      <a href={customerPortalLoginUrl} target="_blank" rel="noreferrer">Mở Portal khách hàng</a>
    </div>}
    {canSendToCustomer && record.sendBlockedReason && !canCreateVersion && !awaitingDecision && <div id="send-review-block-reason" className={s.muted} style={{ marginTop: 8 }}><p>Chưa thể gửi khách: {record.sendBlockedReason}</p>{record.content.missingFields.length > 0 && <ul style={{ margin: "6px 0 0", paddingLeft: 20 }}>{record.content.missingFields.map(field => <li key={field}>{field}</li>)}</ul>}</div>}
    {canSendToCustomer && dirty && !record.sendBlockedReason && <p className={s.muted} style={{ marginTop: 8 }}>Lưu nháp trước khi gửi khách.</p>}
    {record.canEdit && <p className={s.muted} style={{ marginTop: 8 }}>Khi bạn lưu nội dung đã thay đổi, hệ thống tự tạo phiên bản kế tiếp và giữ phiên bản trước trong lịch sử.</p>}
    {error && <p role="alert" className={ui.error} style={{ marginTop: 10 }}>{error}</p>}
    {notice && <p role="status" className={ui.notice} style={{ marginTop: 10 }}>{notice}</p>}
    {approval && <div className={ui.notice} style={{ marginTop: 10 }}>
      <strong>{approval.workflowName}:</strong>{" "}
      {approval.status === 0 ? "Đang chờ công ty kiểm tra" : approval.status === 1 ? "Đã duyệt, hồ sơ chuyển sang bước ký" : "Đã từ chối phê duyệt"}
      {approval.comment && <span> · {approval.comment}</span>}
    </div>}
    {canStartSigning && <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: 10, marginTop: 12 }}>
      <label className={s.label}>Người đại diện công ty *<input className={ui.field} required aria-invalid={signerName.trim().length < 2} maxLength={200} value={signerName} onChange={e => setSignerName(e.target.value)}/>{signerName.trim().length < 2 && <span role="alert" className={s.muted}>Nhập họ tên người đại diện ký (ít nhất 2 ký tự).</span>}</label>
      <label className={s.label}>Chức vụ<input className={ui.field} maxLength={100} value={signerTitle} onChange={e => setSignerTitle(e.target.value)}/></label>
      <label className={s.label}>Số điện thoại<input className={ui.field} inputMode="tel" maxLength={30} value={signerPhone} onChange={e => setSignerPhone(e.target.value)}/></label>
    </div>}

    {versions.length > 0 && <details style={{ marginTop: 12 }} open>
      <summary style={{ cursor: "pointer", fontWeight: 600, fontSize: 13 }}>Lịch sử phiên bản ({versions.length})</summary>
      <div style={{ display: "grid", gap: 8, marginTop: 10 }}>
        {versions.map(version => <div key={version.id} className={s.lineItem}>
          <header><strong>Phiên bản {version.versionNo}{version.isCurrent ? " · Đang chỉnh sửa / gửi khách" : ""}</strong><span className={s.muted}>{dateTime(version.createdAt)}</span></header>
          <p className={s.muted}>{version.changeNote || "Chưa có ghi chú thay đổi"}{version.locked ? " · Đã khóa sau khi gửi khách" : " · Có thể chỉnh sửa"}</p>
          {version.snapshotHash && <p className={s.muted}>Mã kiểm tra nội dung: {version.snapshotHash}</p>}
        </div>)}
      </div>
    </details>}
    {reviews.length > 0 && <details style={{ marginTop: 12 }} open={reviews.some(r => r.comments.some(c => c.fromCustomer && !c.resolved))}>
      <summary style={{ cursor: "pointer", fontWeight: 600, fontSize: 13 }}>Lịch sử gửi khách ({reviews.length})</summary>
      {reviews.map(r => <div key={r.id} className={s.lineItem} style={{ marginTop: 10 }}>
        <header><strong style={{ fontSize: 13 }}>Phiên bản {r.versionNo} · {reviewStatusLabel[r.status] ?? r.status}</strong><span className={s.muted}>{dateTime(r.sentAt)} · {r.sentByName}</span></header>
        {r.message && <p className={s.muted}>Lời nhắn: {r.message}</p>}
        {r.firstViewedAt && <p className={s.muted}>Khách mở lần đầu: {dateTime(r.firstViewedAt)}</p>}
        {r.customerNote && <p><strong>Ghi chú của khách{r.respondedAt ? ` (${dateTime(r.respondedAt)})` : ""}:</strong> {r.customerNote}</p>}
        {r.followupDecision && <p className={s.muted}>Quyết định: {r.followupDecision === "NEGOTIATE" ? "Thương lượng tiếp" : "Đóng draft"}{r.followupNote ? ` — ${r.followupNote}` : ""}</p>}
        {r.comments.map(c => <div key={c.id} style={{ borderLeft: "3px solid var(--border)", paddingLeft: 10, marginLeft: c.parentId ? 20 : 0, opacity: c.resolved ? 0.65 : 1 }}>
          <p style={{ fontSize: 13 }}><strong>{c.authorName}</strong>{c.termTitle && <span className={s.muted}> · {c.termTitle}</span>}<span className={s.muted}> · {dateTime(c.createdAt)}</span>{c.resolved && <span className={s.badge} style={{ marginLeft: 6 }}>Đã xử lý</span>}</p>
          <p style={{ whiteSpace: "pre-wrap" }}>{c.content}</p>
          {c.fromCustomer && <div style={{ display: "flex", gap: 8, margin: "4px 0 8px" }}>
            <button className={ui.secondaryButton} disabled={busy} onClick={() => { setReplyTo({ review: r.id, comment: c }); setReply(""); }}><MessageSquare size={14}/>Trả lời</button>
            {!c.resolved && <button className={ui.secondaryButton} disabled={busy} onClick={() => void act(() => internalApi(`/contract-comments/${c.id}/resolve`, "POST"))}><Check size={14}/>Đánh dấu đã xử lý</button>}
          </div>}
          {replyTo?.comment.id === c.id && <div style={{ display: "grid", gap: 8, marginBottom: 8 }}>
            <Textarea aria-label="Nội dung trả lời" maxLength={4000} value={reply} onChange={e => setReply(e.target.value)}/>
            <div style={{ display: "flex", gap: 8 }}><button className={ui.primaryButton} disabled={busy || !reply.trim()} onClick={() => void act(async () => {
              await internalApi(`/contract-reviews/${replyTo.review}/comments`, "POST", { termKey: c.termKey, parentCommentId: c.id, content: reply.trim() }); setReplyTo(null); })}>Gửi trả lời</button>
              <button className={ui.secondaryButton} onClick={() => setReplyTo(null)}>Hủy</button></div>
          </div>}
        </div>)}
      </div>)}
    </details>}

    <Dialog open={!!confirm} onOpenChange={v => { if (!busy && !v) setConfirm(null); }}><DialogContent>
      <DialogHeader><DialogTitle>{confirm?.title}</DialogTitle><DialogDescription>{confirm?.description}</DialogDescription></DialogHeader>
      {confirm?.note && <label className="grid gap-2 text-sm font-medium">{confirm.note.label}<Textarea maxLength={confirm.note.max} value={note} disabled={busy} onChange={e => setNote(e.target.value)}/></label>}
      <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={() => setConfirm(null)}>Hủy</Button>
        <Button type="button" disabled={busy || (!!confirm?.note?.required && !note.trim())} onClick={() => void run()}>{busy ? "Đang xử lý…" : confirm?.action}</Button></DialogFooter>
    </DialogContent></Dialog>
  </section>;
}
