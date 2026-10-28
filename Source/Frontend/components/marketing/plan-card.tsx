import { Badge } from "@/components/ui/badge";
import { formatCurrency } from "@/lib/format-currency";
import type { PaidPlanPreview } from "@/lib/pricing-preview";

interface PlanCardProps {
  plan: PaidPlanPreview;
  selected: boolean;
  onSelect: () => void;
}

export function PlanCard({ plan, selected, onSelect }: PlanCardProps) {
  return (
    <label className="relative block h-full cursor-pointer">
      <input
        type="radio"
        name="pricing-plan"
        value={plan.months}
        checked={selected}
        onChange={onSelect}
        aria-label={`Gói ${plan.months} tháng`}
        className="peer sr-only"
      />
      <span className="flex h-full flex-col gap-3 rounded-xl border border-border bg-card p-4 shadow-sm transition-colors hover:border-primary/50 peer-checked:border-primary peer-checked:bg-primary/5 peer-checked:ring-1 peer-checked:ring-primary peer-focus-visible:outline-2 peer-focus-visible:outline-offset-4 peer-focus-visible:outline-ring">
        <span className="flex items-center justify-between gap-2">
          <span className="font-semibold">{plan.months} tháng</span>
          <span aria-hidden="true" className={selected ? "size-4 shrink-0 rounded-full border-[5px] border-primary bg-card" : "size-4 shrink-0 rounded-full border border-muted-foreground/50 bg-card"} />
        </span>
        <span className="break-words text-2xl font-bold tracking-tight tabular-nums">{formatCurrency(plan.price)}</span>
        <span className="text-xs text-muted-foreground">{formatCurrency(plan.price / plan.months)} / tháng</span>
        <span className="mt-auto flex min-h-6 flex-wrap items-center gap-2 text-xs font-medium text-primary">
          {plan.caption}
          {plan.recommended && <Badge variant="secondary" className="text-[10px]" title="Giá trung bình mỗi tháng thấp nhất">Đề xuất</Badge>}
        </span>
      </span>
    </label>
  );
}
