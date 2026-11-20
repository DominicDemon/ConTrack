import type { Metadata } from "next";
import { ProfilePage } from "@/components/internal/profile-page";
import { APP_NAME } from "@/lib/application-branding";

export const metadata: Metadata = {
  title: `Hồ sơ của tôi | ${APP_NAME}`,
};

export default function Page() {
  return <ProfilePage />;
}
