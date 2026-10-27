import type { Metadata } from "next";
import { Suspense } from "react";

import { SignupResult } from "@/components/marketing/signup-result";
import { APP_NAME } from "@/lib/application-branding";

export const metadata: Metadata = { title: `${APP_NAME} | Đăng ký – Kết quả`, referrer: "no-referrer" };

export default function SignupResultPage() {
  return (
    <Suspense fallback={<p role="status" className="px-6 py-20 text-center text-muted-foreground">Đang tải...</p>}>
      <SignupResult />
    </Suspense>
  );
}
