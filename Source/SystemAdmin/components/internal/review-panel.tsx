"use client";
import { sendIssues as validateSend, focusIssue } from "../../../Shared/draft-validation";
import DraftPreview from "./draft-preview";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { Check, CopyPlus, KeyRound, Mail, MessageSquare, Send, XCircle, Handshake, RefreshCw, Printer, Upload, UserPlus } from "lucide-react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { internalApi, dateTime, documentStatusLabel, reviewStatusLabel, InternalApiError, type DraftRecord, type DraftVersionHistoryItem, type ReviewSummary, type ReviewComment, type InternalApprovalState, type SigningMonitorDetail } from "@/services/internal-api";
import { useInternalUser } from "./internal-shell";
import { internalStyles as ui } from "./internal-theme";
import s from "./workspace.module.css";

type Confirm = { title: string; description: string; action: string; note?: { label: string; required?: boolean; max: number }; expiry?: boolean; run: (note: string, linkExpiresAt: string) => Promise<unknown> };
const inSevenDays = () => { const d = new Date(Date.now() + 7 * 864e5); return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16); };
const customerPortalLoginUrl = process.env.NEXT_PUBLIC_CUSTOMER_PORTAL_URL || "http://localhost:3000/portal/login";

/** Customer review actions and history for a saved draft. All state transitions are re-checked by the API. */
export default function ReviewPanel({ record, dirty, onChanged, onLocate = focusIssue }: { onLocate?: (path:string) => void; record: DraftRecord; dirty: boolean; onChanged: () => void }) {
  const user = useInternalUser();
  const sendIssues = validateSend(record.content);
  const [confirmed, setConfirmed] = useState(false);
  const [reviews, setReviews] = useState<ReviewSummary[]>([]);
  const [versions, setVersions] = useState<DraftVersionHistoryItem[]>([]);
  const [approval, setApproval] = useState<InternalApprovalState | null>(null);
  /** Hành chính's approval before sending (the Duyệt nội bộ panel): DRAFT, OFFICE, RETURNED, APPROVED or CUSTOMER_*. */
  const [preSend, setPreSend] = useState<{ stage: string } | null>(null);
  const [signingProgress, setSigningProgress] = useState<SigningMonitorDetail | null>(null);
  const [error, setError] = useState(""); const [notice, setNotice] = useState("");
  const [confirm, setConfirm] = useState<Confirm | null>(null); const [note, setNote] = useState(""); const [busy, setBusy] = useState(false); const [expiry, setExpiry] = useState("");
  const [replyTo, setReplyTo] = useState<{ review: string; comment: ReviewComment } | null>(null); const [reply, setReply] = useState("");
  const [signerName, setSignerName] = useState(record.content.fields.PROVIDER_REPRESENTATIVE_NAME ?? "");
  const [signerTitle, setSignerTitle] = useState(record.content.fields.PROVIDER_REPRESENTATIVE_TITLE ?? "");
  const [signerPhone, setSignerPhone] = useState(record.content.fields.PROVIDER_PHONE ?? "");
  const fetchWorkflow = useCallback(async () => {
    const [reviewRows, approvalState, versionRows, preSendState] = await Promise.all([
      internalApi<ReviewSummary[]>(`/contract-drafts/${record.id}/reviews`),
      internalApi<InternalApprovalState | null>(`/contract-drafts/${record.id}/internal-approval`),
      internalApi<DraftVersionHistoryItem[]>(`/contract-drafts/${record.id}/versions`),
      internalApi<{ stage: string }>(`/contract-drafts/${record.id}/approval`),
    ]);
    let signingState: SigningMonitorDetail | null = null;
    if (approvalState?.signingStarted && user.canManageAll) {
      try { signingState = await internalApi<SigningMonitorDetail>(`/contract-signing-monitor/${record.id}`); }
      catch { signingState = null; }
    }
    return { reviewRows, approvalState, signingState, versionRows, preSendState };
  }, [record.id, user.canManageAll]);
  const load = useCallback(async () => {
    try {
      const state = await fetchWorkflow();
      setReviews(state.reviewRows); setApproval(state.approvalState); setSigningProgress(state.signingState); setVersions(state.versionRows); setPreSend(state.preSendState);
    } catch (e) { setError((e as Error).message); }
  }, [fetchWorkflow]);
  useEffect(() => {
    let active = true;
    void fetchWorkflow().then(state => {
      if (!active) return;
      setReviews(state.reviewRows); setApproval(state.approvalState); setSigningProgress(state.signingState); setVersions(state.versionRows); setPreSend(state.preSendState); setError("");
    }).catch(e => { if (active) setError((e as Error).message); });
    return () => { active = false; };
  }, [fetchWorkflow, record.rowVersion]);
  const latest = reviews[0];
  const canSendToCustomer = record.status === 0 || record.status === 1 || (record.status === 2 && !approval);
  const canCreateVersion = (record.canEdit && !["SENT", "VIEWED"].includes(latest?.status ?? ""))
    || (record.status === 1 && record.locked && (latest?.status === "CHANGES_REQUESTED" || (latest?.status === "REJECTED" && latest.followupDecision === "NEGOTIATE")));
  const awaitingDecision = record.status === 7 && latest?.status === "REJECTED" && !latest.followupDecision;
  // Flow: Sale trình → Hành chính duyệt → Sale gửi khách → khách đồng ý → Hành chính duyệt bản chốt → Sếp ký (Sếp does not approve).
  const awaitingInternalApproval = record.status === 2 && approval?.status === 0;
  const canDecideFinal = awaitingInternalApproval && user.canCheck;
  const preSendStage = preSend?.stage ?? "DRAFT";
  const officeApproved = preSendStage === "APPROVED" || preSendStage.startsWith("CUSTOMER_");
  /** "Gửi khách xem" appears only once Hành chính has approved this version; before that a status line explains why. */
  const sendWaiting = canSendToCustomer && !officeApproved
    ? preSendStage === "OFFICE" ? "Đang chờ Hành chính duyệt. Hành chính duyệt xong mới gửi khách được."
      : preSendStage === "RETURNED" ? "Hành chính đã trả lại để chỉnh sửa. Sửa theo nhận xét rồi Trình Hành chính lại."
      : "Chưa trình Hành chính. Bấm Trình Hành chính ở khung Duyệt nội bộ; Hành chính duyệt xong mới gửi khách được."
    : null;
  /** A business signs the printed contract and stamps its red seal; a person signs electronically. */
  const business = record.content.partyType === "BUSINESS";
  const canStartSigning = !business && user.canApprove && record.status === 3 && approval?.status === 1 && !approval.signingStarted;
  const canPrint = business && record.status === 3 && approval?.status === 1;
  const customerApproved = latest?.status === "APPROVED";
  const signingComplete = record.status === 4 || record.status === 5;
  /** A prospect has no portal account: they read, comment and sign through the emailed link; the account comes after both sides signed. */
  const prospect = !record.customerHasAccount;
  const linkUsable = prospect && !!latest && (["SENT", "VIEWED"].includes(latest.status) || (latest.status === "APPROVED" && !signingComplete));
  const companySigned = signingProgress?.signers.some(signer => signer.party === "Công ty" && signer.status === 1) ?? false;
  const customerSigned = signingProgress?.signers.some(signer => signer.party === "Khách hàng" && signer.status === 1) ?? signingComplete;
  const workflowSteps = [
    { title: "Hoàn thiện bản nháp", detail: record.status === 0 ? (record.sendBlockedReason ?? "Điền đủ nội dung, thông tin khách hàng và điều khoản.") : "Phiên bản hợp đồng đã được lưu.", done: record.status !== 0, current: record.status === 0 },
    { title: "Hành chính duyệt trước khi gửi", detail: officeApproved ? "Hành chính đã duyệt, Sale gửi khách được." : preSendStage === "OFFICE" ? "Đang chờ Hành chính duyệt." : preSendStage === "RETURNED" ? "Hành chính trả lại, Sale chỉnh sửa rồi trình lại." : "Sale bấm Trình Hành chính ở khung Duyệt nội bộ.", done: officeApproved, current: preSendStage === "OFFICE" || preSendStage === "RETURNED" },
    { title: "Khách hàng duyệt nội dung", detail: customerApproved ? "Khách hàng đã đồng ý phiên bản này." : latest && ["SENT", "VIEWED"].includes(latest.status) ? "Đang chờ khách xem và phản hồi." : "Gửi phiên bản đã hoàn thiện để khách xác nhận nội dung.", done: customerApproved, current: !customerApproved && record.status === 1 },
    { title: "Hành chính duyệt bản chốt", detail: approval?.status === 1 ? "Hành chính đã duyệt bản khách đồng ý." : awaitingInternalApproval ? "Đang chờ Hành chính duyệt bản khách đã đồng ý." : "Chỉ mở sau khi khách hàng đồng ý.", done: approval?.status === 1, current: awaitingInternalApproval },
    ...business ? [
      { title: "In, ký và đóng dấu đỏ", detail: signingComplete ? "Hai bên đã ký giấy." : approval?.status === 1 ? "In hợp đồng; đại diện hai bên ký, doanh nghiệp đóng dấu đỏ." : "Sau khi Hành chính duyệt bản chốt, in hợp đồng để Sếp ký và khách đóng dấu.", done: signingComplete, current: record.status === 3 },
      { title: "Lưu bản ký giấy", detail: signingComplete ? "Bản scan đã được xác nhận, hồ sơ đã ký." : "Scan bản đã đóng dấu, tải lên màn Bản ký giấy và xác nhận.", done: signingComplete, current: false },
    ] : [{ title: "Sếp ký cho công ty", detail: companySigned ? "Sếp đã ký." : approval?.signingStarted ? "Đang chờ Sếp ký trong hàng đợi Ký phía công ty." : "Sau khi Hành chính duyệt bản chốt, Sếp tạo phiên ký và ký.", done: companySigned, current: record.status === 3 && !companySigned },
    { title: "Khách hàng ký", detail: customerSigned ? "Khách hàng đã ký, hồ sơ hoàn tất." : companySigned ? (prospect ? "Công ty đã ký. Khách mở link trong email, xác minh OTP rồi ký." : "Công ty đã ký. Khách đăng nhập Portal, mở hợp đồng và xác minh OTP để ký.") : prospect ? "Sau khi công ty ký, khách ký ngay trên link đã nhận qua email." : "Sau khi công ty ký, khách đăng nhập Portal để tiếp tục ký.", done: customerSigned, current: companySigned && !customerSigned }],
  ];

  function ask(c: Confirm) { setConfirmed(false); setConfirm(c); setNote(""); setExpiry(inSevenDays()); setError(""); setNotice(""); }
  async function run() {
    if (!confirm) return; setBusy(true); setError("");
    try { await confirm.run(note.trim(), confirm.expiry ? new Date(expiry).toISOString() : ""); setConfirm(null); await load(); onChanged(); }
    catch (e) { setError((e as Error).message); setConfirm(null); if(e instanceof InternalApiError && e.issues.length) focusIssue(e.issues[0].path); } finally { setBusy(false); }
  }
  async function act<T>(action: () => Promise<T>, done?: string | ((result: T) => string)) {
    setBusy(true); setError(""); setNotice("");
    try { const result = await action(); if (done) setNotice(typeof done === "string" ? done : done(result)); await load(); } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  /** Creates the prospect's portal account, or reissues the temporary password; both go to the customer by email. */
  async function issueCustomerPassword(create: boolean) {
    setBusy(true); setError(""); setNotice("");
    try {
      const result = await internalApi<{ emailSent: boolean; emailMasked: string | null; developmentPhone: string | null; developmentPassword: string | null }>(
        `/customers/${record.content.customerId}/${create ? "account" : "reset-temp-password"}`, "POST");
      if (result.emailSent) setNotice(`${create ? "Đã tạo tài khoản và gửi" : "Đã gửi"} email đăng nhập lần đầu tới ${result.emailMasked}: tên đăng nhập là SĐT của khách, kèm mật khẩu tạm.`);
      else setError(`${create ? "Đã tạo tài khoản nhưng chưa" : "Chưa"} gửi được email tới ${result.emailMasked}. Kiểm tra cấu hình SMTP rồi bấm “Cấp lại mật khẩu tạm”.`
        + (result.developmentPhone && result.developmentPassword ? ` (Development) Đăng nhập tại ${customerPortalLoginUrl} bằng SĐT ${result.developmentPhone}, mật khẩu tạm ${result.developmentPassword}.` : ""));
      onChanged();
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  async function refreshWorkflow() {
    setBusy(true); setError("");
    try { await load(); onChanged(); }
    finally { setBusy(false); }
  }

  const mailed = (r: ReviewSummary) => { if (r.linkEmailSent) setNotice(prospect ? "Đã gửi email có link xem hợp đồng cho khách." : "Đã gửi email mời khách đăng nhập Cổng khách hàng để xem."); else setError(prospect ? "Chưa gửi được email cho khách. Kiểm tra email trong hợp đồng hoặc cấu hình SMTP rồi bấm Gửi lại link." : "Đã gửi hợp đồng lên Cổng khách hàng nhưng chưa gửi được email báo khách. Kiểm tra email hoặc cấu hình SMTP."); };
  const send = () => ask({ title: "Gửi khách xem", action: "Gửi khách xem",
    description: prospect
      ? `Gửi phiên bản ${record.versionNo} cho khách tiềm năng ${record.customerName} (${record.customerPhoneMasked}). Phiên bản sẽ bị khóa; khách không cần tài khoản: nhận email có link, xác minh số điện thoại bằng mã OTP rồi xem, góp ý và ký ngay trên link.`
      : `Gửi phiên bản ${record.versionNo} cho ${record.customerName} (${record.customerPhoneMasked}). Phiên bản sẽ bị khóa; khách đã có tài khoản nên nhận email mời đăng nhập Cổng khách hàng để xem và phản hồi.`,
    note: { label: "Lời nhắn cho khách (tùy chọn)", max: 1000 }, expiry: prospect,
    run: (n, linkExpiresAt) => internalApi<ReviewSummary>(`/contract-drafts/${record.id}/send-review`, "POST", { message: n || null, rowVersion: record.rowVersion, versionRowVersion: record.versionRowVersion, contentConfirmed: true, linkExpiresAt: linkExpiresAt || null }).then(mailed) });
  const resendLink = () => latest && ask({ title: "Gửi lại link", action: "Gửi lại link", description: "Gửi link mới qua email cho khách. Link cũ sẽ không dùng được nữa.", expiry: true,
    run: (_, linkExpiresAt) => internalApi<ReviewSummary>(`/contract-reviews/${latest.id}/resend-link`, "POST", { linkExpiresAt, rowVersion: latest.rowVersion }).then(mailed) });
  const newVersion = () => ask({ title: "Tạo phiên bản mới", action: "Tạo phiên bản", description: `Tạo phiên bản ${Number(record.versionNo) + 1} từ nội dung hiện tại. Phiên bản ${record.versionNo} vẫn được giữ trong lịch sử.`,
    note: { label: "Ghi chú thay đổi (tùy chọn)", max: 1000 },
    run: n => internalApi(`/contract-drafts/${record.id}/versions`, "POST", { rowVersion: record.rowVersion, changeNote: n || null }) });
  const followup = (decision: "NEGOTIATE" | "CLOSE") => latest && ask({ title: decision === "NEGOTIATE" ? "Thương lượng tiếp" : "Đóng draft", action: decision === "NEGOTIATE" ? "Thương lượng tiếp" : "Đóng draft",
    description: decision === "NEGOTIATE" ? "Hồ sơ quay lại trạng thái thương lượng; bạn tạo phiên bản mới để sửa và gửi lại." : "Hồ sơ sẽ bị đóng và không thể gửi lại.",
    note: { label: "Ghi chú (tùy chọn)", max: 2000 },
    run: n => internalApi(`/contract-reviews/${latest.id}/followup`, "POST", { decision, note: n || null, rowVersion: latest.rowVersion }) });

  const decideApproval = (approve: boolean) => ask({
    title: approve ? "Hành chính duyệt bản chốt" : "Từ chối bản chốt",
    action: approve ? "Duyệt, chuyển Sếp ký" : "Từ chối bản chốt",
    description: approve
      ? `Xác nhận đã kiểm tra bản khách đồng ý của hợp đồng ${record.code}, phiên bản ${record.versionNo}. Hồ sơ chuyển sang chờ Sếp ký.`
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
  async function printContract() {
    setError(""); const tab = window.open("", "_blank"); // opened before the await so the popup is not blocked
    try {
      const response = await fetch(`/api/internal/contract-drafts/${encodeURIComponent(record.id)}/print-pdf`, { credentials: "same-origin", cache: "no-store" });
      if (!response.ok) throw new Error(((await response.json().catch(() => null)) as { message?: string } | null)?.message ?? "Không tải được PDF hợp đồng.");
      const url = URL.createObjectURL(await response.blob());
      if (tab) tab.location.href = url; else window.location.href = url;
    } catch (e) { tab?.close(); setError((e as Error).message); }
  }
  return <section className={ui.card} style={{ margin: "0 0 16px" }} aria-label="Gửi khách xem">
    <div className={s.workflowGuide} aria-label="Các bước ký hợp đồng">
      <div className={s.workflowGuideHeader}><div><strong>Quy trình ký hợp đồng</strong><p>{business ? "Khách hàng doanh nghiệp: Sale trình → Hành chính duyệt → Sale gửi khách → khách đồng ý → Hành chính duyệt bản chốt → in hợp đồng, Sếp ký và doanh nghiệp đóng dấu đỏ → tải bản scan lên." : "Khách hàng cá nhân: Sale trình → Hành chính duyệt → Sale gửi khách → khách đồng ý → Hành chính duyệt bản chốt → Sếp ký → khách hàng ký."}</p></div>{approval?.signingStarted && !signingComplete && <Link className={ui.secondaryButton} href="/workspace/contracts/company-signing">Mở hàng đợi ký công ty</Link>}</div>
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
        {user.canWrite && canSendToCustomer && officeApproved && <button type="button" className={ui.primaryButton} disabled={busy || dirty || sendIssues.length > 0 || !!record.sendBlockedReason}
          aria-describedby="review-send-guidance" title={dirty ? "Lưu nháp trước khi gửi" : record.sendBlockedReason || undefined} onClick={send}><Send size={16}/>Gửi khách xem</button>}
        {(user.canWrite || user.canCheck) && canCreateVersion && <button className={latest?.status === "CHANGES_REQUESTED" ? ui.primaryButton : ui.secondaryButton} disabled={busy} onClick={newVersion}><CopyPlus size={16}/>Tạo phiên bản mới để chỉnh sửa</button>}
        {user.canWrite && awaitingDecision && <><button className={ui.secondaryButton} disabled={busy} onClick={() => followup("NEGOTIATE")}><Handshake size={16}/>Thương lượng tiếp</button>
          <button className={ui.secondaryButton} disabled={busy} onClick={() => followup("CLOSE")}><XCircle size={16}/>Đóng draft</button></>}
        {user.canWrite && linkUsable && <button className={ui.secondaryButton} disabled={busy} title="Dùng khi khách mất email, link hết hạn hoặc email chưa gửi được" onClick={resendLink}><Mail size={16}/>Gửi lại link</button>}
        {user.canWrite && linkUsable && latest && <button className={ui.secondaryButton} disabled={busy} title="Dùng khi khách không kịp nhập mã, nhập sai quá nhiều lần hoặc bị khóa đăng nhập"
          onClick={() => void act(() => internalApi(`/contract-reviews/${latest.id}/resend-otp`, "POST"), `Đã gửi mã OTP mới tới ${record.customerPhoneMasked} và mở khóa cho khách. Khách mở link trong email, chọn “Đã có mã” và nhập mã (hiệu lực 5 phút).`)}><KeyRound size={16}/>Gửi lại mã OTP</button>}
        {canDecideFinal && <>
          <button className={ui.primaryButton} disabled={busy || dirty} onClick={() => decideApproval(true)}><Check size={16}/>Duyệt, chuyển Sếp ký</button>
          <button className={ui.secondaryButton} disabled={busy || dirty} onClick={() => decideApproval(false)}><XCircle size={16}/>Từ chối</button>
        </>}
        {canStartSigning && <button className={ui.primaryButton} disabled={busy || dirty || signerName.trim().length < 2 || !signerPhone.trim()} onClick={() => void startSigning()}>
          <Send size={16}/>Tạo phiên ký
        </button>}
        {canPrint && <><button className={ui.primaryButton} disabled={busy} onClick={() => void printContract()}><Printer size={16}/>In hợp đồng (PDF)</button>
          <Link className={ui.secondaryButton} href={`/workspace/signed-contracts/${encodeURIComponent(record.id)}`}><Upload size={16}/>Tải bản ký giấy</Link></>}
        {(user.canWrite || user.canCheck) && prospect && (signingComplete || latest?.status === "APPROVED") && <button className={ui.primaryButton} disabled={busy}
          title="Gửi email cho khách: tên đăng nhập là SĐT, kèm mật khẩu tạm; khách đổi mật khẩu ở lần đăng nhập đầu"
          onClick={() => void issueCustomerPassword(true)}><UserPlus size={16}/>Tạo tài khoản khách</button>}
        {(user.canWrite || user.canCheck) && !prospect && latest && <button className={ui.secondaryButton} disabled={busy} title="Chỉ dùng khi khách chưa đổi mật khẩu tạm và mất email"
          onClick={() => void issueCustomerPassword(false)}><KeyRound size={16}/>Cấp lại mật khẩu tạm</button>}
      </div>
    </div>
    {user.canWrite && canSendToCustomer && <div id="review-send-guidance" className={s.muted} style={{ marginTop: 8 }} aria-live="polite">
      {sendWaiting ? <p role="status" className={ui.notice}>{sendWaiting}</p> : dirty ? <p>Lưu nháp để cập nhật thông tin và kiểm tra điều kiện gửi khách.</p> : record.sendBlockedReason || sendIssues.length ? <>
        <p>Chưa thể gửi khách: {record.sendBlockedReason || "Cần hoàn thiện thông tin hợp đồng."}</p>
        {sendIssues.map((issue,i) => <button key={i} type="button" className={ui.secondaryButton} onClick={() => onLocate(issue.path)}>{issue.message}</button>)}
      </> : <p>Nháp đã đủ thông tin. Bạn có thể gửi phiên bản này cho khách xem và phản hồi.</p>}
    </div>}
    {latest?.status === "CHANGES_REQUESTED" && record.locked && (user.canWrite || user.canCheck) && <p role="status" className={ui.notice} style={{ marginTop: 8 }}>Khách đã góp ý và yêu cầu sửa. Sale hoặc Hành chính bấm <strong>Tạo phiên bản mới để chỉnh sửa</strong>, sửa nội dung theo các góp ý bên dưới, rồi Trình Hành chính và gửi lại khách.</p>}
    {(record.issues ?? []).some(i => i.path.startsWith("milestones")) && <button type="button" className={ui.secondaryButton} onClick={() => focusIssue("milestones")}>Đi tới lịch thanh toán</button>}
    {(record.carriedComments?.length ?? 0) > 0 && <section className={ui.notice}><strong>Góp ý chuyển tiếp sang phiên bản này</strong>{record.carriedComments?.map(c => <div key={c.id}><p>{c.content} · chuyển từ bản trước · {c.resolved ? "Đã xử lý" : "Chưa xử lý"}</p>{!c.resolved && <button type="button" disabled={busy} className={ui.secondaryButton} onClick={() => void act(async () => { await internalApi(`/contract-comments/${c.id}/resolve`,"POST"); onChanged(); })}>Đánh dấu đã xử lý</button>}</div>)}</section>}
    {latest && <div className={s.customerPortalAccess}>
      {prospect
        ? <div><strong>Cách khách mở hợp đồng</strong><p>Khách tiềm năng chưa có tài khoản: khách mở link riêng trong email, xác minh số điện thoại bằng mã OTP rồi xem, góp ý, đồng ý và ký ngay trên link đó. Link chỉ mở được hợp đồng này.</p><p className={s.muted}>Khi khách đồng ý lần đầu, hệ thống tự cấp tài khoản và gửi hướng dẫn đăng nhập qua email.</p></div>
        : <div><strong>Cách khách mở hợp đồng</strong><p>Khách đã có tài khoản: khách đăng nhập Cổng khách hàng bằng số điện thoại và mật khẩu, rồi chọn hợp đồng trong mục “Hợp đồng của bạn”. Khách nhận email báo có hợp đồng mới.</p></div>}
      {!prospect && <a href={customerPortalLoginUrl} target="_blank" rel="noreferrer">Mở Portal khách hàng</a>}
    </div>}
    {record.canEdit && <p className={s.muted} style={{ marginTop: 8 }}>Khi bạn lưu nội dung đã thay đổi, hệ thống tự tạo phiên bản kế tiếp và giữ phiên bản trước trong lịch sử.</p>}
    {error && <p role="alert" className={ui.error} style={{ marginTop: 10 }}>{error}</p>}
    {notice && <p role="status" className={ui.notice} style={{ marginTop: 10 }}>{notice}</p>}
    {approval && <div className={ui.notice} style={{ marginTop: 10 }}>
      <strong>{approval.workflowName}:</strong>{" "}
      {approval.status === 0 ? "Đang chờ Hành chính duyệt bản khách đã đồng ý" : approval.status === 1 ? "Hành chính đã duyệt, hồ sơ chờ Sếp ký" : "Đã từ chối phê duyệt"}
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
        {r.linkExpiresAt && <p className={s.muted}>Link hết hạn: {dateTime(r.linkExpiresAt)}</p>}
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
              await internalApi(`/contract-reviews/${replyTo.review}/comments`, "POST", { termKey: c.termKey, section: c.section ?? null, parentCommentId: c.id, content: reply.trim() }); setReplyTo(null); })}>Gửi trả lời</button>
              <button className={ui.secondaryButton} onClick={() => setReplyTo(null)}>Hủy</button></div>
          </div>}
        </div>)}
      </div>)}
    </details>}

    <Dialog open={!!confirm} onOpenChange={v => { if (!busy && !v) setConfirm(null); }}><DialogContent className="sm:max-w-4xl max-h-[90vh] overflow-y-auto">
      <DialogHeader><DialogTitle>{confirm?.title}</DialogTitle><DialogDescription>{confirm?.description}</DialogDescription></DialogHeader>
      {confirm?.action === "Gửi khách xem" && <><DraftPreview content={record.content} code={record.code}/><label className="flex gap-2 text-sm"><input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)}/>Tôi đã kiểm tra bên giao dịch, số tiền, lịch thanh toán và các nội dung viết cố định trong mẫu.</label></>}{confirm?.expiry && <label className="grid gap-2 text-sm font-medium">Link hết hạn lúc *<input required type="datetime-local" className={ui.field} value={expiry} disabled={busy} onChange={e => setExpiry(e.target.value)}/></label>}{confirm?.note && <label className="grid gap-2 text-sm font-medium">{confirm.note.label}<Textarea maxLength={confirm.note.max} value={note} disabled={busy} onChange={e => setNote(e.target.value)}/></label>}
      <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={() => setConfirm(null)}>Hủy</Button>
        <Button type="button" disabled={busy || (!!confirm?.expiry && !expiry) || (confirm?.action === "Gửi khách xem" && !confirmed) || (!!confirm?.note?.required && !note.trim())} onClick={() => void run()}>{busy ? "Đang xử lý…" : confirm?.action}</Button></DialogFooter>
    </DialogContent></Dialog>
  </section>;
}
