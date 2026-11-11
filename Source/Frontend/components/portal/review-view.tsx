"use client";
import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowLeft, Check, MessageSquare, PenLine, XCircle } from "lucide-react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import DraftPreview from "@/components/internal/draft-preview";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import PortalHeader from "./portal-header";
import PortalSigningPanel from "./portal-signing-panel";
import { dateTime } from "@/services/internal-api";
import { customerApi, customerStatusLabel, handleAuthError, isOpenReview, type CustomerReviewDetail, type ReviewComment } from "@/services/customer-api";

type Decision = "APPROVE" | "REQUEST_CHANGES" | "REJECT";
const decisions: Record<Decision, { title: string; description: string; note: string; required: boolean }> = {
  APPROVE: { title: "Đồng ý hợp đồng", description: "Bạn xác nhận đồng ý với nội dung phiên bản này. Nhân viên sẽ kiểm tra nội bộ trước bước ký.", note: "Ghi chú (tùy chọn)", required: false },
  REQUEST_CHANGES: { title: "Yêu cầu sửa", description: "Nhân viên sẽ sửa và gửi lại phiên bản mới cho bạn.", note: "Nội dung cần sửa *", required: true },
  REJECT: { title: "Từ chối hợp đồng", description: "Bạn không đồng ý với hợp đồng này. Người phụ trách sẽ quyết định thương lượng tiếp hoặc đóng hồ sơ.", note: "Lý do từ chối *", required: true },
};

export default function ReviewView({ id }: { id: string }) {
  const router = useRouter();
  const [review, setReview] = useState<CustomerReviewDetail | null>(null); const [error, setError] = useState("");
  const [term, setTerm] = useState(""); const [comment, setComment] = useState(""); const [busy, setBusy] = useState(false);
  const [decision, setDecision] = useState<Decision | null>(null); const [note, setNote] = useState("");
  const fail = useCallback((e: unknown) => { if (handleAuthError(e, router.replace)) setError((e as Error).message); }, [router]);
  useEffect(() => { void customerApi<CustomerReviewDetail>(`/reviews/${id}`).then(setReview).catch(fail); }, [id, fail]);
  if (!review) return <><PortalHeader/><div className={s.empty}>{error ? <p role="alert">{error}</p> : "Đang tải hợp đồng…"}<Link href="/portal/contracts">Về danh sách</Link></div></>;
  const open = isOpenReview(review.status);
  async function sendComment() {
    setBusy(true); setError("");
    try { const c = await customerApi<ReviewComment>(`/reviews/${id}/comments`, "POST", { termKey: term || null, content: comment.trim() });
      setReview(r => r && { ...r, comments: [...r.comments, c] }); setComment(""); }
    catch (e) { fail(e); } finally { setBusy(false); }
  }
  async function respond() {
    if (!decision) return; setBusy(true); setError("");
    try { setReview(await customerApi<CustomerReviewDetail>(`/reviews/${id}/respond`, "POST", { decision, note: note.trim() || null, documentHash: review!.documentHash, rowVersion: review!.rowVersion })); setDecision(null); }
    catch (e) { fail(e); setDecision(null); } finally { setBusy(false); }
  }
  const byTerm = (key: string | null) => review.comments.filter(c => c.termKey === key);
  const terms = review.content.terms;
  return <><PortalHeader/><div className={s.page}>
    <div className={s.titleRow}><div><Link href="/portal/contracts" className={s.muted} style={{ display: "inline-flex", gap: 6, alignItems: "center" }}><ArrowLeft size={16}/>Hợp đồng của bạn</Link>
      <h1>{review.contractName}</h1><p className={s.muted}>Phiên bản {review.versionNo} · gửi lúc {dateTime(review.sentAt)}</p></div>
      <span className={`${s.badge} ${open ? s.warning : ""}`}>{customerStatusLabel[review.status] ?? review.status}</span></div>
    {review.message && <p className={ui.notice} style={{ marginBottom: 16 }}>Lời nhắn của nhân viên: {review.message}</p>}
    {error && <p role="alert" className={ui.error} style={{ marginBottom: 16 }}>{error}</p>}
    {!open && <p role="status" className={ui.notice} style={{ marginBottom: 16 }}>{review.respondedAt ? `Bạn đã phản hồi lúc ${dateTime(review.respondedAt)}.` : "Lần gửi này không còn nhận phản hồi."}{review.customerNote ? ` Ghi chú: ${review.customerNote}` : ""}</p>}
    {review.status === "APPROVED" && <PortalSigningPanel reviewId={id}/>}
    {open && <div style={{ display: "flex", flexWrap: "wrap", gap: 8, marginBottom: 16 }}>
      <button className={ui.primaryButton} disabled={busy} onClick={() => { setDecision("APPROVE"); setNote(""); }}><Check size={16}/>Đồng ý</button>
      <button className={ui.secondaryButton} disabled={busy} onClick={() => { setDecision("REQUEST_CHANGES"); setNote(""); }}><PenLine size={16}/>Yêu cầu sửa</button>
      <button className={ui.secondaryButton} disabled={busy} onClick={() => { setDecision("REJECT"); setNote(""); }}><XCircle size={16}/>Từ chối</button></div>}
    <div style={{ display: "grid", gap: 16, gridTemplateColumns: "repeat(auto-fit, minmax(min(100%, 380px), 1fr))", alignItems: "start" }}>
      <DraftPreview content={review.content}/>
      <section className={ui.card} aria-label="Góp ý theo điều khoản"><h2 style={{ display: "flex", gap: 8, alignItems: "center", fontSize: 16 }}><MessageSquare size={18}/>Góp ý</h2>
        {[{ key: null as string | null, title: "Góp ý chung" }, ...terms.map((t, i) => ({ key: t.key as string | null, title: `Điều ${i + 1}. ${t.title}` }))].map(group => {
          const list = byTerm(group.key); if (!list.length) return null;
          return <div key={group.key ?? "general"} style={{ marginTop: 12 }}><strong style={{ fontSize: 13 }}>{group.title}</strong>
            {list.map(c => <div key={c.id} style={{ borderLeft: "3px solid var(--border)", paddingLeft: 10, marginTop: 6, marginLeft: c.parentId ? 16 : 0 }}>
              <p style={{ fontSize: 12 }} className={s.muted}>{c.fromCustomer ? "Bạn" : c.authorName} · {dateTime(c.createdAt)}{c.resolved ? " · Đã xử lý" : ""}</p>
              <p style={{ whiteSpace: "pre-wrap" }}>{c.content}</p></div>)}</div>;
        })}
        {!review.comments.length && <p className={s.muted} style={{ marginTop: 8 }}>Chưa có góp ý.</p>}
        {open && <div style={{ display: "grid", gap: 8, marginTop: 16 }}>
          <label className={s.label}>Góp ý cho<select className={ui.field} value={term} onChange={e => setTerm(e.target.value)}><option value="">Góp ý chung</option>{terms.map((t, i) => <option key={t.key} value={t.key}>Điều {i + 1}. {t.title}</option>)}</select></label>
          <label className={s.label}>Nội dung<Textarea maxLength={4000} value={comment} onChange={e => setComment(e.target.value)} placeholder="Nhập góp ý của bạn…"/></label>
          <button className={ui.secondaryButton} disabled={busy || !comment.trim()} onClick={() => void sendComment()}>Gửi góp ý</button></div>}
      </section>
    </div>
    <Dialog open={!!decision} onOpenChange={v => { if (!busy && !v) setDecision(null); }}><DialogContent>
      {decision && <><DialogHeader><DialogTitle>{decisions[decision].title}</DialogTitle><DialogDescription>{decisions[decision].description}</DialogDescription></DialogHeader>
        <label className="grid gap-2 text-sm font-medium">{decisions[decision].note}<Textarea maxLength={4000} value={note} disabled={busy} onChange={e => setNote(e.target.value)}/></label>
        <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={() => setDecision(null)}>Hủy</Button>
          <Button type="button" disabled={busy || (decisions[decision].required && !note.trim())} onClick={() => void respond()}>{busy ? "Đang gửi…" : "Xác nhận"}</Button></DialogFooter></>}
    </DialogContent></Dialog>
  </div></>;
}
