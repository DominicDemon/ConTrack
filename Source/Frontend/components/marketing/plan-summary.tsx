import Link from "next/link";
import { ArrowRight, Building2, Clock3, Loader2, RefreshCw } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import type { PricingSession } from "@/hooks/use-pricing-session";
import { APP_NAME } from "@/lib/application-branding";
import { formatCurrency } from "@/lib/format-currency";
import { getPricingLoginHref, PAID_PLANS, TRIAL_DAYS, type PlanSelection } from "@/lib/pricing-preview";

interface PlanSummaryProps {
  selection: PlanSelection;
  session: PricingSession;
  onRetry: () => void;
}

export function PlanSummary({ selection, session, onRetry }: PlanSummaryProps) {
  const plan = selection.kind === "paid"
    ? PAID_PLANS.find((item) => item.months === selection.months)!
    : null;
  const discount = plan ? plan.originalPrice - plan.price : 0;

  return (
    <Card className="gap-5 border-primary/20 bg-card shadow-sm">
      <CardHeader>
        <h2 className="text-lg font-semibold">Lựa chọn của bạn</h2>
        <p className="mt-2 font-semibold text-primary">{APP_NAME}</p>
        <div aria-live="polite" aria-atomic="true" className="space-y-5">
          <p className="text-sm text-muted-foreground">{plan ? `${plan.months} tháng · Thanh toán một lần` : `Dùng thử ${TRIAL_DAYS} ngày · Miễn phí`}</p>
          <dl className="space-y-3 text-sm">
            <div className="flex justify-between gap-3"><dt className="text-muted-foreground">Giá tham khảo</dt><dd className="tabular-nums">{formatCurrency(plan?.originalPrice ?? 0)}</dd></div>
            <div className="flex justify-between gap-3"><dt className="text-muted-foreground">Ưu đãi thời hạn</dt><dd className="tabular-nums">{discount ? `−${formatCurrency(discount)}` : "—"}</dd></div>
          </dl>
          <div className="border-t border-border pt-5">
            <p className="text-sm text-muted-foreground">Tổng dự kiến</p>
            <p className="mt-2 text-3xl font-bold tracking-tight tabular-nums" data-testid="plan-total">{formatCurrency(plan?.price ?? 0)}</p>
            <p className="mt-2 text-xs leading-5 text-muted-foreground">{plan ? `Tương đương ${formatCurrency(plan.price / plan.months)} / tháng` : "Không cần thanh toán cho gói dùng thử."}</p>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {session.status === "checking" && (
          <p role="status" className="flex items-center gap-2 text-sm text-muted-foreground"><Loader2 className="size-4 animate-spin" aria-hidden="true" />Đang kiểm tra phiên đăng nhập...</p>
        )}
        {session.status === "guest" && (
          <>
            <Button asChild className="w-full"><Link href={getPricingLoginHref(selection)}>Đăng nhập để tiếp tục <ArrowRight aria-hidden="true" /></Link></Button>
            <p className="text-xs leading-5 text-muted-foreground">Dành cho tài khoản doanh nghiệp đã có. Lựa chọn gói sẽ được giữ khi bạn đăng nhập.</p>
            <p className="text-xs leading-5 text-muted-foreground">Chưa có tài khoản? Đăng ký tài khoản mới sẽ được mở sau.</p>
          </>
        )}
        {session.status === "error" && (
          <Alert variant="destructive">
            <AlertTitle>Chưa kiểm tra được tài khoản</AlertTitle>
            <AlertDescription>
              <p>Bạn vẫn có thể xem các gói. Vui lòng thử lại để kiểm tra phiên đăng nhập.</p>
              <Button type="button" variant="outline" size="sm" onClick={onRetry} className="mt-3"><RefreshCw aria-hidden="true" />Thử lại</Button>
            </AlertDescription>
          </Alert>
        )}
        {session.status === "authenticated" && (
          <>
            <div className="flex gap-2.5 rounded-lg border border-border bg-secondary/50 p-3">
              <Building2 className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden="true" />
              <div className="min-w-0 text-xs leading-5">
                <p className="font-medium break-words">{session.user.tenantName}</p>
                <p className="break-words text-muted-foreground">Đã đăng nhập: {session.user.fullName || session.user.account}</p>
              </div>
            </div>
            {session.user.mustChangePassword ? (
              <>
                <p className="text-xs leading-5 text-muted-foreground">Tài khoản của bạn cần đổi mật khẩu trước khi tiếp tục sử dụng.</p>
                <Button asChild className="w-full"><Link href="/change-password?required=1">Đổi mật khẩu <ArrowRight aria-hidden="true" /></Link></Button>
              </>
            ) : (
              <Button disabled className="w-full">{plan ? "Thanh toán chưa mở" : "Đăng ký dùng thử chưa mở"}</Button>
            )}
          </>
        )}
        <div className="flex gap-2 border-t border-border pt-4 text-xs leading-5 text-muted-foreground">
          <Clock3 className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          <p>Đăng ký gói và thanh toán trực tuyến chưa mở. Lựa chọn trên trang chưa tạo đơn hàng hoặc kích hoạt dịch vụ.</p>
        </div>
      </CardContent>
    </Card>
  );
}
