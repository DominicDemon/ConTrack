import type { Metadata } from "next";

import { SignupInfoForm } from "@/components/marketing/signup-info";
import { APP_NAME } from "@/lib/application-branding";

export const metadata: Metadata = { title: `${APP_NAME} | Đăng ký – Thông tin tổ chức` };

export default function SignupInfoPage() {
  return <SignupInfoForm />;
}
