import Link from "next/link";

import { APP_NAME } from "@/lib/application-branding";

export function MarketingFooter() {
  return (
    <footer className="border-t border-border">
      <div className="mx-auto flex max-w-6xl flex-col gap-3 px-4 py-6 text-xs text-muted-foreground sm:flex-row sm:items-center sm:justify-between sm:px-6 lg:px-8">
        <p><span className="font-semibold text-foreground">{APP_NAME}</span> · Quản lý hợp đồng cho doanh nghiệp</p>
        <div className="flex flex-wrap items-center gap-x-5 gap-y-2">
          <Link href="/pricing" className="hover:text-primary hover:underline">Gói dịch vụ</Link>
          <span>Phối hợp tập trung · Phân quyền theo vai trò</span>
        </div>
      </div>
    </footer>
  );
}
