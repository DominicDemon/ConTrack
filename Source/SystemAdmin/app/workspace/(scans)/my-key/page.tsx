import type { Metadata } from "next";
import { MyKeyPage } from "@/components/encryption-keys/my-key-page";
import { APP_NAME } from "@/lib/application-branding";

export const metadata: Metadata = {
  title: `Key của tôi | ${APP_NAME}`,
};

export default function Page() {
  return <MyKeyPage />;
}
