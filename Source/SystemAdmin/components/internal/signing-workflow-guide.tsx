import Link from "next/link";
import { Check } from "lucide-react";
import type { DraftRecord, InternalApprovalState, SigningMonitorDetail } from "@/services/internal-api";
import { internalStyles as ui } from "./internal-theme";
import s from "./workspace.module.css";

export default function SigningWorkflowGuide({ record, direct, business, approval, preSendStage = "DRAFT", reviewStatus, signingProgress, sendReady = false, draftIssue }: {
  record?: DraftRecord;
  sendReady?: boolean;
  draftIssue?: string | null;
  direct: boolean;
  business: boolean;
  approval?: InternalApprovalState | null;
  preSendStage?: string;
  reviewStatus?: string;
  signingProgress?: SigningMonitorDetail | null;
}) {
  const status = record?.status ?? 0;
  const customerWaiting = reviewStatus === "SENT" || reviewStatus === "VIEWED";
  const awaitingInternalApproval = status === 2 && approval?.status === 0;
  const officeApproved = direct || preSendStage === "APPROVED" || preSendStage.startsWith("CUSTOMER_");
  const customerApproved = reviewStatus === "APPROVED";
  const signingComplete = status === 4 || status === 5;
  const paperWaiting = business && status === 3;
  const paperPending = paperWaiting && !!record?.paperEvidencePending;
  const prospect = !record?.customerHasAccount;
  const companySigned = signingComplete || !!signingProgress?.signers.some(signer => signer.party === "Công ty" && signer.status === 1);
  const customerSigned = signingComplete || !!signingProgress?.signers.some(signer => signer.party === "Khách hàng" && signer.status === 1);

  const workflowSteps = [
    { title: "Hoàn thiện bản nháp",
      detail: !record ? "Nhập thông tin rồi bấm Lưu nháp."
        : draftIssue && !customerWaiting && status < 3 ? draftIssue
        : direct && status === 0 && !sendReady ? record.sendBlockedReason ?? "Hoàn thiện thông tin để gửi khách."
        : "Phiên bản hợp đồng đã được lưu.",
      done: !!record && (status >= 3 || customerWaiting || (!draftIssue && (!direct || status !== 0 || sendReady))),
      current: !record || (!customerWaiting && !!draftIssue && status < 3) || (direct && status === 0 && !sendReady) },
    ...(!direct ? [{ title: "Hành chính duyệt trước khi gửi",
      detail: officeApproved ? "Hành chính đã duyệt phiên bản này." : preSendStage === "OFFICE" ? "Đang chờ Hành chính duyệt." : preSendStage === "RETURNED" ? "Hành chính trả lại, Sale chỉnh sửa rồi trình lại." : "Sale bấm Trình Hành chính ở khung Duyệt nội bộ.",
      done: officeApproved, current: !!record && !draftIssue && !officeApproved }] : []),
    { title: customerWaiting || customerApproved ? "Khách hàng duyệt nội dung" : "Gửi khách xem",
      detail: customerApproved ? "Khách hàng đã đồng ý phiên bản này." : customerWaiting ? "Đang chờ khách xem và phản hồi."
        : sendReady ? "Bấm Gửi khách xem để gửi phiên bản này." : "Gửi phiên bản đã hoàn thiện để khách xác nhận nội dung.",
      done: customerApproved, current: customerWaiting || sendReady },
    ...(!direct || awaitingInternalApproval ? [{ title: "Hành chính duyệt bản chốt", detail: approval?.status === 1 ? "Hành chính đã duyệt bản khách đồng ý." : awaitingInternalApproval ? "Đang chờ Hành chính duyệt bản khách đã đồng ý." : "Chỉ mở sau khi khách hàng đồng ý.", done: approval?.status === 1, current: awaitingInternalApproval }] : []),
    ...business ? [
      { title: "In, ký và đóng dấu đỏ", detail: signingComplete ? "Hai bên đã ký giấy." : paperPending ? "Đã nộp bản scan để Hành chính kiểm tra." : approval?.status === 1 ? "In hợp đồng; đại diện hai bên ký, doanh nghiệp đóng dấu đỏ." : direct ? "Sau khi khách đồng ý, in hợp đồng để hai bên ký và đóng dấu." : "Sau khi Hành chính duyệt bản chốt, in hợp đồng để Sếp ký và khách đóng dấu.", done: signingComplete || paperPending, current: paperWaiting && !paperPending },
      { title: "Lưu bản ký giấy", detail: signingComplete ? "Bản scan đã được xác nhận, hồ sơ đã ký." : paperPending ? "Đang chờ Hành chính xác nhận bản ký giấy." : "Scan bản đã đóng dấu, tải lên màn Bản ký giấy và xác nhận.", done: signingComplete, current: paperPending },
    ] : [{ title: "Sếp ký cho công ty", detail: companySigned ? "Sếp đã ký." : approval?.signingStarted ? "Đang chờ Sếp ký trong hàng đợi Ký phía công ty." : direct ? "Sau khi khách đồng ý, Sếp tạo phiên ký và ký." : "Sau khi Hành chính duyệt bản chốt, Sếp tạo phiên ký và ký.", done: companySigned, current: status === 3 && !companySigned },
    { title: "Khách hàng ký", detail: customerSigned ? "Khách hàng đã ký, hồ sơ hoàn tất." : companySigned ? (prospect ? "Công ty đã ký. Khách mở link trong email, xác minh OTP rồi ký." : "Công ty đã ký. Khách đăng nhập Portal, mở hợp đồng và xác minh OTP để ký.") : prospect ? "Sau khi công ty ký, khách ký ngay trên link đã nhận qua email." : "Sau khi công ty ký, khách đăng nhập Portal để tiếp tục ký.", done: customerSigned, current: companySigned && !customerSigned }],
  ];

  return (
    <section className={s.workflowGuide} aria-label="Quy trình ký hợp đồng">
      <div className={s.workflowGuideHeader}><div><strong>Quy trình ký hợp đồng</strong><p>{direct
        ? (business ? "Hành chính gửi khách → khách đồng ý → hai bên ký giấy và đóng dấu → Hành chính xác nhận bản scan." : "Hành chính gửi khách → khách đồng ý → Sếp ký → khách hàng ký.")
        : (business ? "Sale trình → Hành chính duyệt → Sale gửi khách → khách đồng ý → Hành chính duyệt bản chốt → hai bên ký giấy và đóng dấu → Hành chính xác nhận bản scan." : "Sale trình → Hành chính duyệt → Sale gửi khách → khách đồng ý → Hành chính duyệt bản chốt → Sếp ký → khách hàng ký.")}</p></div>{!business && approval?.signingStarted && !signingComplete && <Link className={ui.secondaryButton} href="/workspace/contracts/company-signing">Mở hàng đợi ký công ty</Link>}</div>
      <ol className={s.workflowSteps}>{workflowSteps.map((step, index) => <li key={step.title} className={`${s.workflowStep} ${step.done ? s.workflowStepDone : step.current ? s.workflowStepCurrent : ""}`}>
        <span className={s.workflowStepNumber}>{step.done ? <Check size={14}/> : index + 1}</span><div><strong>{step.title}</strong><p>{step.detail}</p></div>
      </li>)}</ol>
    </section>
  );
}
