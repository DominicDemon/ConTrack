export type InternalUser = { id: string; name: string; username: string; permissions: string[]; canManageAll: boolean; canWrite: boolean };
export type DraftField = { key: string; label: string; required: boolean };
export type DraftTerm = { key: string; title: string; content: string; editable: boolean };
export type TemplateDefinition = { id: string; name: string; revision: string; isDemo: boolean; isImported?: boolean; fields: DraftField[]; terms: DraftTerm[] };
export type CustomerOption = { id: string; name: string; phone: string; fields: Record<string, string> };
export type CustomerInput = { name: string; phone: string; email: string; company: string; taxCode: string; address: string };
export type ProductOption = { id: string; name: string; price: number };
export type EmployeeOption = { id: string; name: string };
export type DraftItem = { productId: string | null; name: string; quantity: number; unitPrice: number; discountPercent: number; discountAmount: number };
export type DraftTotals = { subtotal: number; discount: number; vat: number; total: number };
export type DraftContent = { templateId: string; customerId: string; name: string; fields: Record<string, string>; items: DraftItem[]; terms: DraftTerm[]; vatPercent: number; effectiveDate: string | null; expireDate: string | null; templateSnapshot: TemplateDefinition | null; totals: DraftTotals; missingFields: string[] };
export type DraftRecord = { id: string; code: string; employeeId: string; employeeName: string; rowVersion: string; versionRowVersion: string; updatedAt: string; content: DraftContent; status: number; versionNo: string; locked: boolean; canEdit: boolean; customerName: string; customerPhoneMasked: string; latestReviewStatus: string | null; sendBlockedReason: string | null };
export type DraftSummary = { id: string; code: string; name: string; customerName: string; employeeId: string; employeeName: string; updatedAt: string; missingCount: number; status: number; latestReviewStatus: string | null };
export type ContractArchiveEntry = { id: string; code: string; name: string; customerName: string; employeeId: string; employeeName: string; updatedAt: string; status: number; versionNo: string; versionLocked: boolean; signatureCount: number; lastSignedAt: string | null };
export type SigningMonitorEntry = { id: string; code: string; name: string; customerName: string; employeeId: string; employeeName: string; updatedAt: string; status: number; versionNo: string; signatureCount: number; signedCount: number; companySigned: boolean; customerSigned: boolean; latestReviewStatus: string | null; lastSignedAt: string | null };
export type SigningTimelineEvent = { at: string; category: string; title: string; detail: string | null; state: string };
export type SigningSignerProgress = { id: string; versionNo: string; party: string; role: string; name: string; title: string | null; email: string | null; status: number; signedAt: string | null; documentHash: string | null; rejectReason: string | null };
export type SigningArtifactInfo = { fileName: string; contentType: string; size: string; sha256: string; role: number; primary: boolean; addedAt: string };
export type SigningMonitorDetail = { id: string; code: string; name: string; customerName: string; employeeName: string; createdAt: string; updatedAt: string; status: number; versionNo: string; versionLocked: boolean; snapshotHash: string | null; timeline: SigningTimelineEvent[]; signers: SigningSignerProgress[]; artifacts: SigningArtifactInfo[] };
export class InternalApiError extends Error { constructor(public status: number, message: string) { super(message); } }
let csrfToken: string | undefined;
type ApiErrorBody = { message?: string; code?: string };
function readApiError(body: string, fallback: string): ApiErrorBody {
  if (!body.trim()) return { message: fallback };
  try {
    const value: unknown = JSON.parse(body);
    if (typeof value === "object" && value !== null && "message" in value && typeof value.message === "string")
      return { message: value.message, ...( "code" in value && typeof value.code === "string" ? { code: value.code } : {}) };
  } catch { /* The caller reports a safe fallback for non-JSON error bodies. */ }
  return { message: fallback };
}
async function refreshCsrfToken(): Promise<void> {
  const response = await fetch("/api/internal/auth/csrf", { credentials: "same-origin", cache: "no-store" });
  const body = await response.text();
  if (!response.ok) {
    const error = readApiError(body, "Không thể khởi tạo phiên bảo vệ. Vui lòng tải lại trang.");
    throw new InternalApiError(response.status, error.message ?? "Không thể khởi tạo phiên bảo vệ. Vui lòng tải lại trang.");
  }
  let parsed: unknown = null;
  try { parsed = body ? JSON.parse(body) : null; }
  catch { throw new InternalApiError(response.status, "Máy chủ trả mã bảo vệ không hợp lệ. Vui lòng tải lại trang."); }
  if (typeof parsed !== "object" || parsed === null || !("token" in parsed) || typeof parsed.token !== "string" || !parsed.token)
    throw new InternalApiError(response.status, "Máy chủ không trả mã bảo vệ hợp lệ. Vui lòng tải lại trang.");
  csrfToken = parsed.token;
}
export async function internalApi<T>(path: string, method = "GET", body?: unknown): Promise<T> {
  const normalizedMethod = method.toUpperCase();
  if (normalizedMethod !== "GET" && !csrfToken) await refreshCsrfToken();
  const send = () => fetch(`/api/internal${path}`, { method: normalizedMethod, credentials: "same-origin", cache: "no-store",
    headers: { "Content-Type": "application/json", ...(csrfToken ? { "X-CSRF-TOKEN": csrfToken } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) });
  let response = await send();
  let responseBody = response.status === 204 ? "" : await response.text();
  const optionalApprovalState = normalizedMethod === "GET" && /^\/contract-drafts\/\d+\/internal-approval$/.test(path);
  const error = response.ok ? null : readApiError(responseBody, response.status === 429
    ? "Bạn thao tác quá nhanh. Vui lòng thử lại sau một phút."
    : `Không thể xử lý yêu cầu ${normalizedMethod} ${path} (HTTP ${response.status}).`);
  if (normalizedMethod !== "GET" && response.status === 400 && error?.message?.includes("Phiên bảo vệ đã hết hạn")) {
    csrfToken = undefined;
    await refreshCsrfToken();
    response = await send();
    responseBody = response.status === 204 ? "" : await response.text();
  }
  if (!response.ok) {
    const finalError = error && response.status === 400 ? readApiError(responseBody, error.message ?? "Yêu cầu không hợp lệ.") :
      readApiError(responseBody, response.status === 429 ? "Bạn thao tác quá nhanh. Vui lòng thử lại sau một phút." : `Không thể xử lý yêu cầu ${normalizedMethod} ${path} (HTTP ${response.status}).`);
    if (response.status === 400) csrfToken = undefined;
    throw new InternalApiError(response.status, finalError.message ?? "Không thể xử lý yêu cầu.");
  }
  if (optionalApprovalState && (response.status === 204 || !responseBody.trim())) return null as T;
  if (response.status === 204) return undefined as T;
  if (!responseBody.trim()) throw new InternalApiError(response.status, `Máy chủ trả về nội dung rỗng cho ${normalizedMethod} ${path}; kết quả chưa được xác nhận.`);
  try { return JSON.parse(responseBody) as T; }
  catch { throw new InternalApiError(response.status, `Máy chủ trả dữ liệu không hợp lệ cho ${normalizedMethod} ${path}.`); }
}
export const money = (amount: number) => new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format(amount);
export const dateTime = (value: string) => new Date(value.endsWith("Z") ? value : value + "Z").toLocaleString("vi-VN");
export function estimateTotals(content: DraftContent): DraftTotals {
  const round = (n: number) => Math.round((n + Number.EPSILON) * 100) / 100;
  let subtotal = 0, discount = 0;
  for (const item of content.items) { const line = round(item.quantity * item.unitPrice); subtotal += line; discount += item.discountPercent > 0 ? round(line * item.discountPercent / 100) : item.discountAmount; }
  const vat = round((subtotal - discount) * content.vatPercent / 100);
  return { subtotal: round(subtotal), discount: round(discount), vat, total: round(subtotal - discount + vat) };
}
export type ReviewComment = { id: string; termKey: string | null; termTitle: string | null; parentId: string | null; content: string; authorName: string; fromCustomer: boolean; resolved: boolean; createdAt: string };
export type ReviewSummary = { id: string; versionId: string; versionNo: string; status: string; sentAt: string; sentByName: string; message: string | null; firstViewedAt: string | null; customerNote: string | null; respondedAt: string | null; followupDecision: string | null; followupNote: string | null; followupDecidedAt: string | null; rowVersion: string; comments: ReviewComment[] };
export type DraftVersionHistoryItem = { id: string; versionNo: string; changeNote: string | null; createdAt: string; isCurrent: boolean; locked: boolean; snapshotHash: string | null };
export type InternalApprovalState = { id: string; status: number; submittedAt: string; workflowName: string; comment: string | null; signingStarted: boolean };
export type CompanySigningQueueItem = { signatureId: string; documentId: string; versionId: string; code: string; contractName: string; customerName: string; signerName: string; createdAt: string };
export const documentStatusLabel: Record<number, string> = { 0: "Nháp", 1: "Đang thương lượng", 2: "Chờ kiểm tra", 3: "Chờ ký", 4: "Đã ký", 5: "Hoàn tất", 6: "Đã đóng", 7: "Khách từ chối" };
export const reviewStatusLabel: Record<string, string> = { SENT: "Đã gửi, khách chưa mở", VIEWED: "Khách đã xem", APPROVED: "Khách đồng ý", CHANGES_REQUESTED: "Khách yêu cầu sửa", REJECTED: "Khách từ chối", REVOKED: "Đã thu hồi", SUPERSEDED: "Đã thay bằng bản mới" };
