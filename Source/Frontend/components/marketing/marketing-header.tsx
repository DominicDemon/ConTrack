"use client";

import Link from "next/link";
import { Suspense } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { ArrowUpRight, Files } from "lucide-react";

import { ThemeToggle } from "@/components/theme-toggle";
import { Button } from "@/components/ui/button";
import { APP_NAME } from "@/lib/application-branding";
import { getPricingHref, getPricingLoginHref, readPlanSelection } from "@/lib/pricing-preview";
import { cn } from "@/lib/utils";

const NAV_LINKS = [
  { href: "/home", label: "Trang chủ" },
  { href: "/pricing", label: "Gói dịch vụ" },
];

function PricingLoginLink() {
  const params = useSearchParams();
  return <Button variant="ghost" size="sm" asChild><Link href={getPricingLoginHref(readPlanSelection(params))}>Đăng nhập</Link></Button>;
}

export function MarketingHeader() {
  const pathname = usePathname();

  return (
    <header className="border-b border-border bg-card/80">
      <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-3 px-4 py-4 sm:gap-6 sm:px-6 lg:px-8">
        <Link href="/home" aria-label={`${APP_NAME} — Trang chủ`} className="flex items-center gap-2.5 rounded-md focus-visible:outline-2 focus-visible:outline-ring">
          <span className="flex size-9 items-center justify-center rounded-lg bg-primary text-primary-foreground">
            <Files className="size-5" aria-hidden="true" />
          </span>
          <span className="text-xl font-bold tracking-tight">{APP_NAME}</span>
        </Link>
        <nav aria-label="Điều hướng chính" className="order-3 flex w-full gap-6 border-t border-border pt-3 text-sm sm:order-none sm:w-auto sm:border-0 sm:pt-0">
          {NAV_LINKS.map(({ href, label }) => (
            <Link
              key={href}
              href={href}
              aria-current={pathname === href ? "page" : undefined}
              className={cn(
                "rounded-sm py-1 font-medium transition-colors hover:text-primary focus-visible:outline-2 focus-visible:outline-ring",
                pathname === href ? "text-primary" : "text-muted-foreground",
              )}
            >
              {label}
            </Link>
          ))}
        </nav>
        <div className="ml-auto flex items-center gap-1.5 sm:gap-3">
          <ThemeToggle />
          {pathname === "/pricing" ? (
            <Suspense fallback={<Button variant="ghost" size="sm" disabled>Đăng nhập</Button>}>
              <PricingLoginLink />
            </Suspense>
          ) : (
            <Button variant="ghost" size="sm" asChild><Link href="/">Đăng nhập</Link></Button>
          )}
          <Button className="hidden md:inline-flex" asChild>
            <Link href={getPricingHref({ kind: "trial" })}>
              Dùng thử miễn phí <ArrowUpRight aria-hidden="true" />
            </Link>
          </Button>
        </div>
      </div>
    </header>
  );
}
