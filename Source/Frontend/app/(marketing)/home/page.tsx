import type { Metadata } from "next";

import { HomeContent } from "@/components/marketing/home-content";
import { APP_NAME } from "@/lib/application-branding";

export const metadata: Metadata = {
  title: `${APP_NAME} | Không gian hợp đồng của doanh nghiệp`,
  description: "Tập trung hồ sơ, phối hợp phê duyệt và theo dõi vòng đời hợp đồng của doanh nghiệp.",
};

export default function HomePage() {
  return <HomeContent />;
}
