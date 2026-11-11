import { format } from "date-fns";
import { parseApiDate } from "@/lib/format-date-time";
import type {
  ContractStatusKey,
  PersonRef,
  ScanKeywordJobStatus,
  ScanKeywordType,
  SignedEvidenceStatus,
} from "@/services/scans/signed-scans";

export type StatusTone = "success" | "pending" | "info" | "neutral" | "danger";

interface StatusLabel {
  label: string;
  tone: StatusTone;
}

export const KEYWORD_TYPES: ScanKeywordType[] = ["ContractCode", "TaxCode", "PhoneNumber", "CustomerName"];

export const KEYWORD_TYPE_LABELS: Record<ScanKeywordType, string> = {
  ContractCode: "Số hợp đồng",
  TaxCode: "Mã số thuế",
  PhoneNumber: "Số điện thoại",
  CustomerName: "Tên khách hàng",
};

const CONTRACT_STATUS: Record<ContractStatusKey, string> = {
  Draft: "Nháp",
  Negotiating: "Đang đàm phán",
  PendingApproval: "Chờ duyệt",
  PendingSignature: "Chờ ký",
  Signed: "Đã ký",
  Completed: "Hoàn thành",
  Cancelled: "Đã hủy",
  Failed: "Thất bại",
};

/** A status this version does not know yet still shows, as its raw key. */
export const contractStatusLabel = (status: string) => CONTRACT_STATUS[status as ContractStatusKey] ?? status;

export const EVIDENCE_STATUS_LABELS: Record<SignedEvidenceStatus, string> = {
  PendingReview: "Chờ kiểm tra",
  Accepted: "Hợp lệ",
  Rejected: "Không hợp lệ",
  Superseded: "Đã bị thay",
};

export const KEYWORD_JOB_STATUS: Record<ScanKeywordJobStatus, StatusLabel> = {
  Pending: { label: "Chờ đọc", tone: "info" },
  Processing: { label: "Đang đọc", tone: "info" },
  Done: { label: "Đã đọc xong", tone: "success" },
  Failed: { label: "Lỗi", tone: "danger" },
};

const KEYWORD_ERRORS: Record<string, string> = {
  OcrEngineUnavailable: "Máy chủ OCR không phản hồi",
  OcrEngineFailed: "Máy chủ OCR không đọc được bản quét",
  OcrTimeout: "Đọc quá thời gian cho phép",
  OcrUnsupportedFile: "Tệp không phải PDF, JPG hoặc PNG",
  OcrPdfUnreadable: "Không mở được tệp PDF (tệp hỏng hoặc đặt mật khẩu)",
  ScanFileMissing: "Không tìm thấy tệp của bản quét",
  ScanFileLocked: "Tệp đã mã hóa, cần key để đọc lại",
};

export const keywordErrorMessage = (code: string | null) =>
  (code && KEYWORD_ERRORS[code]) || "Không đọc được từ khóa";

/** 0312345678001 → 0312345678-001; 0901234567 → 0901 234 567; 02439998888 → 024 3999 8888. */
export const formatKeywordValue = (type: ScanKeywordType, value: string) => {
  if (type === "TaxCode" && /^\d{13}$/.test(value)) {
    return `${value.slice(0, 10)}-${value.slice(10)}`;
  }
  if (type === "PhoneNumber") {
    if (/^02\d{9}$/.test(value)) {
      return `${value.slice(0, 3)} ${value.slice(3, 7)} ${value.slice(7)}`;
    }
    if (/^0\d{9}$/.test(value)) {
      return `${value.slice(0, 4)} ${value.slice(4, 7)} ${value.slice(7)}`;
    }
  }
  return value;
};

export const formatFileSize = (size: number | string) => {
  const bytes = Number(size);
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`;
  return `${(bytes / (1024 * 1024)).toLocaleString("vi-VN", { maximumFractionDigits: 1 })} MB`;
};

/** yyyy-MM-dd from the API → dd/MM/yyyy, without a time-zone shift. */
export const formatSignDate = (value: string) => {
  const [year, month, day] = value.split("-");
  return year && month && day ? `${day}/${month}/${year}` : value;
};

export const formatMoney = (amount: number) => `${new Intl.NumberFormat("vi-VN").format(amount)} ₫`;

export const formatDateTime = (value: string) => format(parseApiDate(value), "HH:mm, dd/MM/yyyy");

export const personLabel = (person: PersonRef) =>
  person.roleName ? `${person.displayName} (${person.roleName})` : person.displayName;
