import type { DraftContent } from "@/services/internal-api";
export type CustomerSession = { id: string; name: string; mustChangePassword: boolean };
export type CustomerReviewSummary = { id: string; contractName: string; versionNo: string; status: string; sentAt: string; respondedAt: string | null };
export type ReviewComment = { id: string; termKey: string | null; termTitle: string | null; parentId: string | null; content: string; authorName: string; fromCustomer: boolean; resolved: boolean; createdAt: string };
export type CustomerReviewDetail = { id: string; contractName: string; versionNo: string; status: string; sentAt: string; message: string | null; customerNote: string | null;
  respondedAt: string | null; documentHash: string; rowVersion: string; content: DraftContent; comments: ReviewComment[] };
export const customerStatusLabel: Record<string, string> = { SENT: "Chờ bạn xem", VIEWED: "Chờ bạn phản hồi", APPROVED: "Bạn đã đồng ý", CHANGES_REQUESTED: "Bạn đã yêu cầu sửa",
  REJECTED: "Bạn đã từ chối", REVOKED: "Đã thu hồi", SUPERSEDED: "Đã có phiên bản mới" };
export const isOpenReview = (status: string) => status === "SENT" || status === "VIEWED";
export class CustomerApiError extends Error { constructor(public status: number, message: string, public code?: string) { super(message); } }
let csrfToken: string | undefined;
export async function customerApi<T>(path: string, method = "GET", body?: unknown): Promise<T> {
  if (method !== "GET" && !csrfToken) {
    const response = await fetch("/api/customer/auth/csrf", { credentials: "same-origin", cache: "no-store" });
    if (!response.ok) throw new CustomerApiError(response.status, "Không kết nối được hệ thống. Vui lòng thử lại.");
    csrfToken = (await response.json()).token;
  }
  const response = await fetch(`/api/customer${path}`, { method, credentials: "same-origin", cache: "no-store",
    headers: { "Content-Type": "application/json", ...(csrfToken ? { "X-CSRF-TOKEN": csrfToken } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) });
  if (!response.ok) {
    const error = await response.json().catch(() => null);
    if (response.status === 400) csrfToken = undefined;
    throw new CustomerApiError(response.status, error?.message ?? (response.status === 429 ? "Bạn thao tác quá nhanh. Vui lòng thử lại sau." : "Không thể xử lý yêu cầu. Vui lòng thử lại."), error?.code ?? undefined);
  }
  return response.status === 204 ? undefined as T : response.json();
}
/** Sends the customer to the right page for auth errors; returns false when the error was handled that way. */
export function handleAuthError(error: unknown, replace: (url: string) => void): boolean {
  if (error instanceof CustomerApiError && error.status === 401) { replace("/portal/login"); return false; }
  if (error instanceof CustomerApiError && error.code === "PASSWORD_CHANGE_REQUIRED") { replace("/portal/change-password"); return false; }
  return true;
}
