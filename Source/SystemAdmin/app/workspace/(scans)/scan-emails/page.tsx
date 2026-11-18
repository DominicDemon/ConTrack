import type { Metadata } from "next";
import { ScanEmailHistory } from "@/components/signed-scans/scan-email-history";
import { Card, CardContent } from "@/components/ui/card";
import { APP_NAME } from "@/lib/application-branding";

export const metadata: Metadata = {
  title: `Lịch sử gửi email | ${APP_NAME}`,
};

export default function ScanEmailHistoryPage() {
  return (
    <>
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-foreground">Lịch sử gửi email bản scan</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          100 lần gửi gần nhất. Bạn thấy các lần mình gửi; người kiểm tra bản scan và quản trị key thấy tất cả.
        </p>
      </div>
      <Card className="gap-0 border-border bg-card p-0 shadow-sm">
        <CardContent className="p-4">
          <ScanEmailHistory />
        </CardContent>
      </Card>
    </>
  );
}
