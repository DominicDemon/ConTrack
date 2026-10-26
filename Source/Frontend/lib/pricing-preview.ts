// Presentation data approved for the FE preview, not a billing source of truth.
// The checkout API must eventually supply prices, eligibility and entitlements.
export const TRIAL_DAYS = 15;

export interface PaidPlanPreview {
  months: 3 | 6 | 12;
  price: number;
  originalPrice: number;
  caption: string;
  recommended?: boolean;
}

export const PAID_PLANS: readonly PaidPlanPreview[] = [
  { months: 3, price: 900_000, originalPrice: 900_000, caption: "Linh hoạt khởi đầu" },
  { months: 6, price: 1_620_000, originalPrice: 1_800_000, caption: "Tiết kiệm 10%" },
  { months: 12, price: 2_880_000, originalPrice: 3_600_000, caption: "Tiết kiệm 20%", recommended: true },
];

export type PlanSelection =
  | { kind: "trial" }
  | { kind: "paid"; months: PaidPlanPreview["months"] };

export const COMMON_PLAN_FEATURES = [
  "Quản lý hồ sơ & mẫu hợp đồng",
  "Đàm phán & phê duyệt nội bộ",
  "Bàn giao & nghiệm thu",
  "Theo dõi thanh toán hợp đồng",
] as const;

export function readPlanSelection(params: { get: (key: string) => string | null }): PlanSelection {
  if (params.get("plan") === "trial") return { kind: "trial" };
  const months = params.get("months");
  return { kind: "paid", months: months === "3" ? 3 : months === "6" ? 6 : 12 };
}

export function getPricingHref(selection: PlanSelection) {
  return selection.kind === "trial"
    ? "/pricing?plan=trial"
    : `/pricing?plan=paid&months=${selection.months}`;
}

// Only allow the pricing return flow. Rebuild its query to discard arbitrary
// destinations, fragments and parameters instead of trusting user-supplied URLs.
export function getPricingReturnPath(value: string | null): string | null {
  if (!value || value.length > 512 || !/^\/pricing(?:\?|$)/.test(value)) return null;
  try {
    const url = new URL(value, "https://contrack.invalid");
    if (url.origin !== "https://contrack.invalid" || url.pathname !== "/pricing") return null;
    return getPricingHref(readPlanSelection(url.searchParams));
  } catch {
    return null;
  }
}

export function getPricingLoginHref(selection: PlanSelection) {
  return `/?${new URLSearchParams({ returnTo: getPricingHref(selection) })}`;
}
