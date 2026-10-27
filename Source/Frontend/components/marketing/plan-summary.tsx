import Link from "next/link";
import { ArrowRight, ShieldCheck } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { APP_NAME } from "@/lib/application-branding";
import { formatCurrency } from "@/lib/format-currency";
import type { PaidPlanPreview } from "@/lib/pricing-preview";
import type { SignupPlan } from "@/lib/signup-draft";

interface PlanSummaryProps {
  plan: SignupPlan | null;
  card: PaidPlanPreview | null;
  onContinue: () => void;
}

export function PlanSummary({ plan, card, onContinue }: PlanSummaryProps) {
  const isPaid = plan?.isPaid ?? false;
  const originalPrice = card?.originalPrice ?? plan?.price ?? 0;
  const discount = Math.max(0, originalPrice - (plan?.price ?? 0));

  return (
    <Card className="gap-5 border-primary/20 bg-card shadow-sm">
      <CardHeader>
        <h2 className="text-lg font-semibold">Lựa chọn của bạn</h2>
        <p className="mt-2 font-semibold text-primary">{APP_NAME}</p>
        <div aria-live="polite" aria-atomic="true" className="space-y-5">
          <p className="text-sm text-muted-foreground">{plan ? (isPaid ? `${plan.label} · Thanh toán một lần` : `${plan.label} · Miễn phí`) : "Chưa chọn gói"}</p>
          <dl className="space-y-3 text-sm">
            <div className="flex justify-between gap-3"><dt className="text-muted-foreground">Giá tham khảo</dt><dd className="tabular-nums">{formatCurrency(originalPrice, plan?.currency)}</dd></div>
            <div className="flex justify-between gap-3"><dt className="text-muted-foreground">Ưu đãi thời hạn</dt><dd className="tabular-nums">{discount ? `−${formatCurrency(discount, plan?.currency)}` : "—"}</dd></div>
          </dl>
          <div className="border-t border-border pt-5">
            <p className="text-sm text-muted-foreground">Tổng</p>
            <p className="mt-2 text-3xl font-bold tracking-tight tabular-nums" data-testid="plan-total">{formatCurrency(plan?.price ?? 0, plan?.currency)}</p>
            <p className="mt-2 text-xs leading-5 text-muted-foreground">{isPaid && card ? `Tương đương ${formatCurrency((plan?.price ?? 0) / card.months, plan?.currency)} / tháng` : "Không cần thanh toán cho gói dùng thử."}</p>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <Button type="button" className="w-full" disabled={!plan} onClick={onContinue}>
          {isPaid ? "Đăng ký và thanh toán" : "Đăng ký dùng thử"} <ArrowRight aria-hidden="true" />
        </Button>
        <p className="text-xs leading-5 text-muted-foreground">
          Đã có tài khoản? <Link href="/" className="font-medium text-primary underline-offset-4 hover:underline">Đăng nhập</Link>
        </p>
        <div className="flex gap-2 border-t border-border pt-4 text-xs leading-5 text-muted-foreground">
          <ShieldCheck className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          <p>Thanh toán an toàn qua PayOS. Tài khoản đăng nhập được gửi tới email của bạn ngay sau khi kích hoạt.</p>
        </div>
      </CardContent>
    </Card>
  );
}
