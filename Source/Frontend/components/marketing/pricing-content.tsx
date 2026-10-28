"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Check, Gift, Headphones, Loader2, RefreshCw, X } from "lucide-react";

import { PlanCard } from "@/components/marketing/plan-card";
import { PlanSummary } from "@/components/marketing/plan-summary";
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from "@/components/ui/accordion";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { COMMON_PLAN_FEATURES, getPricingHref, readPlanSelection, type PlanSelection } from "@/lib/pricing-preview";
import { getTrialPlan, readSignupDraft, resolveSignupPlan, toPaidPlanCards, writeSignupDraft } from "@/lib/signup-draft";
import { subscriptionApi, type PublicPlan } from "@/services/subscription-api";

type PlansState = { status: "loading" } | { status: "error" } | { status: "ready"; plans: PublicPlan[] };

export function PricingContent() {
  const router = useRouter();
  const params = useSearchParams();
  const selection = readPlanSelection(params);
  const isTrial = selection.kind === "trial";
  const [state, setState] = useState<PlansState>({ status: "loading" });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    subscriptionApi.getPlans(controller.signal)
      .then((plans) => setState({ status: "ready", plans }))
      .catch(() => { if (!controller.signal.aborted) setState({ status: "error" }); });
    return () => controller.abort();
  }, [attempt]);

  const plans = state.status === "ready" ? state.plans : [];
  const trial = getTrialPlan(plans);
  const trialDays = trial?.versions[0]?.durationDays ?? 0;
  const paidPlan = plans.find((plan) => plan.isPaid) ?? null;
  const cards = toPaidPlanCards(plans);
  const selectedPlan = resolveSignupPlan(plans, selection);
  const selectedCard = selection.kind === "paid" ? cards.find((card) => card.months === selection.months) ?? null : null;
  const limits = isTrial ? trial : paidPlan;

  const handleSelect = (next: PlanSelection) => {
    router.replace(getPricingHref(next), { scroll: false });
  };

  const handleContinue = useCallback(() => {
    if (!selectedPlan) return;
    const previous = readSignupDraft();
    // Giữ thông tin đã nhập nếu người dùng quay lại đổi gói; voucher tính theo gói nên bỏ.
    writeSignupDraft({ plan: selectedPlan, info: previous?.info });
    router.push("/signup/info");
  }, [router, selectedPlan]);

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6 sm:py-12 lg:px-8">
      <header className="mb-8 max-w-2xl">
        <p className="mb-3 text-xs font-semibold tracking-widest text-primary uppercase">Gói dịch vụ</p>
        <h1 className="text-3xl font-bold leading-tight tracking-tight sm:text-4xl">Một gói đầy đủ.<br className="sm:hidden" /> Thời hạn linh hoạt.</h1>
        <p className="mt-4 text-sm leading-6 text-muted-foreground sm:text-base">Chọn thời hạn phù hợp. Đồng hành lâu hơn, tiết kiệm hơn.</p>
      </header>
      {state.status === "loading" && (
        <p role="status" className="flex items-center gap-2 py-20 text-sm text-muted-foreground"><Loader2 className="size-4 animate-spin" aria-hidden="true" />Đang tải các gói dịch vụ...</p>
      )}
      {state.status === "error" && (
        <Alert variant="destructive" className="max-w-xl">
          <AlertTitle>Không tải được danh sách gói</AlertTitle>
          <AlertDescription>
            <p>Vui lòng kiểm tra kết nối và thử lại.</p>
            <Button type="button" variant="outline" size="sm" className="mt-3" onClick={() => { setState({ status: "loading" }); setAttempt((value) => value + 1); }}><RefreshCw aria-hidden="true" />Thử lại</Button>
          </AlertDescription>
        </Alert>
      )}
      {state.status === "ready" && (
        <div className="grid items-start gap-8 lg:grid-cols-[minmax(0,1fr)_320px]">
          <div className="min-w-0">
            <fieldset className="min-w-0">
              <legend className="mb-4 flex items-center gap-2.5 font-semibold"><span className="flex size-6 items-center justify-center rounded-full bg-primary/10 text-xs text-primary">1</span>Chọn gói sử dụng</legend>
              {trial && (
                <label className="mb-6 block cursor-pointer">
                  <input type="radio" name="pricing-plan" value="trial" checked={isTrial} onChange={() => handleSelect({ kind: "trial" })} aria-label={`Dùng thử ${trialDays} ngày`} className="peer sr-only" />
                  <span className="flex flex-wrap items-center gap-3 rounded-xl border border-border bg-card p-4 transition-colors hover:border-primary/50 peer-checked:border-primary peer-checked:bg-primary/5 peer-checked:ring-1 peer-checked:ring-primary peer-focus-visible:outline-2 peer-focus-visible:outline-offset-4 peer-focus-visible:outline-ring sm:p-5">
                    <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"><Gift className="size-5" aria-hidden="true" /></span>
                    <span className="min-w-0 flex-1 space-y-1">
                      <span className="block text-sm font-semibold">Chưa sẵn sàng? Dùng thử {trialDays} ngày.</span>
                      <span className="block text-xs leading-5 text-muted-foreground">{trial.planDescription ?? "Trải nghiệm quy trình chính"}</span>
                    </span>
                    <Badge variant={isTrial ? "default" : "secondary"}>{isTrial ? "Đã chọn dùng thử" : "Miễn phí"}</Badge>
                  </span>
                </label>
              )}
              <div className="grid gap-3 sm:grid-cols-3">
                {cards.map((plan) => (
                  <PlanCard key={plan.months} plan={plan} selected={selection.kind === "paid" && selection.months === plan.months} onSelect={() => handleSelect({ kind: "paid", months: plan.months })} />
                ))}
              </div>
            </fieldset>
            <section aria-labelledby="plan-benefits" className="mt-7 rounded-xl border border-border bg-card p-5 sm:p-6">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <h2 id="plan-benefits" className="font-semibold">{isTrial ? "Quyền lợi gói dùng thử" : "Mọi thời hạn đều bao gồm"}</h2>
                <Badge variant="outline">{isTrial ? `${trialDays} ngày trải nghiệm` : "Gói trả phí"}</Badge>
              </div>
              <ul className="mt-5 grid gap-4 text-sm sm:grid-cols-2">
                {COMMON_PLAN_FEATURES.map((feature) => <li key={feature} className="flex items-start gap-2.5"><Check className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden="true" /><span>{feature}</span></li>)}
                {limits && (
                  <>
                    <li className="flex items-start gap-2.5"><Check className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden="true" /><span>{limits.maxUsers} tài khoản nhân viên</span></li>
                    <li className="flex items-start gap-2.5"><Check className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden="true" /><span>{limits.maxContracts} hợp đồng · {limits.maxScanPages} trang scan</span></li>
                  </>
                )}
              </ul>
              <div className="mt-5 flex items-start gap-3 border-t border-border pt-4 text-sm" aria-live="polite">
                {limits?.allowsSupportTickets ? <Headphones className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden="true" /> : <X className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden="true" />}
                <div><p className="font-medium">{limits?.allowsSupportTickets ? "Bao gồm Customer Support" : "Không bao gồm Customer Support"}</p><p className="mt-1 text-xs leading-5 text-muted-foreground">Hỗ trợ khách hàng dành cho gói trả phí còn hiệu lực.</p></div>
              </div>
            </section>
            <section className="mt-8" aria-labelledby="pricing-faq">
              <h2 id="pricing-faq" className="mb-2 font-semibold">Một vài điều bạn có thể muốn biết</h2>
              <Accordion type="single" collapsible>
                <AccordionItem value="trial"><AccordionTrigger>Gói dùng thử khác gì gói trả phí?</AccordionTrigger><AccordionContent className="leading-6 text-muted-foreground">Gói dùng thử kéo dài {trialDays} ngày, miễn phí và mỗi email chỉ dùng được một lần. Customer Support chỉ dành cho gói trả phí còn hiệu lực.</AccordionContent></AccordionItem>
                <AccordionItem value="duration"><AccordionTrigger>Các thời hạn khác nhau thế nào?</AccordionTrigger><AccordionContent className="leading-6 text-muted-foreground">Các lựa chọn có cùng quyền lợi, khác nhau về thời hạn và giá. Số tiền mỗi tháng là giá quy đổi để so sánh; tổng là chi phí cho cả kỳ.</AccordionContent></AccordionItem>
                <AccordionItem value="purchase"><AccordionTrigger>Sau khi thanh toán tôi nhận tài khoản thế nào?</AccordionTrigger><AccordionContent className="leading-6 text-muted-foreground">Hệ thống tự tạo không gian làm việc cho tổ chức của bạn và gửi mã tổ chức, tên đăng nhập và mật khẩu tạm tới email đã đăng ký, thường trong vòng một phút.</AccordionContent></AccordionItem>
              </Accordion>
            </section>
          </div>
          <aside aria-label="Tóm tắt gói đã chọn" className="min-w-0 lg:sticky lg:top-6">
            <PlanSummary plan={selectedPlan} card={selectedCard} onContinue={handleContinue} />
          </aside>
        </div>
      )}
    </div>
  );
}
