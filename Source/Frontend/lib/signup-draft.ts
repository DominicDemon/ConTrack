import type { PaidPlanPreview, PlanSelection } from "@/lib/pricing-preview";
import type { PublicPlan } from "@/services/subscription-api";

/** Gói đã chọn ở màn (1), đã quy về một PLAN_VERSION cụ thể. */
export interface SignupPlan {
  planVersionId: number;
  planName: string;
  isPaid: boolean;
  price: number;
  currency: string;
  durationDays: number;
  durationMonths?: number | null;
  label: string;
}

/** Thông tin màn (2). Lưu sessionStorage để refresh màn (3) không mất dữ liệu. */
export interface SignupInfo {
  tenantCode: string;
  tenantName: string;
  fullName: string;
  email: string;
  phone: string;
}

export interface SignupDraft {
  plan: SignupPlan;
  info?: SignupInfo;
  voucherCode?: string;
}

const STORAGE_KEY = "contrack.signup-draft";

export function readSignupDraft(): SignupDraft | null {
  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const draft = JSON.parse(raw) as SignupDraft;
    return typeof draft?.plan?.planVersionId === "number" ? draft : null;
  } catch {
    return null;
  }
}

export function writeSignupDraft(draft: SignupDraft) {
  try {
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(draft));
  } catch {
    // Trình duyệt chặn storage: luồng vẫn chạy trong tab hiện tại, chỉ mất khả năng giữ dữ liệu khi refresh.
  }
}

export function clearSignupDraft() {
  try {
    window.sessionStorage.removeItem(STORAGE_KEY);
  } catch {
    // bỏ qua
  }
}

const monthsOf = (months: number | null) => months ?? 0;

/** Thẻ gói trả phí cho trang /pricing, tính "Tiết kiệm x%" so với phiên bản ngắn nhất. */
export function toPaidPlanCards(plans: PublicPlan[]): PaidPlanPreview[] {
  const versions = plans.filter((plan) => plan.isPaid).flatMap((plan) => plan.versions).filter((v) => v.durationMonths != null && v.durationMonths >= 1 && v.durationMonths <= 24);
  if (versions.length === 0) return [];
  const base = versions.reduce((shortest, v) => (monthsOf(v.durationMonths) < monthsOf(shortest.durationMonths) ? v : shortest));
  const baseMonthly = base.price / monthsOf(base.durationMonths);
  // Recommend the lowest monthly cost; prefer the shorter commitment on ties.
  const bestValue = versions.reduce((best, v) => {
    const monthly = v.price / monthsOf(v.durationMonths);
    const bestMonthly = best.price / monthsOf(best.durationMonths);
    return monthly < bestMonthly || (monthly === bestMonthly && monthsOf(v.durationMonths) < monthsOf(best.durationMonths)) ? v : best;
  });
  return [...versions].sort((a, b) => monthsOf(a.durationMonths) - monthsOf(b.durationMonths)).map((v) => {
    const months = monthsOf(v.durationMonths);
    const originalPrice = Math.round(baseMonthly * months);
    const saving = originalPrice > 0 ? Math.round((1 - v.price / originalPrice) * 100) : 0;
    return {
      months,
      price: v.price,
      originalPrice,
      caption: saving > 0 ? `Tiết kiệm ${saving}%` : "Linh hoạt khởi đầu",
      recommended: versions.length > 1 && v.planVersionId === bestValue.planVersionId,
    };
  });
}

export function getTrialPlan(plans: PublicPlan[]) {
  return plans.find((plan) => !plan.isPaid && plan.versions.length > 0) ?? null;
}

/** Quy lựa chọn trên URL (/pricing?plan=...&months=...) về một PLAN_VERSION của API. */
export function resolveSignupPlan(plans: PublicPlan[], selection: PlanSelection): SignupPlan | null {
  if (selection.kind === "trial") {
    const trial = getTrialPlan(plans);
    const version = trial?.versions[0];
    if (!trial || !version) return null;
    const days = version.durationDays ?? 0;
    return { planVersionId: version.planVersionId, planName: trial.planName, isPaid: false, price: version.price, currency: version.currency, durationDays: days, label: `Dùng thử ${days} ngày` };
  }
  for (const plan of plans.filter((item) => item.isPaid)) {
    const version = plan.versions.find((v) => monthsOf(v.durationMonths) === selection.months);
    if (version) {
      return { planVersionId: version.planVersionId, planName: plan.planName, isPaid: true, price: version.price, currency: version.currency, durationMonths: version.durationMonths, durationDays: version.durationDays ?? selection.months * 30, label: `${plan.planName} · ${selection.months} tháng` };
    }
  }
  return null;
}

// Khớp quy tắc backend (SubscriptionRules) để báo lỗi ngay trên form; server vẫn kiểm tra lại.
export const TENANT_CODE_PATTERN = /^[a-z0-9][a-z0-9-]{1,28}[a-z0-9]$/;
export const normalizeTenantCode = (value: string) => value.trim().toLowerCase();
export const normalizePhone = (value: string) => value.replace(/[\s.-]/g, "");

export function validateSignupInfo(info: SignupInfo): Partial<Record<keyof SignupInfo, string>> {
  const errors: Partial<Record<keyof SignupInfo, string>> = {};
  if (!TENANT_CODE_PATTERN.test(normalizeTenantCode(info.tenantCode)))
    errors.tenantCode = "Mã tổ chức dài 3–30 ký tự, chỉ gồm chữ thường a–z, số và dấu gạch ngang, không bắt đầu/kết thúc bằng dấu gạch ngang.";
  if (!info.tenantName.trim() || info.tenantName.trim().length > 255) errors.tenantName = "Tên tổ chức không được để trống và tối đa 255 ký tự.";
  if (!info.fullName.trim() || info.fullName.trim().length > 100) errors.fullName = "Họ tên không được để trống và tối đa 100 ký tự.";
  const email = info.email.trim();
  if (email.length > 50 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) errors.email = "Email không đúng định dạng hoặc dài quá 50 ký tự.";
  if (!/^0\d{9}$/.test(normalizePhone(info.phone))) errors.phone = "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.";
  return errors;
}
