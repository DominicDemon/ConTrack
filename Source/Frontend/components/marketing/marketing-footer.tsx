import Link from "next/link";
import { Building2, Files, Mail, MapPin, Phone } from "lucide-react";

import { APP_NAME } from "@/lib/application-branding";

export function MarketingFooter() {
  return (
    <footer className="border-t border-border bg-card text-muted-foreground">
      <div className="mx-auto grid max-w-6xl gap-8 px-4 py-10 text-sm leading-6 sm:grid-cols-2 sm:px-6 lg:grid-cols-[1.2fr_1fr_1fr_1.3fr] lg:gap-8 lg:px-8 lg:py-12">
        <section aria-labelledby="footer-brand">
          <Link href="/home" className="inline-flex items-center gap-2.5 rounded-md text-foreground focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring">
            <span className="flex size-9 items-center justify-center rounded-lg bg-primary"><Files className="size-5 text-primary-foreground" aria-hidden="true" /></span>
            <span id="footer-brand" className="text-xl font-bold tracking-tight">{APP_NAME}</span>
          </Link>
          <p className="mt-4 font-medium text-foreground">Đồng hành cùng doanh nghiệp trong từng hợp đồng.</p>
          <p className="mt-2">Quản lý hợp đồng tập trung, kết nối đội ngũ và theo dõi tiến độ trong suốt vòng đời hợp đồng.</p>
          <div className="mt-6 border-t border-border pt-5">
            <p className="flex items-center gap-2 font-semibold text-foreground"><Building2 className="size-4 text-primary" aria-hidden="true" />Một sản phẩm của DTC</p>
            <p className="mt-2 text-xs leading-6">Giải pháp phần mềm hỗ trợ quản lý và phối hợp công việc trên nền tảng Web.</p>
          </div>
        </section>
        <nav aria-labelledby="footer-product">
          <h2 id="footer-product" className="mb-4 text-sm font-semibold text-foreground">Thông tin cần biết</h2>
          <ul className="space-y-2.5">
            <li><FooterLink href="/home">Giới thiệu sản phẩm</FooterLink></li>
            <li><FooterLink href="/pricing">Gói dịch vụ &amp; bảng giá</FooterLink></li>
            <li><FooterLink href="/pricing?plan=trial">Khám phá gói dùng thử</FooterLink></li>
          </ul>
        </nav>
        <nav aria-labelledby="footer-support">
          <h2 id="footer-support" className="mb-4 text-sm font-semibold text-foreground">Hỗ trợ</h2>
          <ul className="space-y-2.5">
            <li><FooterLink href="/pricing#pricing-faq">Câu hỏi thường gặp</FooterLink></li>
            <li><FooterLink href="/">Đăng nhập hệ thống</FooterLink></li>
            <li><FooterLink href="/forgot-password">Khôi phục mật khẩu</FooterLink></li>
          </ul>
          <p className="mt-4 text-xs leading-6">Hỗ trợ khách hàng dành cho gói trả phí còn hiệu lực.</p>
        </nav>
        <section aria-labelledby="footer-contact">
          <h2 id="footer-contact" className="mb-4 text-sm font-semibold text-foreground">Liên hệ</h2>
          <address className="space-y-4 not-italic">
            <p className="flex items-start gap-3"><Phone className="mt-1.5 size-4 shrink-0 text-primary" aria-hidden="true" /><span><span className="font-medium text-foreground">Hotline: </span><a href="tel:02435573636" className="rounded-sm hover:text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring">024 3557.3636</a></span></p>
            <p className="flex items-start gap-3"><Mail className="mt-1.5 size-4 shrink-0 text-primary" aria-hidden="true" /><span className="min-w-0"><span className="font-medium text-foreground">E-mail: </span><a href="mailto:info@dtctech.vn" className="rounded-sm break-words hover:text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring">info@dtctech.vn</a></span></p>
            <p className="flex items-start gap-3"><MapPin className="mt-1.5 size-4 shrink-0 text-primary" aria-hidden="true" /><span><span className="font-medium text-foreground">Địa chỉ: </span>Tầng 2, Số 14 Pháo Đài Láng, Đống Đa, Hà Nội</span></p>
          </address>
          <ul aria-label="Mạng xã hội" className="mt-4 flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
            <li>Google plus</li><li>Facebook</li><li>Twitter</li>
          </ul>
        </section>
      </div>
      <div className="border-t border-border">
        <div className="mx-auto flex max-w-6xl flex-col gap-2 px-4 py-5 text-xs leading-6 text-muted-foreground sm:flex-row sm:items-center sm:justify-between sm:px-6 lg:px-8">
          <p>© {new Date().getFullYear()} DTC. All Rights Reserved.</p>
          <p>{APP_NAME} · Quản lý hợp đồng cho doanh nghiệp</p>
        </div>
      </div>
    </footer>
  );
}

interface FooterLinkProps {
  href: string;
  children: React.ReactNode;
}

function FooterLink({ href, children }: FooterLinkProps) {
  return <Link href={href} className="rounded-sm transition-colors hover:text-primary hover:underline hover:underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring">{children}</Link>;
}
