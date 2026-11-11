"use client";

import { useEffect, useState } from "react";
import { ArrowRight, Eye, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "@/components/ui/sonner";
import { electronicSigningApi } from "@/services/electronic-signing-api";
import { contractApi } from "@/services/contract-api";
import { downloadBlob } from "@/components/contract-templates/contract-template-utils";

interface Props {
  contractId: number;
  versionId: number;
  versionLocked: boolean;
  canInitiate: boolean;
  customerName: string;
  customerPhone: string;
}

const getSessionLabel = (status: number) => ({
  1: "Chờ Giám đốc ký",
  4: "Chờ khách xác thực OTP",
  5: "Chờ khách ký",
  7: "Phiên đã hết hạn",
  8: "Phiên đã hủy",
}[status] ?? "Đang xử lý");

export function ElectronicSigningStartPanel({ contractId, versionId, versionLocked, canInitiate, customerName, customerPhone }: Props) {
  const [directorName, setDirectorName] = useState("");
  const [directorPhone, setDirectorPhone] = useState("");
  const [customerPhoneValue, setCustomerPhoneValue] = useState(customerPhone);
  const [customerSignerName, setCustomerSignerName] = useState(customerName);
  const [directorPage, setDirectorPage] = useState(1);
  const [directorX, setDirectorX] = useState(360);
  const [directorY, setDirectorY] = useState(700);
  const [customerPage, setCustomerPage] = useState(1);
  const [customerX, setCustomerX] = useState(80);
  const [customerY, setCustomerY] = useState(700);
  const [busy, setBusy] = useState(false);
  const [sessionStatus, setSessionStatus] = useState<number | null>(null);
  const [rejectionReason, setRejectionReason] = useState<string | null>(null);
  const [previewUrl, setPreviewUrl] = useState("");

  useEffect(() => () => { if (previewUrl) URL.revokeObjectURL(previewUrl); }, [previewUrl]);

  useEffect(() => {
    let active = true;
    void electronicSigningApi.getStatus(contractId, versionId)
      .then((result) => {
        if (!active) return;
        setSessionStatus(result?.status ?? null);
        setRejectionReason(result?.rejectionReason ?? null);
      })
      .catch(() => { if (active) setSessionStatus(null); });
    return () => { active = false; };
  }, [contractId, versionId]);

  if (!canInitiate) return null;

  const start = async () => {
    if (!versionLocked) {
      toast.error("Chỉ có thể gửi ký sau khi đã chốt và khóa version hợp đồng.");
      return;
    }
    if (!directorName.trim() || !directorPhone.trim() || !customerSignerName.trim() || !customerPhoneValue.trim()) {
      toast.error("Nhập tên và số điện thoại của cả hai người ký.");
      return;
    }
    if (!window.confirm("Gửi version đã khóa tới Giám đốc để ký? Version này sẽ không thể chỉnh sửa.")) return;
    setBusy(true);
    try {
      const result = await electronicSigningApi.start({
        contractId,
        versionId,
        directorName: directorName.trim(),
        directorPhone: directorPhone.trim(),
        customerName: customerSignerName.trim(),
        customerPhone: customerPhoneValue.trim(),
        linkLifetimeHours: 72,
        directorPlacement: { pageNumber: directorPage, x: directorX, y: directorY, width: 150, height: 50 },
        customerPlacement: { pageNumber: customerPage, x: customerX, y: customerY, width: 150, height: 50 },
      });
      setSessionStatus(1);
      toast.success(`Đã gửi phiên #${result.sessionId} vào hàng đợi Giám đốc.`);
    } catch {
      toast.error("Không thể tạo phiên ký. Hãy kiểm tra version, quyền và trạng thái hợp đồng.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card className="mb-5 border-primary/20">
      <CardHeader>
        <CardTitle className="text-base">Gửi ký điện tử</CardTitle>
        <CardDescription>Gửi PDF của version hiện tại đã khóa tới Giám đốc. Khách hàng chỉ nhận link sau khi Giám đốc ký.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        {sessionStatus !== null && sessionStatus !== 2 && sessionStatus !== 6 && (
          <p className="rounded-md border bg-muted p-3 text-sm">Version này đã có phiên ký: {getSessionLabel(sessionStatus)}.</p>
        )}
        {sessionStatus === 2 && (
          <div className="space-y-2 rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-950">
            <p className="font-medium">Giám đốc đã trả version này.</p>
            {rejectionReason && <p>{rejectionReason}</p>}
            <p>Tạo vòng đàm phán/version mới từ mục “Vòng đàm phán”, chỉnh sửa version mới rồi gửi lại.</p>
          </div>
        )}
        {sessionStatus === 6 && (
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-emerald-300 bg-emerald-50 p-3 text-sm text-emerald-950">
            <span>Hai bên đã ký version này.</span>
            <Button variant="outline" onClick={async () => {
              try {
                const pdf = await electronicSigningApi.getFinalPdf(contractId, versionId);
                downloadBlob(pdf, `contract-${contractId}-version-${versionId}-signed.pdf`);
              } catch { toast.error("Không tải được PDF đã ký."); }
            }}>Tải PDF cuối</Button>
          </div>
        )}
        {!versionLocked && <p className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">Version chưa khóa. Hoàn tất chốt hợp đồng trước khi gửi ký.</p>}
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <div className="space-y-2"><Label htmlFor="sign-director-name">Tên Giám đốc</Label><Input id="sign-director-name" value={directorName} onChange={(event) => setDirectorName(event.target.value)} maxLength={300} /></div>
          <div className="space-y-2"><Label htmlFor="sign-director-phone">Điện thoại Giám đốc</Label><Input id="sign-director-phone" value={directorPhone} onChange={(event) => setDirectorPhone(event.target.value)} maxLength={30} /></div>
          <div className="space-y-2"><Label htmlFor="sign-customer-name">Tên người đại diện khách hàng</Label><Input id="sign-customer-name" value={customerSignerName} onChange={(event) => setCustomerSignerName(event.target.value)} maxLength={300} /></div>
          <div className="space-y-2"><Label htmlFor="sign-customer-phone">Điện thoại nhận OTP của khách</Label><Input id="sign-customer-phone" value={customerPhoneValue} onChange={(event) => setCustomerPhoneValue(event.target.value)} maxLength={30} /></div>
        </div>
        <div className="grid gap-5 lg:grid-cols-2">
          <fieldset className="grid grid-cols-3 gap-3 rounded-lg border p-4">
            <legend className="px-1 text-sm font-medium">Vị trí chữ ký Giám đốc (điểm PDF)</legend>
            <div className="space-y-1"><Label htmlFor="director-page">Trang</Label><Input id="director-page" type="number" min={1} value={directorPage} onChange={(e) => setDirectorPage(Number(e.target.value))} /></div>
            <div className="space-y-1"><Label htmlFor="director-x">X</Label><Input id="director-x" type="number" min={0} value={directorX} onChange={(e) => setDirectorX(Number(e.target.value))} /></div>
            <div className="space-y-1"><Label htmlFor="director-y">Y</Label><Input id="director-y" type="number" min={0} value={directorY} onChange={(e) => setDirectorY(Number(e.target.value))} /></div>
          </fieldset>
          <fieldset className="grid grid-cols-3 gap-3 rounded-lg border p-4">
            <legend className="px-1 text-sm font-medium">Vị trí chữ ký khách hàng (điểm PDF)</legend>
            <div className="space-y-1"><Label htmlFor="customer-page">Trang</Label><Input id="customer-page" type="number" min={1} value={customerPage} onChange={(e) => setCustomerPage(Number(e.target.value))} /></div>
            <div className="space-y-1"><Label htmlFor="customer-x">X</Label><Input id="customer-x" type="number" min={0} value={customerX} onChange={(e) => setCustomerX(Number(e.target.value))} /></div>
            <div className="space-y-1"><Label htmlFor="customer-y">Y</Label><Input id="customer-y" type="number" min={0} value={customerY} onChange={(e) => setCustomerY(Number(e.target.value))} /></div>
          </fieldset>
        </div>
        <div className="space-y-3">
          <Button variant="outline" disabled={busy} onClick={async () => {
            try {
              const blob = await contractApi.downloadPreviewPdf(contractId);
              setPreviewUrl((old) => { if (old) URL.revokeObjectURL(old); return URL.createObjectURL(blob); });
            } catch { toast.error("Không tạo được PDF xem trước."); }
          }}><Eye className="mr-2 size-4" />Xem PDF trước khi gửi</Button>
          {previewUrl && <iframe title="PDF xem trước" src={previewUrl} className="h-[65vh] w-full rounded-md border" />}
        </div>
        <Button onClick={() => void start()} disabled={busy || !versionLocked || sessionStatus !== null}>
          {busy ? <Loader2 className="mr-2 size-4 animate-spin" /> : <ArrowRight className="mr-2 size-4" />}
          Gửi Giám đốc ký
        </Button>
      </CardContent>
    </Card>
  );
}
