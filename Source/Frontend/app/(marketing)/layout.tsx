import type { ReactNode } from "react";

import { MarketingFooter } from "@/components/marketing/marketing-footer";
import { MarketingHeader } from "@/components/marketing/marketing-header";

export default function MarketingLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col bg-background text-foreground">
      <a
        href="#marketing-content"
        className="sr-only z-50 rounded-md bg-primary p-3 text-primary-foreground focus:not-sr-only focus:fixed focus:left-4 focus:top-4"
      >
        Đi đến nội dung chính
      </a>
      <MarketingHeader />
      <main id="marketing-content" tabIndex={-1} className="min-w-0 flex-1 outline-none">
        {children}
      </main>
      <MarketingFooter />
    </div>
  );
}
