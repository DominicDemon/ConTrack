"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { Check, Gift, Headphones, Info, X } from "lucide-react";

import { PlanCard } from "@/components/marketing/plan-card";
import { PlanSummary } from "@/components/marketing/plan-summary";
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from "@/components/ui/accordion";
import { Badge } from "@/components/ui/badge";
import { usePricingSession } from "@/hooks/use-pricing-session";
import { COMMON_PLAN_FEATURES, getPricingHref, PAID_PLANS, readPlanSelection, TRIAL_DAYS, type PlanSelection } from "@/lib/pricing-preview";

export function PricingContent() {
  const router = useRouter();
  const params = useSearchParams();
  const selection = readPlanSelection(params);
  const isTrial = selection.kind === "trial";
  const { session, retry } = usePricingSession();

  const handleSelect = (next: PlanSelection) => {
    router.replace(getPricingHref(next), { scroll: false });
  };

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6 sm:py-12 lg:px-8">
      <header className="mb-8 max-w-2xl">
        <p className="mb-3 text-xs font-semibold tracking-widest text-primary uppercase">Gói dịch vụ</p>
        <h1 className="text-3xl font-bold leading-tight tracking-tight sm:text-4xl">Một gói đầy đủ.<br className="sm:hidden" /> Thời hạn linh hoạt.</h1>
        <p className="mt-4 text-sm leading-6 text-muted-foreground sm:text-base">Chọn 3, 6 hoặc 12 tháng. Đồng hành lâu hơn, tiết kiệm hơn.</p>
      </header>
      <div className="mb-7 flex items-start gap-2 rounded-lg border border-border bg-secondary/50 px-4 py-3 text-xs leading-5 text-muted-foreground">
        <Info className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
        <p>Giá và hạn mức dưới đây là thông tin minh họa, chưa phải báo giá chính thức. Hiện chưa tiếp nhận đăng ký gói hoặc thanh toán.</p>
      </div>
      <div className="grid items-start gap-8 lg:grid-cols-[minmax(0,1fr)_320px]">
        <div className="min-w-0">
          <fieldset className="min-w-0">
            <legend className="mb-4 flex items-center gap-2.5 font-semibold"><span className="flex size-6 items-center justify-center rounded-full bg-primary/10 text-xs text-primary">1</span>Chọn gói sử dụng</legend>
            <label className="mb-6 block cursor-pointer">
              <input type="radio" name="pricing-plan" value="trial" checked={isTrial} onChange={() => handleSelect({ kind: "trial" })} aria-label={`Dùng thử ${TRIAL_DAYS} ngày`} className="peer sr-only" />
              <span className="flex flex-wrap items-center gap-3 rounded-xl border border-border bg-card p-4 transition-colors hover:border-primary/50 peer-checked:border-primary peer-checked:bg-primary/5 peer-checked:ring-1 peer-checked:ring-primary peer-focus-visible:outline-2 peer-focus-visible:outline-offset-4 peer-focus-visible:outline-ring sm:p-5">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"><Gift className="size-5" aria-hidden="true" /></span>
                <span className="min-w-0 flex-1 space-y-1">
                  <span className="block text-sm font-semibold">Chưa sẵn sàng? Dùng thử {TRIAL_DAYS} ngày.</span>
                  <span className="block text-xs leading-5 text-muted-foreground">Trải nghiệm quy trình chính · Không bao gồm Customer Support</span>
                </span>
                <Badge variant={isTrial ? "default" : "secondary"}>{isTrial ? "Đã chọn dùng thử" : "Miễn phí"}</Badge>
              </span>
            </label>
            <div className="grid gap-3 sm:grid-cols-3">
              {PAID_PLANS.map((plan) => (
                <PlanCard key={plan.months} plan={plan} selected={selection.kind === "paid" && selection.months === plan.months} onSelect={() => handleSelect({ kind: "paid", months: plan.months })} />
              ))}
            </div>
          </fieldset>
          <section aria-labelledby="plan-benefits" className="mt-7 rounded-xl border border-border bg-card p-5 sm:p-6">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <h2 id="plan-benefits" className="font-semibold">{isTrial ? "Quyền lợi gói dùng thử" : "Cả ba thời hạn đều bao gồm"}</h2>
              <Badge variant="outline">{isTrial ? `${TRIAL_DAYS} ngày trải nghiệm` : "Gói trả phí"}</Badge>
            </div>
            <ul className="mt-5 grid gap-4 text-sm sm:grid-cols-2">
              {COMMON_PLAN_FEATURES.map((feature) => <li key={feature} className="flex items-start gap-2.5"><Check className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden="true" /><span>{feature}</span></li>)}
              <li className="flex items-start gap-2.5"><Check className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden="true" /><span>{isTrial ? "3 tài khoản nhân viên" : "10 tài khoản nhân viên"}</span></li>
              <li className="flex items-start gap-2.5"><Check className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden="true" /><span>{isTrial ? "10 hợp đồng · 500 MB lưu trữ" : "5 GB lưu trữ tài liệu"}</span></li>
            </ul>
            <div className="mt-5 flex items-start gap-3 border-t border-border pt-4 text-sm" aria-live="polite">
              {isTrial ? <X className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden="true" /> : <Headphones className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden="true" />}
              <div><p className="font-medium">{isTrial ? "Không bao gồm Customer Support" : "Bao gồm Customer Support"}</p><p className="mt-1 text-xs leading-5 text-muted-foreground">Hỗ trợ khách hàng dành cho gói trả phí còn hiệu lực.</p></div>
            </div>
          </section>
          <section className="mt-8" aria-labelledby="pricing-faq">
            <h2 id="pricing-faq" className="mb-2 font-semibold">Một vài điều bạn có thể muốn biết</h2>
            <Accordion type="single" collapsible>
              <AccordionItem value="trial"><AccordionTrigger>Gói dùng thử khác gì gói trả phí?</AccordionTrigger><AccordionContent className="leading-6 text-muted-foreground">Gói dùng thử kéo dài {TRIAL_DAYS} ngày, với hạn mức minh họa 3 tài khoản, 10 hợp đồng và 500 MB lưu trữ. Customer Support chỉ dành cho gói trả phí còn hiệu lực.</AccordionContent></AccordionItem>
              <AccordionItem value="duration"><AccordionTrigger>Gói 3, 6 và 12 tháng khác nhau thế nào?</AccordionTrigger><AccordionContent className="leading-6 text-muted-foreground">Ba lựa chọn có cùng quyền lợi trong bản giới thiệu này, khác nhau về thời hạn và giá. Số tiền mỗi tháng là giá quy đổi để so sánh; tổng dự kiến là chi phí cho cả kỳ.</AccordionContent></AccordionItem>
              <AccordionItem value="purchase"><AccordionTrigger>Tôi có thể mua hoặc kích hoạt gói ngay không?</AccordionTrigger><AccordionContent className="leading-6 text-muted-foreground">Hiện bạn có thể so sánh và chọn gói. Đăng ký tài khoản mới, kích hoạt dùng thử và thanh toán trực tuyến sẽ được mở sau. Đăng nhập chưa làm phát sinh đơn hàng hay chi phí.</AccordionContent></AccordionItem>
            </Accordion>
          </section>
        </div>
        <aside aria-label="Tóm tắt gói đã chọn" className="min-w-0 lg:sticky lg:top-6">
          <PlanSummary selection={selection} session={session} onRetry={retry} />
        </aside>
      </div>
    </div>
  );
}
