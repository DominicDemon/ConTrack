// Marketing copy only; prices, durations and limits come from GET /api/public/plans.
export const TRIAL_DAYS = 15;

export interface PaidPlanPreview {
  months: number;
  price: number;
  originalPrice: number;
  caption: string;
  recommended?: boolean;
}

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
  const months = Number(params.get("months"));
  return { kind: "paid", months: Number.isInteger(months) && months >= 1 && months <= 24 ? months : 12 };
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
