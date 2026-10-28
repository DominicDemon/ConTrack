import axiosClient, { publicAxiosClient } from "@/lib/axios-interceptor";

export interface InvoiceReadiness {
  status: string;
  isPaid: boolean;
  tenantCode: string | null;
  returnTo: string | null;
}
export interface PaymentConfirmation {
  publicId: string; invoiceCode: string; status: string;
  billingName: string; buyerName: string | null; billingEmail: string | null;
  billingPhone: string | null; billingAddress: string | null; billingTaxIdentifier: string | null;
  planName: string | null; durationMonths: number | null; durationDays: number | null;
  subtotal: number; discountAmount: number; totalAmount: number; currency: string;
  paidAt: string | null; paymentMethod: string | null; paymentReference: string | null;
}
export interface InvoiceListItem {
  publicId: string; invoiceCode: string; planName: string | null;
  durationMonths: number | null; durationDays: number | null;
  totalAmount: number; currency: string; paidAt: string | null;
  startAt: string | null; endAt: string | null;
}
export interface InvoiceListResponse {
  items: InvoiceListItem[]; total: number; page: number; pageSize: number;
}
export const invoicesApi = {
  list: (page: number, signal: AbortSignal) => axiosClient.get<unknown, InvoiceListResponse>(
    "/api/billing/invoices", { params: { page }, signal, timeout: 15_000 }),
  readiness: (id: string, signal: AbortSignal) => publicAxiosClient.get<unknown, InvoiceReadiness>(
    `/api/public/invoices/${encodeURIComponent(id)}/readiness`, { signal, timeout: 15_000 }),
  get: (id: string, signal: AbortSignal) => axiosClient.get<unknown, PaymentConfirmation>(
    `/api/billing/invoices/${encodeURIComponent(id)}`, { signal, timeout: 15_000 }),
};
