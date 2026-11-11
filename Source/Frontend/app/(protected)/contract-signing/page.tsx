"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Check, Clipboard, FileSignature, RefreshCw, X } from "lucide-react";
import { Header } from "@/components/ui/custom/header";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/sonner";
import { formatDateTime } from "@/lib/format-date-time";
import { RBAC_PERMISSIONS } from "@/lib/rbac";
import { PermissionGuard } from "@/components/auth/permission-guard";
import { electronicSigningApi, type DirectorSigningQueueItem } from "@/services/electronic-signing-api";

export default function ContractSigningQueuePage() {
  const [items, setItems] = useState<DirectorSigningQueueItem[]>([]);
  const [selected, setSelected] = useState<DirectorSigningQueueItem | null>(null);
  const [pdfUrl, setPdfUrl] = useState("");
  const [signatureReady, setSignatureReady] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const [rejectReason, setRejectReason] = useState("");
  const [customerLink, setCustomerLink] = useState("");
  const [customerLinkExpiresAt, setCustomerLinkExpiresAt] = useState("");
  const [busy, setBusy] = useState(false);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const drawing = useRef(false);
  const previous = useRef<{ x: number; y: number } | null>(null);

  const loadQueue = useCallback(async () => {
    try {
      const rows = await electronicSigningApi.getDirectorQueue();
      setItems(rows);
      if (selected && !rows.some((row) => row.sessionId === selected.sessionId)) {
        setSelected(null);
        setCustomerLink("");
      }
    } catch {
      toast.error("Không tải được hàng đợi ký.");
    }
  }, [selected]);

  useEffect(() => { void loadQueue(); }, [loadQueue]);
  useEffect(() => () => { if (pdfUrl) URL.revokeObjectURL(pdfUrl); }, [pdfUrl]);

  const openItem = async (item: DirectorSigningQueueItem) => {
    setBusy(true);
    try {
      const blob = await electronicSigningApi.getDirectorPdf(item.sessionId);
      setPdfUrl((old) => { if (old) URL.revokeObjectURL(old); return URL.createObjectURL(blob); });
      setSelected(item);
      setConfirmed(false);
      setCustomerLink("");
      setCustomerLinkExpiresAt("");
      clearSignature();
    } catch {
      toast.error("Không tải được PDF để rà soát.");
    } finally { setBusy(false); }
  };

  const locate = (event: React.PointerEvent<HTMLCanvasElement>) => {
    const rect = event.currentTarget.getBoundingClientRect();
    return { x: (event.clientX - rect.left) * (event.currentTarget.width / rect.width), y: (event.clientY - rect.top) * (event.currentTarget.height / rect.height) };
  };
  const begin = (event: React.PointerEvent<HTMLCanvasElement>) => {
    event.currentTarget.setPointerCapture(event.pointerId);
    drawing.current = true;
    previous.current = locate(event);
  };
  const move = (event: React.PointerEvent<HTMLCanvasElement>) => {
    if (!drawing.current || !previous.current) return;
    const context = event.currentTarget.getContext("2d");
    if (!context) return;
    const next = locate(event);
    context.strokeStyle = "#111827";
    context.lineWidth = 3;
    context.lineCap = "round";
    context.beginPath();
    context.moveTo(previous.current.x, previous.current.y);
    context.lineTo(next.x, next.y);
    context.stroke();
    previous.current = next;
    setSignatureReady(true);
  };
  const end = () => { drawing.current = false; previous.current = null; };
  const clearSignature = () => {
    const canvas = canvasRef.current;
    const context = canvas?.getContext("2d");
    if (canvas && context) context.clearRect(0, 0, canvas.width, canvas.height);
    setSignatureReady(false);
    setConfirmed(false);
  };

  const sign = async () => {
    if (!selected || !canvasRef.current || !signatureReady || !confirmed) return;
    setBusy(true);
    try {
      const result = await electronicSigningApi.signAsDirector(
        selected.sessionId,
        canvasRef.current.toDataURL("image/png").split(",")[1],
      );
      setCustomerLink(`${window.location.origin}${result.customerSigningLink}`);
      setCustomerLinkExpiresAt(result.linkExpiresAt);
      setItems((rows) => rows.filter((item) => item.sessionId !== selected.sessionId));
      toast.success("Đã ký. Link khách hàng chỉ được tạo sau bước này.");
    } catch {
      toast.error("Không thể ký. Phiên có thể đã được xử lý hoặc không còn hợp lệ.");
    } finally { setBusy(false); }
  };

  const reject = async () => {
    if (!selected || rejectReason.trim().length < 3) return;
    setBusy(true);
    try {
      await electronicSigningApi.rejectAsDirector(selected.sessionId, rejectReason.trim());
      setSelected(null);
      setPdfUrl((old) => { if (old) URL.revokeObjectURL(old); return ""; });
      setRejectReason("");
      await loadQueue();
      toast.success("Đã trả hợp đồng kèm lý do.");
    } catch { toast.error("Không thể từ chối phiên ký."); }
    finally { setBusy(false); }
  };

  const copyLink = async () => {
    await navigator.clipboard.writeText(customerLink);
    toast.success("Đã sao chép link khách hàng.");
  };

  return (
    <PermissionGuard permission={RBAC_PERMISSIONS.contractElectronicSigningDirector}>
      <div className="space-y-6">
        <Header title="Ký hợp đồng · Rà soát PDF, ký hoặc trả lại kèm lý do" />
        <div className="grid gap-5 xl:grid-cols-[360px_minmax(0,1fr)]">
          <Card>
            <CardHeader className="flex-row items-center justify-between space-y-0">
              <CardTitle className="text-base">Chờ Giám đốc ký ({items.length})</CardTitle>
              <Button variant="ghost" size="icon" onClick={() => void loadQueue()} aria-label="Tải lại hàng đợi"><RefreshCw className="size-4" /></Button>
            </CardHeader>
            <CardContent className="space-y-2">
              {items.length === 0 && <p className="py-8 text-center text-sm text-muted-foreground">Không có hợp đồng chờ ký.</p>}
              {items.map((item) => (
                <button key={item.sessionId} onClick={() => void openItem(item)} className={`w-full rounded-lg border p-3 text-left transition-colors hover:bg-muted ${selected?.sessionId === item.sessionId ? "border-primary bg-primary/5" : ""}`}>
                  <span className="block font-medium">{item.contractName}</span>
                  <span className="mt-1 block text-xs text-muted-foreground">{item.contractCode || `#${item.contractId}`} · {item.customerName}</span>
                  <span className="mt-1 block text-xs text-muted-foreground">Gửi lúc {formatDateTime(item.submittedAt)}</span>
                </button>
              ))}
            </CardContent>
          </Card>

          <div className="space-y-5">
            {selected ? <>
              <Card>
                <CardHeader><CardTitle className="text-base">{selected.contractName} · Phiên #{selected.sessionId}</CardTitle></CardHeader>
                <CardContent>{pdfUrl && <iframe title="PDF hợp đồng gốc" src={pdfUrl} className="h-[60vh] w-full rounded border" />}</CardContent>
              </Card>
              <Card>
                <CardHeader><CardTitle className="flex items-center gap-2 text-base"><FileSignature className="size-4" /> Chữ ký Giám đốc</CardTitle></CardHeader>
                <CardContent className="space-y-4">
                  <p className="text-sm text-muted-foreground">Vẽ chữ ký sau khi đã rà soát tài liệu. Mỗi xác nhận tạo một PDF đã ký mới.</p>
                  <canvas ref={canvasRef} width={900} height={220} aria-label="Khung vẽ chữ ký Giám đốc" className="h-40 w-full touch-none rounded-md border bg-white" onPointerDown={begin} onPointerMove={move} onPointerUp={end} onPointerCancel={end} />
                  <Button variant="outline" onClick={clearSignature}>Xóa chữ ký</Button>
                  <label className="flex items-start gap-3 text-sm"><input type="checkbox" className="mt-1 size-4" checked={confirmed} onChange={(event) => setConfirmed(event.target.checked)} /><span>Tôi đã kiểm tra PDF và xác nhận chữ ký này cho hợp đồng trên.</span></label>
                  <Button disabled={busy || !signatureReady || !confirmed || Boolean(customerLink)} onClick={() => void sign()}><Check className="mr-2 size-4" />Ký và tạo link khách hàng</Button>
                  {customerLink && <div className="space-y-2 rounded-lg border border-emerald-300 bg-emerald-50 p-3 text-sm"><div className="flex flex-wrap items-center gap-2"><span className="min-w-0 flex-1 break-all">{customerLink}</span><Button size="sm" variant="outline" onClick={() => void copyLink()}><Clipboard className="mr-2 size-4" />Sao chép</Button></div><p className="text-xs text-emerald-900">Link hết hạn: {new Date(customerLinkExpiresAt).toLocaleString("vi-VN")}</p></div>}
                </CardContent>
              </Card>
              <Card>
                <CardHeader><CardTitle className="text-base">Trả lại hợp đồng</CardTitle></CardHeader>
                <CardContent className="space-y-3">
                  <Label htmlFor="director-reason">Lý do (bắt buộc)</Label>
                  <Textarea id="director-reason" maxLength={2000} value={rejectReason} onChange={(event) => setRejectReason(event.target.value)} placeholder="Nêu rõ nội dung cần chỉnh sửa" />
                  <Button variant="destructive" disabled={busy || rejectReason.trim().length < 3} onClick={() => void reject()}><X className="mr-2 size-4" />Từ chối và trả về Sales</Button>
                </CardContent>
              </Card>
            </> : <Card className="flex min-h-64 items-center justify-center"><p className="text-sm text-muted-foreground">Chọn một hợp đồng để xem PDF.</p></Card>}
          </div>
        </div>
      </div>
    </PermissionGuard>
  );
}
