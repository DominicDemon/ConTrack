import type { Metadata } from "next";

import { SignupCart } from "@/components/marketing/signup-cart";
import { APP_NAME } from "@/lib/application-branding";

export const metadata: Metadata = { title: `${APP_NAME} | Đăng ký – Xác nhận` };

export default function SignupCartPage() {
  return <SignupCart />;
}
