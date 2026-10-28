import type { BillingCycle, ServicePlan } from "./plan-management";

function plan(planId: number, planCode: string, planName: string, cycle: BillingCycle, durationDays: number, price: number, activeSubscribers: number): ServicePlan {
  const isPaid = cycle !== "TRIAL";
  return {
    planId, planCode, planName,
    planDescription: isPaid ? `Sử dụng eContract trong ${planName.toLowerCase()}, bao gồm hỗ trợ khách hàng.` : "Trải nghiệm quy trình chính trong 15 ngày, không bao gồm hỗ trợ khách hàng.",
    planStatus: "ACTIVE", isPaid, allowsSupportTickets: isPaid,
    maxUsers: isPaid ? 20 : 2, maxContracts: isPaid ? 1000 : 10, maxScanPages: isPaid ? 1000 : 20, activeSubscribers,
    versions: [{ planVersionId: planId * 100000 + 1, versionNumber: 1, price, currency: "VND", billingCycle: cycle, durationDays,
      effectiveFrom: "2026-10-01T00:00:00Z", effectiveTo: null, status: "ACTIVE" }],
  };
}

// Mỗi dòng mẫu là một thời hạn trên UI. API thật hiện nhóm các thời hạn trả phí
// trong cùng PLAN; khi nối API cần ánh xạ theo PLAN_VERSION, không dùng ID mẫu.
// Giá, thời hạn, giới hạn theo SaasSeedData; số thuê bao chỉ để minh họa.
export function createMockServicePlans(): ServicePlan[] {
  return [
    plan(1, "TRIAL", "Dùng thử 15 ngày", "TRIAL", 15, 0, 12),
    plan(2, "PLAN_3M", "3 tháng", "QUARTERLY", 90, 900000, 45),
    plan(3, "PLAN_6M", "6 tháng", "SEMI_ANNUAL", 180, 1620000, 18),
    plan(4, "PLAN_12M", "12 tháng", "YEARLY", 365, 2880000, 8),
  ];
}
