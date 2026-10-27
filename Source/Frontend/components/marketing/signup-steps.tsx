import { Check } from "lucide-react";

import { cn } from "@/lib/utils";

const STEPS = ["Chọn gói", "Thông tin", "Xác nhận", "Thanh toán", "Kết quả"] as const;

/** Thanh tiến trình luồng đăng ký; current là chỉ số 0–4. */
export function SignupSteps({ current }: { current: number }) {
  return (
    <ol className="mb-8 flex flex-wrap items-center gap-x-3 gap-y-2 text-xs sm:text-sm" aria-label="Các bước đăng ký">
      {STEPS.map((label, index) => {
        const done = index < current;
        const active = index === current;
        return (
          <li key={label} className="flex items-center gap-2" aria-current={active ? "step" : undefined}>
            <span className={cn(
              "flex size-6 items-center justify-center rounded-full border text-[11px] font-semibold",
              done && "border-primary bg-primary text-primary-foreground",
              active && "border-primary text-primary",
              !done && !active && "border-border text-muted-foreground",
            )}>
              {done ? <Check className="size-3.5" aria-hidden="true" /> : index + 1}
            </span>
            <span className={cn(active ? "font-semibold text-foreground" : "text-muted-foreground")}>{label}</span>
            {index < STEPS.length - 1 && <span aria-hidden="true" className="hidden h-px w-6 bg-border sm:block" />}
          </li>
        );
      })}
    </ol>
  );
}
