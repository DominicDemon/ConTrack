export type BillingCycle = "TRIAL" | "QUARTERLY" | "SEMI_ANNUAL" | "YEARLY" | "CUSTOM";
export interface PlanVersion {
  planVersionId: number;
  versionNumber: number;
  price: number;
  currency: "VND";
  billingCycle: BillingCycle;
  durationDays: number;
  durationMonths?: number;
  effectiveFrom: string;
  effectiveTo: string | null;
  status: "ACTIVE" | "INACTIVE";
}
export interface ServicePlan {
  planId: number;
  revision?: number;
  planCode: string;
  planName: string;
  planDescription: string;
  planStatus: "ACTIVE" | "INACTIVE";
  isPaid: boolean;
  allowsSupportTickets: boolean;
  maxUsers: number;
  maxContracts: number;
  maxScanPages: number;
  /** Số tổ chức có thuê bao hoạt động, cộng trên mọi phiên bản giá của gói. */
  activeSubscribers: number;
  versions: PlanVersion[];
}
export const CYCLE_LABELS: Record<BillingCycle, string> = {
  TRIAL: "Dùng thử", QUARTERLY: "3 tháng", SEMI_ANNUAL: "6 tháng", YEARLY: "12 tháng", CUSTOM: "Tùy chỉnh",
};
export function planDurationMonths(version: PlanVersion): number {
  return version.durationMonths ?? (version.billingCycle === "YEARLY" ? 12 : version.billingCycle === "SEMI_ANNUAL" ? 6 : version.billingCycle === "QUARTERLY" ? 3 : Math.round(version.durationDays / 30));
}
export function formatPlanDuration(version: PlanVersion): string {
  return version.billingCycle === "TRIAL" ? `${version.durationDays} ngày` : `${planDurationMonths(version)} tháng`;
}
export function cycleForMonths(months: number): BillingCycle {
  return months === 3 ? "QUARTERLY" : months === 6 ? "SEMI_ANNUAL" : months === 12 ? "YEARLY" : "CUSTOM";
}
export const formatPlanPrice = (price: number) => new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" }).format(price);
export function currentVersions(plan: ServicePlan) {
  return plan.versions.filter((version) => version.status === "ACTIVE" && version.effectiveTo === null)
    .sort((a, b) => a.durationDays - b.durationDays);
}
export function addPriceVersion(plan: ServicePlan, cycle: BillingCycle, price: number, durationDays: number, now: string, durationMonths?: number): ServicePlan {
  const nextNumber = Math.max(0, ...plan.versions.map((v) => v.versionNumber)) + 1;
  return {
    ...plan,
    versions: [
      ...plan.versions.map((v): PlanVersion => v.billingCycle === cycle && v.status === "ACTIVE"
        ? { ...v, status: "INACTIVE", effectiveTo: now } : v),
      { planVersionId: plan.planId * 100000 + nextNumber, versionNumber: nextNumber, price, currency: "VND", billingCycle: cycle, durationDays, durationMonths, effectiveFrom: now, effectiveTo: null, status: "ACTIVE" },
    ],
  };
}
