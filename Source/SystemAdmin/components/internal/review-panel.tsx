"use client";
import { focusIssue } from "../../../Shared/draft-validation";
import DraftPreview from "./draft-preview";

import { useCallback, useEffect, useState } from "react";
import { Check, CopyPlus, KeyRound, MessageSquare, Send, XCircle, Handshake } from "lucide-react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { internalApi, dateTime, documentStatusLabel, reviewStatusLabel, InternalApiError, type DraftRecord, type ReviewSummary, type ReviewComment } from "@/services/internal-api";
import { internalStyles as ui } from "./internal-theme";
import s from "./workspace.module.css";

type Confirm = { title: string; description: string; action: string; note?: { label: string; required?: boolean; max: number }; run: (note: string) => Promise<unknown> };

/** Customer review actions and history for a saved draft. All state transitions are re-checked by the API. */
export default function ReviewPanel({ record, dirty, onChanged }: { record: DraftRecord; dirty: boolean; onChanged: () => void }) {
  const [confirmed, setConfirmed] = useState(false);
  const [reviews, setReviews] = useState<ReviewSummary[]>([]);
  const [error, setError] = useState(""); const [notice, setNotice] = useState("");
  const [confirm, setConfirm] = useState<Confirm | null>(null); const [note, setNote] = useState(""); const [busy, setBusy] = useState(false);
  const [replyTo, setReplyTo] = useState<{ review: string; comment: ReviewComment } | null>(null); const [reply, setReply] = useState("");
  const load = useCallback(() => internalApi<ReviewSummary[]>(`/contract-drafts/${record.id}/reviews`).then(setReviews).catch(e => setError(e.message)), [record.id]);
  useEffect(() => { void load(); }, [load, record.rowVersion]);
  const latest = reviews[0];
  const canCreateVersion = record.status === 1 && record.locked && (latest?.status === "CHANGES_REQUESTED" || (latest?.status === "REJECTED" && latest.followupDecision === "NEGOTIATE"));
  const awaitingDecision = record.status === 7 && latest?.status === "REJECTED" && !latest.followupDecision;

  function ask(c: Confirm) { setConfirmed(false); setConfirm(c); setNote(""); setError(""); setNotice(""); }
  async function run() {
    if (!confirm) return; setBusy(true); setError("");
    try { await confirm.run(note.trim()); setConfirm(null); await load(); onChanged(); }
    catch (e) { setError((e as Error).message); setConfirm(null); if(e instanceof InternalApiError && e.issues.length) focusIssue(e.issues[0].path); } finally { setBusy(false); }
  }
  async function act(action: () => Promise<unknown>, done?: string) {
    setBusy(true); setError(""); setNotice("");
    try { await action(); if (done) setNotice(done); await load(); } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }

  const send = () => ask({ title: "Gửi khách xem", action: "Gửi khách xem",
    description: `Gửi phiên bản ${record.versionNo} cho ${record.customerName} (${record.customerPhoneMasked}). Phiên bản sẽ bị khóa; khách nhận SMS để đăng nhập và phản hồi.`,
    note: { label: "Lời nhắn cho khách (tùy chọn)", max: 1000 },
    run: n => internalApi(`/contract-drafts/${record.id}/send-review`, "POST", { message: n || null, rowVersion: record.rowVersion, versionRowVersion: record.versionRowVersion, contentConfirmed: true }) });
  const newVersion = () => ask({ title: "Tạo phiên bản mới", action: "Tạo phiên bản", description: `Sao chép phiên bản ${record.versionNo} đã gửi thành phiên bản mới để chỉnh sửa và gửi lại.`,
    note: { label: "Ghi chú thay đổi (tùy chọn)", max: 1000 },
    run: n => internalApi(`/contract-drafts/${record.id}/versions`, "POST", { rowVersion: record.rowVersion, changeNote: n || null }) });
  const followup = (decision: "NEGOTIATE" | "CLOSE") => latest && ask({ title: decision === "NEGOTIATE" ? "Thương lượng tiếp" : "Đóng draft", action: decision === "NEGOTIATE" ? "Thương lượng tiếp" : "Đóng draft",
    description: decision === "NEGOTIATE" ? "Hồ sơ quay lại trạng thái thương lượng; bạn tạo phiên bản mới để sửa và gửi lại." : "Hồ sơ sẽ bị đóng và không thể gửi lại.",
    note: { label: "Ghi chú (tùy chọn)", max: 2000 },
    run: n => internalApi(`/contract-reviews/${latest.id}/followup`, "POST", { decision, note: n || null, rowVersion: latest.rowVersion }) });

  return <section className={ui.card} style={{ margin: "0 0 16px" }} aria-label="Gửi khách xem">
    <div style={{ display: "flex", flexWrap: "wrap", gap: 10, alignItems: "center", justifyContent: "space-between" }}>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 8, alignItems: "center" }}>
        <strong style={{ fontSize: 14 }}>Phiên bản {record.versionNo}</strong>
        <span className={s.badge}>{documentStatusLabel[record.status] ?? record.status}</span>
        {latest && <span className={`${s.badge} ${["CHANGES_REQUESTED", "REJECTED"].includes(latest.status) ? s.warning : ""}`}>{reviewStatusLabel[latest.status] ?? latest.status}</span>}
        {record.locked && <span className={s.muted}>Đã khóa — chỉ xem</span>}
      </div>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
        <button type="button" className={ui.primaryButton} disabled={busy || dirty || !!record.sendBlockedReason}
          aria-describedby="review-send-guidance" title={dirty ? "Lưu nháp trước khi gửi" : record.sendBlockedReason || undefined} onClick={send}><Send size={16}/>Gửi khách xem</button>
        {canCreateVersion && <button className={ui.secondaryButton} disabled={busy} onClick={newVersion}><CopyPlus size={16}/>Tạo phiên bản mới</button>}
        {awaitingDecision && <><button className={ui.secondaryButton} disabled={busy} onClick={() => followup("NEGOTIATE")}><Handshake size={16}/>Thương lượng tiếp</button>
          <button className={ui.secondaryButton} disabled={busy} onClick={() => followup("CLOSE")}><XCircle size={16}/>Đóng draft</button></>}
        {latest && <button className={ui.secondaryButton} disabled={busy} title="Chỉ dùng khi khách chưa đổi mật khẩu tạm và mất tin nhắn"
          onClick={() => void act(() => internalApi(`/customers/${record.content.customerId}/reset-temp-password`, "POST"), "Đã xếp SMS mật khẩu tạm mới cho khách.")}><KeyRound size={16}/>Cấp lại mật khẩu tạm</button>}
      </div>
    </div>
    <div id="review-send-guidance" className={s.muted} style={{ marginTop: 8 }} aria-live="polite">
      {dirty ? <p>Lưu nháp để cập nhật thông tin và kiểm tra điều kiện gửi khách.</p> : record.sendBlockedReason ? <>
        <p>Chưa thể gửi khách: {record.sendBlockedReason}</p>
        {!!record.content.missingFields.length && <p>Cần bổ sung: {record.content.missingFields.join(", ")}.</p>}
      </> : <p>Nháp đã đủ thông tin. Bạn có thể gửi phiên bản này cho khách xem và phản hồi.</p>}
    </div>
    {(record.issues ?? []).some(i => i.path.startsWith("milestones")) && <button type="button" className={ui.secondaryButton} onClick={() => focusIssue("milestones")}>Đi tới lịch thanh toán</button>}
    {(record.carriedComments?.length ?? 0) > 0 && <section className={ui.notice}><strong>Góp ý chuyển tiếp sang phiên bản này</strong>{record.carriedComments?.map(c => <div key={c.id}><p>{c.content} · chuyển từ bản trước · {c.resolved ? "Đã xử lý" : "Chưa xử lý"}</p>{!c.resolved && <button type="button" disabled={busy} className={ui.secondaryButton} onClick={() => void act(async () => { await internalApi(`/contract-comments/${c.id}/resolve`,"POST"); onChanged(); })}>Đánh dấu đã xử lý</button>}</div>)}</section>}
    {error && <p role="alert" className={ui.error} style={{ marginTop: 10 }}>{error}</p>}
    {notice && <p role="status" className={ui.notice} style={{ marginTop: 10 }}>{notice}</p>}

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

    <Dialog open={!!confirm} onOpenChange={v => { if (!busy && !v) setConfirm(null); }}><DialogContent className="sm:max-w-4xl max-h-[90vh] overflow-y-auto">
      <DialogHeader><DialogTitle>{confirm?.title}</DialogTitle><DialogDescription>{confirm?.description}</DialogDescription></DialogHeader>
      {confirm?.action === "Gửi khách xem" && <><DraftPreview content={record.content} code={record.code}/><label className="flex gap-2 text-sm"><input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)}/>Tôi đã kiểm tra bên giao dịch, số tiền, lịch thanh toán và các nội dung viết cố định trong mẫu.</label></>}{confirm?.note && <label className="grid gap-2 text-sm font-medium">{confirm.note.label}<Textarea maxLength={confirm.note.max} value={note} disabled={busy} onChange={e => setNote(e.target.value)}/></label>}
      <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={() => setConfirm(null)}>Hủy</Button>
        <Button type="button" disabled={busy || (confirm?.action === "Gửi khách xem" && !confirmed) || (!!confirm?.note?.required && !note.trim())} onClick={() => void run()}>{busy ? "Đang xử lý…" : confirm?.action}</Button></DialogFooter>
    </DialogContent></Dialog>
  </section>;
}
