export type InternalUser = { id: string; name: string; username: string; permissions: string[]; canManageAll: boolean; canWrite: boolean };
export type DraftField = { key: string; label: string; required: boolean; dataType?: "TEXT" | "NUMBER" | "DATE" | "SELECT"; options?: string[]; min?: number | null; max?: number | null };
export type DraftTerm = { key: string; title: string; content: string; editable: boolean };
export type TemplateDefinition = { id: string; name: string; revision: string; isDemo: boolean; isImported?: boolean; fields: DraftField[]; terms: DraftTerm[] };
export type CustomerOption = { id: string; name: string; phone: string; fields: Record<string, string> };
export type CustomerInput = { name: string; phone: string; email: string; company: string; taxCode: string; address: string };
export type ProductOption = { id: string; name: string; price: number };
export type EmployeeOption = { id: string; name: string };
export type DraftItem = { productId: string | null; name: string; quantity: number; unitPrice: number; discountPercent: number; discountAmount: number };
export type DraftTotals = { subtotal: number; discount: number; vat: number; total: number };
export type BusinessOption = { id: string; name: string; taxCode: string; address: string; representative: string };
export type DraftMilestone = { key: string; title: string; termKey: string; percent: number; amount: number; anchor: "DATE" | "SIGNED" | "HANDOVER"; offsetDays: number; dueDate: string | null; condition: string };
export type DraftContent = { formatVersion?: number; partyType?: string; customerBusinessId?: string | null; paymentMode?: "PERCENT" | "AMOUNT"; milestones?: DraftMilestone[]; templateId: string; customerId: string; name: string; fields: Record<string, string>; items: DraftItem[]; terms: DraftTerm[]; vatPercent: number; effectiveDate: string | null; expireDate: string | null; templateSnapshot: TemplateDefinition | null; totals: DraftTotals; missingFields: string[] };
export type DraftIssue = { code: string; path: string; message: string };
export type CarriedDraftComment = { id: string; termKey: string | null; content: string; sourceCommentId: string | null; sourceVersionId: string | null; resolved: boolean };
export type DraftRecord = { issues?: DraftIssue[]; carriedComments?: CarriedDraftComment[]; id: string; code: string; employeeId: string; employeeName: string; rowVersion: string; versionRowVersion: string; updatedAt: string; content: DraftContent; status: number; versionNo: string; locked: boolean; canEdit: boolean; customerName: string; customerPhoneMasked: string; latestReviewStatus: string | null; sendBlockedReason: string | null };
export type DraftSummary = { id: string; code: string; name: string; customerName: string; employeeId: string; employeeName: string; updatedAt: string; missingCount: number; status: number; latestReviewStatus: string | null };
export class InternalApiError extends Error { constructor(public status: number, message: string, public issues: DraftIssue[] = []) { super(message); } }
let csrfToken: string | undefined;
export async function internalApi<T>(path: string, method = "GET", body?: unknown): Promise<T> {
  if (method !== "GET" && !csrfToken) {
    const response = await fetch("/api/internal/auth/csrf", { credentials: "same-origin", cache: "no-store" });
    if (!response.ok) throw new InternalApiError(response.status, "Không kết nối được hệ thống. Vui lòng thử lại.");
    csrfToken = (await response.json()).token;
  }
  const response = await fetch(`/api/internal${path}`, { method, credentials: "same-origin", cache: "no-store",
    headers: { "Content-Type": "application/json", ...(csrfToken ? { "X-CSRF-TOKEN": csrfToken } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) });
  if (!response.ok) {
    const error = await response.json().catch(() => null);
    if (response.status === 400) csrfToken = undefined;
    throw new InternalApiError(response.status, error?.message ?? (response.status === 429 ? "Bạn thao tác quá nhanh. Vui lòng thử lại sau một phút." : "Không thể xử lý yêu cầu. Vui lòng thử lại."), error?.issues ?? []);
  }
  return response.status === 204 ? undefined as T : response.json();
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
export const documentStatusLabel: Record<number, string> = { 0: "Nháp", 1: "Đang thương lượng", 2: "Chờ kiểm tra", 3: "Chờ ký", 4: "Đã ký", 5: "Hoàn tất", 6: "Đã đóng", 7: "Khách từ chối" };
export const reviewStatusLabel: Record<string, string> = { SENT: "Đã gửi, khách chưa mở", VIEWED: "Khách đã xem", APPROVED: "Khách đồng ý", CHANGES_REQUESTED: "Khách yêu cầu sửa", REJECTED: "Khách từ chối", REVOKED: "Đã thu hồi", SUPERSEDED: "Đã thay bằng bản mới" };

// Older uploaded templates did not declare the company fields.
export function completePartyFields(template: TemplateDefinition): TemplateDefinition {
  if (!template.isImported) return template;
  const required: DraftField[] = [
    {key:"PROVIDER_LEGAL_NAME",label:"Tên pháp lý công ty",required:true},
    {key:"PROVIDER_REPRESENTATIVE_NAME",label:"Người đại diện công ty",required:true},
    {key:"PROVIDER_ADDRESS",label:"Địa chỉ công ty",required:true},
    {key:"PROVIDER_TAX_CODE",label:"Mã số thuế công ty",required:true},
  ];
  return {...template,fields:[...template.fields,...required.filter(f => !template.fields.some(x => x.key === f.key))]};
}
