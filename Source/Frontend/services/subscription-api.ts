import { publicAxiosClient } from "@/lib/axios-interceptor";

export interface PublicPlanVersion {
  planVersionId: number;
  versionNumber: number;
  price: number;
  currency: string;
  billingCycle: string;
  durationDays: number | null;
  durationMonths: number | null;
}

export interface PublicPlan {
  planId: number;
  planCode: string;
  planName: string;
  planDescription: string | null;
  isPaid: boolean;
  allowsSupportTickets: boolean;
  maxUsers: number;
  maxContracts: number;
  maxScanPages: number;
  versions: PublicPlanVersion[];
}

export interface TenantCodeCheckResponse {
  tenantCode: string;
  available: boolean;
  errorCode: string | null;
  message: string | null;
}

export interface VoucherPreviewResponse {
  planVersionId: number;
  voucherCode: string | null;
  subtotal: number;
  discount: number;
  total: number;
  currency: string;
}

export interface SubscriptionCheckoutRequest {
  planVersionId: number;
  tenantCode: string;
  tenantName: string;
  fullName: string;
  email: string;
  phone: string;
  voucherCode?: string | null;
}

export interface SubscriptionCheckoutResponse {
  publicId: string;
  invoiceCode: string;
  total: number;
  currency: string;
  /** Có giá trị thì chuyển trình duyệt sang PayOS; null (gói 0đ) thì đi thẳng tới màn kết quả. */
  checkoutUrl: string | null;
}

export type SubscriptionStatus =
  | "PENDING"
  | "ACTIVE"
  | "PAYMENT_FAILED"
  | "CANCELLED"
  | "PROVISIONING_FAILED";

export interface SubscriptionStatusResponse {
  receiptPath: string | null;
  publicId: string;
  status: SubscriptionStatus;
  isPaid: boolean;
  message: string;
  tenantCode: string;
  tenantName: string;
  invoiceCode: string;
  total: number;
  currency: string;
  checkoutUrl: string | null;
}

const BASE_URL = "/api/public";

export const subscriptionApi = {
  getPlans: (signal?: AbortSignal) =>
    publicAxiosClient.get<unknown, PublicPlan[]>(`${BASE_URL}/plans`, { signal }),

  checkTenantCode: (code: string, signal?: AbortSignal) =>
    publicAxiosClient.get<unknown, TenantCodeCheckResponse>(`${BASE_URL}/tenant-code/check`, {
      params: { code },
      signal,
    }),

  previewVoucher: (planVersionId: number, voucherCode: string | null) =>
    publicAxiosClient.post<unknown, VoucherPreviewResponse>(`${BASE_URL}/vouchers/preview`, {
      planVersionId,
      voucherCode,
    }),

  checkout: (request: SubscriptionCheckoutRequest) =>
    publicAxiosClient.post<unknown, SubscriptionCheckoutResponse>(`${BASE_URL}/subscriptions/checkout`, request),

  getStatus: (publicId: string, signal?: AbortSignal) =>
    publicAxiosClient.get<unknown, SubscriptionStatusResponse>(
      `${BASE_URL}/subscriptions/${encodeURIComponent(publicId)}/status`,
      { signal, timeout: 15_000 },
    ),

  cancel: (publicId: string) =>
    publicAxiosClient.post<unknown, SubscriptionStatusResponse>(
      `${BASE_URL}/subscriptions/${encodeURIComponent(publicId)}/cancel`, {}, { timeout: 15_000 },
    ),
};
