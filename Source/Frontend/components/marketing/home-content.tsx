import Link from "next/link";
import {
  ArrowRight,
  Check,
  FileText,
  FolderOpen,
  GitPullRequest,
  ListChecks,
  MessageSquare,
  ShieldCheck,
  Sparkles,
} from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { APP_NAME } from "@/lib/application-branding";
import { getPricingHref, TRIAL_DAYS } from "@/lib/pricing-preview";

const BENEFITS = [
  { icon: FolderOpen, title: "Tập trung hồ sơ", text: "Hợp đồng, phụ lục và các phiên bản được lưu trong cùng một hồ sơ. Dễ tìm lại khi cần." },
  { icon: GitPullRequest, title: "Phối hợp rõ ràng", text: "Phân công trách nhiệm, trao đổi nội dung và theo dõi việc phê duyệt giữa các bộ phận." },
  { icon: ListChecks, title: "Theo dõi đến hoàn tất", text: "Kết nối bàn giao, nghiệm thu và từng đợt thanh toán trong suốt vòng đời hợp đồng." },
];

function WorkspacePreview() {
  return (
    <figure className="min-w-0">
      <Card className="gap-0 overflow-hidden border-border py-0 shadow-xl shadow-primary/5">
        <div className="flex items-center justify-between border-b border-border bg-secondary/60 px-5 py-4">
          <div className="flex items-center gap-2.5 text-sm font-medium">
            <span className="flex size-7 items-center justify-center rounded-md bg-primary/10 text-primary">
              <FolderOpen className="size-4" aria-hidden="true" />
            </span>
            Công ty Minh An
          </div>
          <span className="flex size-8 items-center justify-center rounded-full border border-border bg-card text-xs font-semibold">MA</span>
        </div>
        <CardContent className="space-y-5 p-5 sm:p-6">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 className="font-semibold">Hợp đồng của bạn</h2>
            <Badge variant="secondary" className="font-normal">Không gian làm việc</Badge>
          </div>
          <div className="rounded-lg border border-border bg-background/60 p-4">
            <div className="flex items-start gap-3">
              <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
                <FileText className="size-5" aria-hidden="true" />
              </span>
              <div className="min-w-0 space-y-1">
                <p className="text-sm font-semibold leading-relaxed">Hợp đồng cung cấp phần mềm</p>
                <p className="text-xs leading-relaxed text-muted-foreground">HD-2026-024 · Công ty An Phát</p>
              </div>
            </div>
            <ol aria-label="Tiến trình hợp đồng minh họa" className="mt-5 grid grid-cols-2 gap-2 sm:grid-cols-4">
              {["Soạn thảo", "Phê duyệt", "Chờ ký", "Thực hiện"].map((step, index) => (
                <li key={step} className="space-y-2">
                  <div className={index < 2 ? "h-1 rounded-full bg-primary" : "h-1 rounded-full bg-border"} />
                  <span className="flex items-center gap-1 text-[11px] text-muted-foreground">
                    {index < 2 && <Check className="size-3 text-primary" aria-hidden="true" />}
                    {step}
                  </span>
                </li>
              ))}
            </ol>
          </div>
          <div className="flex gap-3">
            <MessageSquare className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden="true" />
            <div className="space-y-1">
              <p className="text-sm font-medium">Trao đổi ngay trên hợp đồng</p>
              <p className="text-xs leading-relaxed text-muted-foreground">Các góp ý được lưu cùng phiên bản.</p>
            </div>
          </div>
          <div className="flex gap-3">
            <ShieldCheck className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden="true" />
            <div className="space-y-1">
              <p className="text-sm font-medium">Đúng người, đúng quyền</p>
              <p className="text-xs leading-relaxed text-muted-foreground">Phối hợp giữa các bộ phận trong công ty.</p>
            </div>
          </div>
        </CardContent>
      </Card>
      <figcaption className="mt-4 text-center text-xs text-muted-foreground">Minh họa không gian làm việc {APP_NAME} · Dữ liệu mẫu</figcaption>
    </figure>
  );
}

export function HomeContent() {
  return (
    <div className="mx-auto max-w-6xl px-4 sm:px-6 lg:px-8">
      <section className="grid items-center gap-10 py-12 sm:py-16 lg:grid-cols-2 lg:gap-14 lg:py-20" aria-labelledby="home-title">
        <div>
          <Badge variant="outline" className="mb-6 gap-2 border-primary/20 bg-primary/5 px-3 py-1.5 text-primary">
            <Sparkles className="size-3.5" aria-hidden="true" />
            Không gian hợp đồng của doanh nghiệp
          </Badge>
          <h1 id="home-title" className="text-4xl font-bold leading-[1.15] tracking-tight sm:text-5xl lg:text-[3.35rem]">
            Mỗi hợp đồng.<br />Một hành trình<br /><span className="text-primary">được kiểm soát.</span>
          </h1>
          <p className="mt-6 max-w-lg text-base leading-7 text-muted-foreground">
            Từ bản nháp đến nghiệm thu và thanh toán. {APP_NAME} giúp đội ngũ phối hợp, theo dõi tiến độ và lưu trữ hợp đồng tại một nơi.
          </p>
          <div className="mt-8 flex flex-wrap gap-3">
            <Button size="lg" asChild>
              <Link href={getPricingHref({ kind: "trial" })}>Khám phá gói dùng thử <ArrowRight aria-hidden="true" /></Link>
            </Button>
            <Button variant="outline" size="lg" asChild><Link href="/pricing">Xem gói & giá</Link></Button>
          </div>
          <p className="mt-4 text-xs leading-5 text-muted-foreground">Dùng thử {TRIAL_DAYS} ngày · Xem quyền lợi trước khi lựa chọn</p>
        </div>
        <WorkspacePreview />
      </section>
      <section aria-label="Lợi ích của sản phẩm" className="grid gap-8 border-y border-border py-9 md:grid-cols-3 md:gap-7">
        {BENEFITS.map(({ icon: Icon, title, text }) => (
          <div key={title}>
            <span className="mb-4 flex size-10 items-center justify-center rounded-lg bg-primary/10 text-primary"><Icon className="size-5" aria-hidden="true" /></span>
            <h2 className="font-semibold">{title}</h2>
            <p className="mt-2 text-sm leading-6 text-muted-foreground">{text}</p>
          </div>
        ))}
      </section>
      <section className="my-10 flex flex-col gap-5 rounded-xl border border-primary/15 bg-primary/5 p-6 sm:flex-row sm:items-center sm:justify-between sm:p-8" aria-labelledby="home-plans-title">
        <div>
          <h2 id="home-plans-title" className="text-xl font-semibold tracking-tight">Bắt đầu gọn nhẹ. Sẵn sàng cho cả đội ngũ.</h2>
          <p className="mt-2 text-sm leading-6 text-muted-foreground">Tìm hiểu gói dùng thử và các kỳ 3, 6, 12 tháng phù hợp với doanh nghiệp.</p>
        </div>
        <Button variant="outline" className="shrink-0" asChild><Link href="/pricing">Khám phá các gói <ArrowRight aria-hidden="true" /></Link></Button>
      </section>
    </div>
  );
}
