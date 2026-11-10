"use client";

import { useEffect, useRef, useState } from "react";
import { useParams } from "next/navigation";
import { toast } from "@/components/ui/sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { electronicSigningPublicApi } from "@/services/electronic-signing-public-api";

type SigningStep = "request" | "verify" | "review" | "complete";

export default function PublicElectronicSigningPage() {
  const { tenantCode, linkToken } = useParams<{ tenantCode: string; linkToken: string }>();
  const [step, setStep] = useState<SigningStep>("request");
  const [challengeId, setChallengeId] = useState("");
  const [otp, setOtp] = useState("");
  const [pdfUrl, setPdfUrl] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const [busy, setBusy] = useState(false);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const drawingRef = useRef(false);
  const lastPointRef = useRef<{ x: number; y: number } | null>(null);

  useEffect(() => () => {
    if (pdfUrl) URL.revokeObjectURL(pdfUrl);
  }, [pdfUrl]);

  const requestOtp = async () => {
    setBusy(true);
    try {
      const result = await electronicSigningPublicApi.requestOtp(tenantCode, linkToken);
      setChallengeId(result.challengeId);
      setStep("verify");
      toast.info("Nếu đường dẫn còn hiệu lực, mã xác minh sẽ được gửi đến số đã đăng ký.");
    } catch {
      toast.error("Không thể yêu cầu OTP. Hãy kiểm tra đường dẫn hoặc liên hệ công ty.");
    } finally {
      setBusy(false);
    }
  };

  const verifyOtp = async () => {
    if (!challengeId || !/^\d{6}$/.test(otp)) {
      toast.error("Nhập mã OTP gồm 6 chữ số.");
      return;
    }
    setBusy(true);
    try {
      await electronicSigningPublicApi.verifyOtp(tenantCode, linkToken, challengeId, otp);
      const pdf = await electronicSigningPublicApi.getPdf(tenantCode, linkToken, challengeId);
      setPdfUrl(URL.createObjectURL(pdf));
      setStep("review");
    } catch {
      toast.error("OTP sai, đã hết hạn hoặc hợp đồng không còn chờ ký.");
    } finally {
      setBusy(false);
    }
  };

  const point = (event: React.PointerEvent<HTMLCanvasElement>) => {
    const canvas = event.currentTarget;
    const bounds = canvas.getBoundingClientRect();
    return {
      x: (event.clientX - bounds.left) * (canvas.width / bounds.width),
      y: (event.clientY - bounds.top) * (canvas.height / bounds.height),
    };
  };

  const startDrawing = (event: React.PointerEvent<HTMLCanvasElement>) => {
    const canvas = event.currentTarget;
    canvas.setPointerCapture(event.pointerId);
    drawingRef.current = true;
    lastPointRef.current = point(event);
  };

  const draw = (event: React.PointerEvent<HTMLCanvasElement>) => {
    if (!drawingRef.current || !lastPointRef.current) return;
    const canvas = event.currentTarget;
    const context = canvas.getContext("2d");
    if (!context) return;
    const next = point(event);
    context.strokeStyle = "#111827";
    context.lineWidth = 3;
    context.lineCap = "round";
    context.lineJoin = "round";
    context.beginPath();
    context.moveTo(lastPointRef.current.x, lastPointRef.current.y);
    context.lineTo(next.x, next.y);
    context.stroke();
    lastPointRef.current = next;
  };

  const stopDrawing = () => {
    drawingRef.current = false;
    lastPointRef.current = null;
  };

  const clearSignature = () => {
    const canvas = canvasRef.current;
    const context = canvas?.getContext("2d");
    if (canvas && context) context.clearRect(0, 0, canvas.width, canvas.height);
    setConfirmed(false);
  };

  const sign = async () => {
    const canvas = canvasRef.current;
    if (!canvas || !challengeId || !confirmed) {
      toast.error("Vui lòng ký tên và xác nhận đồng ý ký hợp đồng.");
      return;
    }
    const context = canvas.getContext("2d");
    const hasInk = context?.getImageData(0, 0, canvas.width, canvas.height).data.some(
      (value, index) => index % 4 === 3 && value > 0,
    ) ?? false;
    if (!hasInk) {
      toast.error("Vui lòng vẽ chữ ký trước khi xác nhận.");
      return;
    }
    setBusy(true);
    try {
      const finalPdf = await electronicSigningPublicApi.sign(tenantCode, linkToken, {
        challengeId,
        signaturePngBase64: canvas.toDataURL("image/png").split(",")[1],
        confirmed,
      });
      const downloadUrl = URL.createObjectURL(finalPdf);
      const downloadLink = document.createElement("a");
      downloadLink.href = downloadUrl;
      downloadLink.download = "signed-contract.pdf";
      downloadLink.click();
      URL.revokeObjectURL(downloadUrl);
      setStep("complete");
      setPdfUrl((current) => {
        if (current) URL.revokeObjectURL(current);
        return "";
      });
    } catch {
      toast.error("Không thể hoàn tất chữ ký. Đường dẫn có thể đã hết hạn hoặc đã được sử dụng.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <main className="mx-auto flex min-h-screen w-full max-w-5xl flex-col gap-6 px-4 py-10 sm:px-8">
      <meta name="referrer" content="no-referrer" />
      <header className="space-y-2">
        <p className="text-sm font-medium uppercase tracking-[0.16em] text-primary">DTC · Ký hợp đồng</p>
        <h1 className="text-3xl font-semibold tracking-tight">Xác minh và ký điện tử</h1>
        <p className="max-w-2xl text-sm leading-6 text-muted-foreground">
          Mã OTP được gửi đến số điện thoại đã đăng ký. Hãy đọc bản PDF đã được Giám đốc ký trước khi ký.
        </p>
      </header>

      {step === "request" && (
        <section className="max-w-xl space-y-4 rounded-xl border bg-card p-6">
          <h2 className="text-lg font-semibold">Xác minh người ký</h2>
          <p className="text-sm text-muted-foreground">Yêu cầu mã xác minh một lần để tiếp tục.</p>
          <Button onClick={() => void requestOtp()} disabled={busy}>{busy ? "Đang gửi yêu cầu…" : "Gửi mã OTP"}</Button>
        </section>
      )}

      {step === "verify" && (
        <section className="max-w-xl space-y-4 rounded-xl border bg-card p-6">
          <Label htmlFor="signing-otp">Mã OTP 6 chữ số</Label>
          <Input id="signing-otp" inputMode="numeric" autoComplete="one-time-code" maxLength={6} value={otp} onChange={(event) => setOtp(event.target.value.replace(/\D/g, "").slice(0, 6))} />
          <Button onClick={() => void verifyOtp()} disabled={busy || otp.length !== 6}>{busy ? "Đang xác minh…" : "Xác minh và xem PDF"}</Button>
        </section>
      )}

      {step === "review" && (
        <>
          <section className="overflow-hidden rounded-xl border bg-card">
            <div className="border-b px-5 py-3">
              <h2 className="font-semibold">Hợp đồng đã được Giám đốc ký</h2>
              <p className="text-sm text-muted-foreground">Đọc toàn bộ tài liệu trước khi ký.</p>
            </div>
            {pdfUrl && <iframe title="PDF hợp đồng" src={pdfUrl} className="h-[70vh] w-full" />}
          </section>
          <section className="space-y-4 rounded-xl border bg-card p-5">
            <div>
              <h2 className="font-semibold">Chữ ký của bạn</h2>
              <p className="text-sm text-muted-foreground">Vẽ chữ ký trong khung. Hệ thống sẽ gắn chữ ký vào bản PDF.</p>
            </div>
            <canvas ref={canvasRef} width={900} height={220} aria-label="Khung vẽ chữ ký" className="h-44 w-full touch-none rounded-md border bg-white" onPointerDown={startDrawing} onPointerMove={draw} onPointerUp={stopDrawing} onPointerCancel={stopDrawing} />
            <Button type="button" variant="outline" onClick={clearSignature}>Xóa chữ ký</Button>
            <label className="flex cursor-pointer items-start gap-3 text-sm leading-6">
              <input type="checkbox" checked={confirmed} onChange={(event) => setConfirmed(event.target.checked)} className="mt-1 size-4" />
              <span>Tôi đã đọc hợp đồng và xác nhận chữ ký này là chữ ký của tôi.</span>
            </label>
            <Button onClick={() => void sign()} disabled={busy || !confirmed}>{busy ? "Đang hoàn tất…" : "Xác nhận ký hợp đồng"}</Button>
          </section>
        </>
      )}

      {step === "complete" && (
        <section className="max-w-2xl space-y-3 rounded-xl border border-emerald-200 bg-emerald-50 p-6 text-emerald-950">
          <h2 className="text-xl font-semibold">Đã hoàn tất ký hợp đồng</h2>
          <p className="text-sm leading-6">Chữ ký đã được ghi nhận. Đường dẫn này không thể dùng để ký lại. Vui lòng liên hệ công ty nếu cần bản hợp đồng cuối.</p>
        </section>
      )}
    </main>
  );
}
