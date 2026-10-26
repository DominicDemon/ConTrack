import type { Metadata } from "next";
import { Suspense } from "react";

import { PricingContent } from "@/components/marketing/pricing-content";
import { APP_NAME } from "@/lib/application-branding";

export const metadata: Metadata = {
  title: `${APP_NAME} | Gói dịch vụ`,
  description: "So sánh gói dùng thử 15 ngày và các kỳ sử dụng 3, 6, 12 tháng.",
};

export default function PricingPage() {
  return (
    <Suspense fallback={<p role="status" className="px-6 py-20 text-center text-muted-foreground">Đang tải các gói dịch vụ...</p>}>
      <PricingContent />
    </Suspense>
  );
}
