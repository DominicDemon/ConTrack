import type { Metadata } from "next";
import { ScanSearch } from "@/components/signed-scans/scan-search";
import { Card, CardContent } from "@/components/ui/card";
import { APP_NAME } from "@/lib/application-branding";

export const metadata: Metadata = {
  title: `Tìm hợp đồng | ${APP_NAME}`,
};

export default function SignedScanSearchPage() {
  return (
    <>
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-foreground">Tìm hợp đồng</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Tìm theo mã số thuế, số điện thoại, số hợp đồng hoặc tên khách hàng đọc được trên bản ký giấy và hợp đồng giấy scan
          (không cần gõ dấu).
        </p>
      </div>
      <Card className="gap-0 border-border bg-card p-0 shadow-sm">
        <CardContent className="p-4">
          <ScanSearch />
        </CardContent>
      </Card>
    </>
  );
}
