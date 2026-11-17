"use client";
import { useEffect, useState } from "react";
import { internalApi, dateTime, type DraftRecord } from "@/services/internal-api";
import { useInternalUser } from "./internal-shell";
import { internalStyles as ui } from "./internal-theme";

type Event = { id: string; action: string; actor: string; at: string; data: string | null };
type Approval = { stage: string; rowVersion: string | null; canSubmit: boolean; canDecide: boolean; history: Event[] };
const stages: Record<string, string> = { DRAFT: "Sale chuẩn bị", OFFICE: "Chờ Hành chính kiểm tra", DIRECTOR: "Chờ Sếp duyệt", APPROVED: "Đã duyệt — Sale có thể gửi khách", RETURNED: "Trả về chỉnh sửa", CUSTOMER_SENT: "Đã gửi — chờ khách xem", CUSTOMER_VIEWED: "Khách đang xem hợp đồng", CUSTOMER_APPROVED: "Khách đã đồng ý — chờ ký", CUSTOMER_CHANGES_REQUESTED: "Khách yêu cầu sửa — Sale tạo phiên bản mới", CUSTOMER_REJECTED: "Khách từ chối — Sale xử lý phản hồi", CUSTOMER_REVOKED: "Đã thu hồi lần gửi", CUSTOMER_SUPERSEDED: "Lần gửi đã được thay thế" };
const actions: Record<string, string> = { SUBMIT: "Trình duyệt", APPROVE: "Duyệt", RETURN: "Trả lại", COMMENT: "Nhận xét", RESOLVE: "Đã xử lý nhận xét" };

export default function ApprovalPanel({ record, dirty, onChanged }: { record: DraftRecord; dirty: boolean; onChanged: () => void }) {
  const user = useInternalUser();
  const [approval, setApproval] = useState<Approval | null>(null);
  const [note, setNote] = useState("");
  const [termKey, setTermKey] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    let active = true;
    internalApi<Approval>(`/contract-drafts/${record.id}/approval`).then(value => { if (active) setApproval(value); }).catch(e => { if (active) setError(e.message); });
    return () => { active = false; };
  }, [record.id, record.rowVersion]);
  async function act(action: string, resolveId?: string) {
    setBusy(true); setError("");
    try {
      const comment = action === "comment";
      await internalApi(`/contract-drafts/${record.id}/${comment ? "internal-comments" : `approval/${action}`}`, "POST", {
        rowVersion: record.rowVersion, versionRowVersion: record.versionRowVersion, approvalRowVersion: approval?.rowVersion,
        note, content: note, termKey: termKey || null, resolveId: resolveId || null,
      });
      setNote(""); onChanged();
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  const resolved = new Set(approval?.history.filter(e => e.action === "RESOLVE").map(e => String(JSON.parse(e.data || "{}").resolveId)));
  return <section className={ui.card} aria-label="Duyệt nội bộ" style={{ padding: 20, marginBottom: 16 }}>
    <h2>Duyệt nội bộ</h2><p role="status">{approval ? stages[approval.stage] : "Đang tải tiến độ…"}</p>
    {error && <p role="alert" className={ui.error}>{error}</p>}
    <fieldset disabled={busy || dirty} style={{ border: 0, padding: 0 }}>
      <label>Nhận xét cho<select className={ui.field} value={termKey} onChange={e => setTermKey(e.target.value)}><option value="">Toàn bộ hợp đồng</option>{record.content.terms.map(t => <option key={t.key} value={t.key}>{t.title}</option>)}</select></label>
      <label>Nội dung nhận xét / lý do trả lại<textarea className={ui.field} maxLength={1000} value={note} onChange={e => setNote(e.target.value)}/></label>
      <div style={{ display: "flex", gap: 12, flexWrap: "wrap", marginTop: 12 }}>
        <button className={ui.secondaryButton} disabled={!note.trim()} onClick={() => void act("comment")}>Gửi nhận xét nội bộ</button>
        {approval?.canSubmit && <button className={ui.primaryButton} onClick={() => void act("submit")}>Trình Hành chính</button>}
        {approval?.canDecide && <><button className={ui.primaryButton} onClick={() => void act("approve")}>{approval.stage === "OFFICE" ? "Duyệt và trình Sếp" : "Duyệt cho gửi khách"}</button><button className={ui.secondaryButton} disabled={!note.trim()} onClick={() => void act("return")}>Trả lại chỉnh sửa</button></>}
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
