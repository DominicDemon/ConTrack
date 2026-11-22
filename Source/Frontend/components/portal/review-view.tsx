"use client";
import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowLeft, Check, MessageSquare, Pencil, PenLine, Trash2, XCircle } from "lucide-react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import DraftPreview from "@/components/internal/draft-preview";
import ReviewWord from "./review-word";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import PortalHeader from "./portal-header";
import PortalSigningPanel from "./portal-signing-panel";
import { dateTime } from "@/services/internal-api";
import { CustomerApiError, customerApi, customerStatusLabel, handleAuthError, isOpenReview, type CustomerReviewDetail, type ReviewComment } from "@/services/customer-api";
import { onNotificationRefresh } from "../../../Shared/notification-navigation";

type Decision = "APPROVE" | "REQUEST_CHANGES" | "REJECT";
const decisions: Record<Decision, { title: string; description: string; note: string; required: boolean }> = {
  APPROVE: { title: "Đồng ý hợp đồng", description: "Bạn xác nhận đồng ý với nội dung phiên bản này. Nếu đây là lần đầu bạn đồng ý hợp đồng, hệ thống sẽ gửi hướng dẫn đăng nhập Cổng khách hàng qua email. Nhân viên sẽ kiểm tra bản chốt trước bước ký.", note: "Ghi chú (tùy chọn)", required: false },
  REQUEST_CHANGES: { title: "Gửi yêu cầu sửa", description: "Các góp ý theo mục của bạn được gửi cho nhân viên. Nhân viên sẽ sửa và gửi lại phiên bản mới.", note: "Ghi chú thêm (tùy chọn)", required: false },
  REJECT: { title: "Từ chối hợp đồng", description: "Bạn không đồng ý với hợp đồng này. Người phụ trách sẽ quyết định thương lượng tiếp hoặc đóng hồ sơ.", note: "Lý do từ chối *", required: true },
};
/** Parts of the contract a comment can be about besides a term (ContractReviewPolicy.CommentSections). */
const sections: [string, string][] = [["PARTIES", "Thông tin các bên"], ["DATES", "Thời hạn hợp đồng"], ["ITEMS", "Hạng mục và giá trị"], ["PAYMENT", "Lịch thanh toán"]];
/** One value per target: "" = general, "sec:<KEY>" = a section, "term:<key>" = a term. */
const targetOf = (c: ReviewComment) => c.termKey ? `term:${c.termKey}` : c.section ? `sec:${c.section}` : "";

/** onAuthLost marks a session opened from an emailed link: it replaces the list links and the password-login redirects. */
export default function ReviewView({ id, onAuthLost }: { id: string; onAuthLost?: () => void }) {
  const router = useRouter();
  const [review, setReview] = useState<CustomerReviewDetail | null>(null); const [error, setError] = useState("");
  const [term, setTerm] = useState(""); const [comment, setComment] = useState(""); const [busy, setBusy] = useState(false);
  const [decision, setDecision] = useState<Decision | null>(null); const [note, setNote] = useState("");
  /** The customer's own comment being changed in place: target ("", "sec:KEY" or "term:key") and text. */
  const [editing, setEditing] = useState<{ id: string; target: string; text: string } | null>(null);
  const fail = useCallback((e: unknown) => {
    if (onAuthLost && e instanceof CustomerApiError && [401, 403, 410].includes(e.status)) { onAuthLost(); return; }
    if (handleAuthError(e, router.replace)) setError((e as Error).message);
  }, [router, onAuthLost]);
  const [revision, setRevision] = useState(0);
  const markSigned = useCallback(() => setReview(current => current && { ...current, isSigned: true }), []);
  useEffect(() => onNotificationRefresh(() => { if (!busy) setRevision(value => value + 1); }), [busy]);
  useEffect(() => {
    let active = true;
    void customerApi<CustomerReviewDetail>(`/reviews/${id}`).then(value => {
      if (active) { setReview(value); setError(""); }
    }).catch(e => { if (active) fail(e); });
    return () => { active = false; };
  }, [id, fail, revision]);
  if (!review) return <><PortalHeader onLogout={onAuthLost}/><div className={s.empty}>{error ? <p role="alert">{error}</p> : "Đang tải hợp đồng…"}{!onAuthLost && <Link href="/portal/contracts">Về danh sách</Link>}</div></>;
  const open = !review.isSigned && isOpenReview(review.status);
  async function sendComment() {
    setBusy(true); setError("");
    try { const c = await customerApi<ReviewComment>(`/reviews/${id}/comments`, "POST", { termKey: term.startsWith("term:") ? term.slice(5) : null,
        section: term.startsWith("sec:") ? term.slice(4) : null, content: comment.trim() });
      setReview(r => r && { ...r, comments: [...r.comments, c] }); setComment(""); }
    catch (e) { fail(e); } finally { setBusy(false); }
  }
  const targetBody = (target: string) => ({ termKey: target.startsWith("term:") ? target.slice(5) : null, section: target.startsWith("sec:") ? target.slice(4) : null });
  async function saveEdit() {
    if (!editing) return; setBusy(true); setError("");
    try { const c = await customerApi<ReviewComment>(`/reviews/${id}/comments/${editing.id}`, "PUT", { ...targetBody(editing.target), content: editing.text.trim() });
      setReview(r => r && { ...r, comments: r.comments.map(x => x.id === c.id ? c : x) }); setEditing(null); }
    catch (e) { fail(e); } finally { setBusy(false); }
  }
  async function removeComment(commentId: string) {
    if (!window.confirm("Xóa góp ý này?")) return; setBusy(true); setError("");
    try { await customerApi(`/reviews/${id}/comments/${commentId}`, "DELETE"); setReview(r => r && { ...r, comments: r.comments.filter(x => x.id !== commentId) }); }
    catch (e) { fail(e); } finally { setBusy(false); }
  }
  async function respond() {
    if (!decision) return; setBusy(true); setError("");
    try { setReview(await customerApi<CustomerReviewDetail>(`/reviews/${id}/respond`, "POST", { decision, note: note.trim() || null, documentHash: review!.documentHash, rowVersion: review!.rowVersion })); setDecision(null); }
    catch (e) { fail(e); setDecision(null); } finally { setBusy(false); }
  }
  const terms = review.content.terms;
  const targets = [{ value: "", title: "Góp ý chung" }, ...sections.map(([key, title]) => ({ value: `sec:${key}`, title })),
    ...terms.map((t, i) => ({ value: `term:${t.key}`, title: `Điều ${i + 1}. ${t.title}`, fixed: !t.editable }))];
  // Comments the customer wrote on this version (carried-over ones from the previous version do not count).
  const mine = review.comments.filter(c => c.fromCustomer && !c.carried).length;
  // Agreeing and asking for changes exclude each other: once agreed, no more comments; once commented, no "Đồng ý".
  const approved = review.status === "APPROVED";
  const electronicReview = approved && review.content.partyType !== "BUSINESS";
  const preview = review.content.templateSnapshot?.wordStorageKey
    ? <ReviewWord key={review.id} id={review.id}/> : <DraftPreview content={review.content}/>;
  /** Same rule as the API: own comment on this version, review open, not handled or answered by the staff yet. */
  const canChange = (c: ReviewComment) => open && c.fromCustomer && !c.carried && !c.resolved && !review.comments.some(x => x.parentId === c.id);
  return <><PortalHeader onLogout={onAuthLost}/><div className={s.page}>
    <div className={s.titleRow}><div>{!onAuthLost && <Link href="/portal/contracts" className={s.muted} style={{ display: "inline-flex", gap: 6, alignItems: "center" }}><ArrowLeft size={16}/>Hợp đồng của bạn</Link>}
      <h1>{review.contractName}</h1><p className={s.muted}>Phiên bản {review.versionNo} · gửi lúc {dateTime(review.sentAt)}</p></div>
      <span className={`${s.badge} ${open ? s.warning : ""}`}>{review.isSigned ? "Đã ký" : customerStatusLabel[review.status] ?? review.status}</span></div>
    {review.message && <p className={ui.notice} style={{ marginBottom: 16 }}>Lời nhắn của nhân viên: {review.message}</p>}
    {error && <p role="alert" className={ui.error} style={{ marginBottom: 16 }}>{error}</p>}
    {!open && <p role="status" className={ui.notice} style={{ marginBottom: 16 }}>{review.respondedAt ? `Bạn đã phản hồi lúc ${dateTime(review.respondedAt)}.` : "Lần gửi này không còn nhận phản hồi."}{review.customerNote ? ` Ghi chú: ${review.customerNote}` : ""}</p>}
    {review.status === "APPROVED" && (review.content.partyType === "BUSINESS"
      ? <p role="status" className={ui.notice} style={{ marginBottom: 16 }}>{review.isSigned ? "Công ty đã xác nhận bản ký giấy của hợp đồng này." : "Sau khi công ty duyệt, nhân viên sẽ gửi bản in để đại diện doanh nghiệp ký và đóng dấu."}</p>
      : <PortalSigningPanel key={id} reviewId={id} onSigned={markSigned}>{preview}</PortalSigningPanel>)}
    {open && <div style={{ display: "flex", flexWrap: "wrap", gap: 8, marginBottom: 16 }}>
      {mine === 0 && <button className={ui.primaryButton} disabled={busy} onClick={() => { setDecision("APPROVE"); setNote(""); }}><Check size={16}/>Đồng ý</button>}
      <button className={ui.secondaryButton} disabled={busy || mine === 0} title={mine === 0 ? "Thêm góp ý cho mục cần sửa trước" : undefined}
        onClick={() => { setDecision("REQUEST_CHANGES"); setNote(""); }}><PenLine size={16}/>Gửi yêu cầu sửa{mine ? ` (${mine} góp ý)` : ""}</button>
      {mine > 0 && <p className={s.muted} style={{ flexBasis: "100%" }}>Bạn đã góp ý cho phiên bản này nên không chọn Đồng ý. Bấm Gửi yêu cầu sửa để nhân viên chỉnh lại hợp đồng.</p>}
      <button className={ui.secondaryButton} disabled={busy} onClick={() => { setDecision("REJECT"); setNote(""); }}><XCircle size={16}/>Từ chối</button></div>}
    {!electronicReview && <div style={{ display: "grid", gap: 16, gridTemplateColumns: "repeat(auto-fit, minmax(min(100%, 380px), 1fr))", alignItems: "start" }}>
      {preview}
      {!approved && <section className={ui.card} aria-label="Góp ý và yêu cầu sửa theo mục"><h2 style={{ display: "flex", gap: 8, alignItems: "center", fontSize: 16 }}><MessageSquare size={18}/>Góp ý / yêu cầu sửa theo mục</h2>
        {open && <p className={s.muted} style={{ marginTop: 4 }}>Chọn mục cần sửa (thông tin các bên, hạng mục, thanh toán, từng điều khoản…), ghi nội dung rồi bấm Thêm góp ý. Khi xong, bấm Gửi yêu cầu sửa.</p>}
        {targets.map(group => {
          const list = review.comments.filter(c => targetOf(c) === group.value); if (!list.length) return null;
          return <div key={group.value || "general"} style={{ marginTop: 12 }}><strong style={{ fontSize: 13 }}>{group.title}</strong>
            {list.map(c => <div key={c.id} style={{ borderLeft: "3px solid var(--border)", paddingLeft: 10, marginTop: 6, marginLeft: c.parentId ? 16 : 0 }}>
              <p style={{ fontSize: 12 }} className={s.muted}>{c.fromCustomer ? "Bạn" : c.authorName} · {dateTime(c.createdAt)}{c.resolved ? " · Đã xử lý" : ""}</p>
              {editing?.id === c.id ? <div style={{ display: "grid", gap: 6, marginTop: 4 }}>
                <select aria-label="Mục của góp ý" className={ui.field} value={editing.target} onChange={e => setEditing({ ...editing, target: e.target.value })}>{targets.filter(t => !("fixed" in t && t.fixed)).map(t => <option key={t.value} value={t.value}>{t.title}</option>)}</select>
                <Textarea aria-label="Nội dung góp ý" maxLength={4000} value={editing.text} onChange={e => setEditing({ ...editing, text: e.target.value })}/>
                <div style={{ display: "flex", gap: 8 }}><button className={ui.primaryButton} disabled={busy || !editing.text.trim()} onClick={() => void saveEdit()}>Lưu</button>
                  <button className={ui.secondaryButton} disabled={busy} onClick={() => setEditing(null)}>Hủy</button></div></div>
              : <><p style={{ whiteSpace: "pre-wrap" }}>{c.content}</p>
                {canChange(c) && <div style={{ display: "flex", gap: 8, marginTop: 4 }}>
                  <button className={ui.secondaryButton} disabled={busy} onClick={() => setEditing({ id: c.id, target: targetOf(c), text: c.content })}><Pencil size={14}/>Sửa</button>
                  <button className={ui.secondaryButton} disabled={busy} onClick={() => void removeComment(c.id)}><Trash2 size={14}/>Xóa</button></div>}</>}
            </div>)}</div>;
        })}
        {!review.comments.length && <p className={s.muted} style={{ marginTop: 8 }}>Chưa có góp ý.</p>}
        {open && <div style={{ display: "grid", gap: 8, marginTop: 16 }}>
          <label className={s.label}>Mục<select className={ui.field} value={term} onChange={e => setTerm(e.target.value)}>{targets.filter(t => !("fixed" in t && t.fixed)).map(t => <option key={t.value} value={t.value}>{t.title}</option>)}</select></label>
          <label className={s.label}>Góp ý / nội dung cần sửa<Textarea maxLength={4000} value={comment} onChange={e => setComment(e.target.value)} placeholder="Ví dụ: Đề nghị sửa đơn giá hạng mục 2 thành…"/></label>
          <button className={ui.secondaryButton} disabled={busy || !comment.trim()} onClick={() => void sendComment()}>Thêm góp ý</button></div>}
      </section>}
    </div>}
    <Dialog open={!!decision} onOpenChange={v => { if (!busy && !v) setDecision(null); }}><DialogContent>
      {decision && <><DialogHeader><DialogTitle>{decisions[decision].title}</DialogTitle><DialogDescription>{decisions[decision].description}</DialogDescription></DialogHeader>
        <label className="grid gap-2 text-sm font-medium">{decisions[decision].note}<Textarea maxLength={4000} value={note} disabled={busy} onChange={e => setNote(e.target.value)}/></label>
        <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={() => setDecision(null)}>Hủy</Button>
          <Button type="button" disabled={busy || (decisions[decision].required && !note.trim())} onClick={() => void respond()}>{busy ? "Đang gửi…" : "Xác nhận"}</Button></DialogFooter></>}
    </DialogContent></Dialog>
  </div></>;
}
