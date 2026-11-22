"use client";
import { useEffect, useState } from "react";
import { onNotificationRefresh } from "../../../Shared/notification-navigation";
import { Plus, Trash2 } from "lucide-react";
import { internalApi, dateTime, type DraftRecord } from "@/services/internal-api";
import { useInternalUser } from "./internal-shell";
import { internalStyles as ui } from "./internal-theme";

type Event = { id: string; action: string; actor: string; at: string; data: string | null };
type Approval = { stage: string; rowVersion: string | null; canSubmit: boolean; canDecide: boolean; history: Event[] };
const stages: Record<string, string> = { DRAFT: "Sale chuẩn bị", OFFICE: "Chờ Hành chính duyệt", DIRECTOR: "Đã duyệt — Sale có thể gửi khách", APPROVED: "Đã duyệt — Sale có thể gửi khách", RETURNED: "Trả về chỉnh sửa", CUSTOMER_SENT: "Đã gửi — chờ khách xem", CUSTOMER_VIEWED: "Khách đang xem hợp đồng", CUSTOMER_APPROVED: "Khách đã đồng ý — Hành chính duyệt bản chốt rồi Sếp ký (xem khung Quy trình ký bên dưới)", CUSTOMER_CHANGES_REQUESTED: "Khách yêu cầu sửa — Sale tạo phiên bản mới", CUSTOMER_REJECTED: "Khách từ chối — Sale xử lý phản hồi", CUSTOMER_REVOKED: "Đã thu hồi lần gửi", CUSTOMER_SUPERSEDED: "Lần gửi đã được thay thế" };
const actions: Record<string, string> = { SUBMIT: "Trình duyệt", APPROVE: "Duyệt", RETURN: "Trả lại", COMMENT: "Nhận xét", RESOLVE: "Đã xử lý nhận xét" };

export default function ApprovalPanel({ record, dirty, onChanged }: { record: DraftRecord; dirty: boolean; onChanged: () => void }) {
  const user = useInternalUser();
  const [approval, setApproval] = useState<Approval | null>(null);
  const [note, setNote] = useState("");
  /** Several comments at once, each about the whole contract or one part of it. */
  const [comments, setComments] = useState<{ id: string; termKey: string; text: string }[]>(() => [{ id: crypto.randomUUID(), termKey: "", text: "" }]);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [refresh, setRefresh] = useState(0);
  useEffect(() => onNotificationRefresh(() => setRefresh(value => value + 1)), []);
  useEffect(() => {
    let active = true;
    internalApi<Approval>(`/contract-drafts/${record.id}/approval`).then(value => { if (active) setApproval(value); }).catch(e => { if (active) setError(e.message); });
    return () => { active = false; };
  }, [record.id, record.rowVersion, refresh]);
  const base = () => ({ rowVersion: record.rowVersion, versionRowVersion: record.versionRowVersion, approvalRowVersion: approval?.rowVersion });
  async function act(action: string, resolveId?: string) {
    setBusy(true); setError("");
    try {
      if (resolveId) await internalApi(`/contract-drafts/${record.id}/internal-comments`, "POST", { ...base(), content: null, termKey: null, resolveId });
      else await internalApi(`/contract-drafts/${record.id}/approval/${action}`, "POST", { ...base(), note });
      setNote(""); onChanged();
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  const filled = comments.filter(c => c.text.trim());
  // All comments go in one request (one row version check); on error the form keeps them so nothing is lost.
  async function sendComments() {
    setBusy(true); setError("");
    try {
      await internalApi(`/contract-drafts/${record.id}/internal-comments`, "POST", { ...base(), content: null, termKey: null, resolveId: null,
        items: filled.map(c => ({ content: c.text.trim(), termKey: c.termKey || null })) });
      setComments([{ id: crypto.randomUUID(), termKey: "", text: "" }]); onChanged();
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  const setComment = (id: string, patch: Partial<{ termKey: string; text: string }>) => setComments(old => old.map(c => c.id === id ? { ...c, ...patch } : c));
  const resolved = new Set(approval?.history.filter(e => e.action === "RESOLVE").map(e => String(JSON.parse(e.data || "{}").resolveId)));
  return <section className={ui.card} aria-label="Duyệt nội bộ" style={{ padding: 20, marginBottom: 16 }}>
    <h2>Duyệt nội bộ</h2><p role="status">{approval ? stages[approval.stage] : "Đang tải tiến độ…"}</p>
    {error && <p role="alert" className={ui.error}>{error}</p>}
    <fieldset disabled={busy || dirty} style={{ border: 0, padding: 0 }}>
      <div style={{ display: "grid", gap: 10 }}>
        <strong style={{ fontSize: 14 }}>Nhận xét nội bộ</strong>
        {comments.map((c, index) => <div key={c.id} style={{ display: "grid", gap: 6, padding: 10, border: "1px solid var(--border)", borderRadius: 8 }}>
          <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
            <span style={{ fontSize: 13, fontWeight: 600 }}>Nhận xét {index + 1}</span>
            <select aria-label={`Nhận xét ${index + 1} cho phần`} className={ui.field} style={{ flex: 1, minWidth: 200 }} value={c.termKey} onChange={e => setComment(c.id, { termKey: e.target.value })}>
              <option value="">Toàn bộ hợp đồng</option>{record.content.terms.map(t => <option key={t.key} value={t.key}>{t.title}</option>)}</select>
            {comments.length > 1 && <button type="button" className={ui.secondaryButton} aria-label={`Xóa nhận xét ${index + 1}`} onClick={() => setComments(old => old.filter(x => x.id !== c.id))}><Trash2 size={15}/></button>}
          </div>
          <textarea aria-label={`Nội dung nhận xét ${index + 1}`} className={ui.field} maxLength={4000} value={c.text} onChange={e => setComment(c.id, { text: e.target.value })}/>
        </div>)}
        <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
          <button type="button" className={ui.secondaryButton} disabled={comments.length >= 20} onClick={() => setComments(old => [...old, { id: crypto.randomUUID(), termKey: "", text: "" }])}><Plus size={15}/>Thêm nhận xét</button>
          <button type="button" className={ui.secondaryButton} disabled={!filled.length} onClick={() => void sendComments()}>Gửi {filled.length > 1 ? `${filled.length} nhận xét` : "nhận xét"} nội bộ</button>
        </div>
      </div>
      {(approval?.canSubmit || approval?.canDecide) && <label style={{ display: "grid", gap: 6, marginTop: 14 }}><strong style={{ fontSize: 14 }}>Ghi chú duyệt / lý do trả lại{approval?.canDecide ? " (bắt buộc khi trả lại)" : ""}</strong><textarea className={ui.field} maxLength={1000} value={note} onChange={e => setNote(e.target.value)}/></label>}
      <div style={{ display: "flex", gap: 12, flexWrap: "wrap", marginTop: 12 }}>
        {approval?.canSubmit && <button className={ui.primaryButton} onClick={() => void act("submit")}>Trình Hành chính</button>}
        {approval?.canDecide && <><button className={ui.primaryButton} onClick={() => void act("approve")}>Duyệt cho gửi khách</button><button className={ui.secondaryButton} disabled={!note.trim()} onClick={() => void act("return")}>Trả lại chỉnh sửa</button></>}
      </div>
      {dirty && <p>Lưu thay đổi trước khi thao tác.</p>}
      <ol>{approval?.history.map(event => {
        const data = JSON.parse(event.data || "{}");
        return <li key={event.id}><strong>{actions[event.action] ?? event.action}</strong> · {event.actor} · {dateTime(event.at)} · Phiên bản {data.version}
          {data.termKey && <p>Điều khoản {data.termKey}</p>}{data.note && <p>{data.note}</p>}
          {event.action === "COMMENT" && (resolved.has(event.id) ? <span>Đã xử lý</span> : user.canCheck && <button className={ui.secondaryButton} onClick={() => void act("comment", event.id)}>Đánh dấu đã xử lý</button>)}
        </li>;
      })}</ol>
    </fieldset>
  </section>;
}
