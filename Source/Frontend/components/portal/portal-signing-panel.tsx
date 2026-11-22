"use client";

import { useCallback, useEffect, useRef, useState, type PointerEvent } from "react";
import { Check, FileSignature, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";
import { customerApi, customerApiBlob, handleAuthError, type CustomerSigningOtpResult } from "@/services/customer-api";
import { dateTime } from "@/services/internal-api";
import { useRouter } from "next/navigation";
import WordPdfViewer from "@/components/internal/word-pdf-viewer";

type SigningPhase = "WAITING_APPROVAL" | "SUPERSEDED" | "WAITING_COMPANY" | "READY" | "SIGNED";
type SigningState = { phase: SigningPhase; companySignedAt: string | null; customerSignedAt: string | null };

const phaseMessage: Record<SigningPhase, string> = {
  WAITING_APPROVAL: "Hợp đồng đang chờ công ty duyệt bản chốt.",
  SUPERSEDED: "Phiên bản này đã được thay thế. Vui lòng mở phiên bản mới nhất.",
  WAITING_COMPANY: "Khách đã đồng ý. Hợp đồng sẽ mở để bạn ký sau khi đại diện công ty ký xong.",
  READY: "Đại diện công ty đã ký. Xác minh OTP để xem bản chốt và ký hợp đồng.",
  SIGNED: "Bạn đã ký hợp đồng.",
};

export default function PortalSigningPanel({ reviewId }: { reviewId: string }) {
  const router = useRouter();
  const [state, setState] = useState<SigningState | null>(null);
  const [challengeId, setChallengeId] = useState("");
  const [otp, setOtp] = useState("");
  const [notice, setNotice] = useState("");
  const [error, setError] = useState("");
  const [pdfUrl, setPdfUrl] = useState("");
  const [busy, setBusy] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const [hasInk, setHasInk] = useState(false);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const drawingRef = useRef(false);
  const lastPointRef = useRef<{ x: number; y: number } | null>(null);
  const base = `/signing/${encodeURIComponent(reviewId)}`;

  const refreshState = useCallback(async () => {
    try {
      const result = await customerApi<SigningState>(base);
      setError("");
      setState(result);
      if (result.phase === "SIGNED" && !pdfUrl) {
        const pdf = await customerApiBlob(`${base}/final-document`);
        setPdfUrl(URL.createObjectURL(pdf));
      }
    } catch (e) {
      if (handleAuthError(e, router.replace)) setError((e as Error).message);
    }
  }, [base, pdfUrl, router]);

  useEffect(() => {
    let active = true;
    void (async () => {
      try {
        const result = await customerApi<SigningState>(base);
        if (!active) return;
        setState(result);
        if (result.phase === "SIGNED") {
          const pdf = await customerApiBlob(`${base}/final-document`);
          if (active) setPdfUrl(URL.createObjectURL(pdf));
        }
      } catch (e) {
        if (active && handleAuthError(e, router.replace)) setError((e as Error).message);
      }
    })();
    return () => { active = false; };
  }, [base, router]);

  useEffect(() => () => { if (pdfUrl) URL.revokeObjectURL(pdfUrl); }, [pdfUrl]);

  async function requestOtp() {
    setBusy(true); setError(""); setNotice("");
    try {
      const result = await customerApi<CustomerSigningOtpResult>(`${base}/otp/request`, "POST");
      setChallengeId(result.challengeId);
      setOtp(result.developmentOtp ?? "");
      setNotice(result.developmentOtp ? `Mã OTP thử nghiệm: ${result.developmentOtp}` : "Mã OTP đã được gửi đến số điện thoại đăng ký với hợp đồng.");
    } catch (e) { if (handleAuthError(e, router.replace)) setError((e as Error).message); }
    finally { setBusy(false); }
  }

  async function verifyOtp() {
    setBusy(true); setError(""); setNotice("");
    try {
      await customerApi(`${base}/otp/verify`, "POST", { challengeId, otp });
      const pdf = await customerApiBlob(`${base}/document?challengeId=${encodeURIComponent(challengeId)}`);
      setPdfUrl(URL.createObjectURL(pdf));
      setNotice("OTP hợp lệ. Hãy đọc bản PDF trước khi ký.");
    } catch (e) { if (handleAuthError(e, router.replace)) setError((e as Error).message); }
    finally { setBusy(false); }
  }

  function point(event: PointerEvent<HTMLCanvasElement>) {
    const bounds = event.currentTarget.getBoundingClientRect();
    return { x: (event.clientX - bounds.left) * (event.currentTarget.width / bounds.width), y: (event.clientY - bounds.top) * (event.currentTarget.height / bounds.height) };
  }

  function startDrawing(event: PointerEvent<HTMLCanvasElement>) {
    event.currentTarget.setPointerCapture(event.pointerId);
    drawingRef.current = true;
    lastPointRef.current = point(event);
  }

  function draw(event: PointerEvent<HTMLCanvasElement>) {
    if (!drawingRef.current || !lastPointRef.current) return;
    const context = event.currentTarget.getContext("2d");
    if (!context) return;
    const next = point(event);
    context.strokeStyle = "#132b42"; context.lineWidth = 4; context.lineCap = "round"; context.lineJoin = "round";
    context.beginPath(); context.moveTo(lastPointRef.current.x, lastPointRef.current.y); context.lineTo(next.x, next.y); context.stroke();
    lastPointRef.current = next;
    setHasInk(true);
  }

  function clearSignature() {
    const canvas = canvasRef.current;
    const context = canvas?.getContext("2d");
    if (canvas && context) context.clearRect(0, 0, canvas.width, canvas.height);
    setHasInk(false);
    setConfirmed(false);
  }

  function croppedSignature(canvas: HTMLCanvasElement): string | null {
    const context = canvas.getContext("2d");
    if (!context) return null;
    const { data, width, height } = context.getImageData(0, 0, canvas.width, canvas.height);
    let left = width, top = height, right = -1, bottom = -1;
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
      if (data[(y * width + x) * 4 + 3] === 0) continue;
      left = Math.min(left, x); top = Math.min(top, y); right = Math.max(right, x); bottom = Math.max(bottom, y);
    }
    if (right < left || bottom < top) return null;
    const padding = 18;
    left = Math.max(0, left - padding); top = Math.max(0, top - padding);
    right = Math.min(width - 1, right + padding); bottom = Math.min(height - 1, bottom + padding);
    const crop = document.createElement("canvas");
    crop.width = right - left + 1; crop.height = bottom - top + 1;
    crop.getContext("2d")?.putImageData(context.getImageData(left, top, crop.width, crop.height), 0, 0);
    return crop.toDataURL("image/png").split(",")[1] ?? null;
  }

  async function sign() {
    const canvas = canvasRef.current;
    const signaturePngBase64 = canvas ? croppedSignature(canvas) : null;
    if (!canvas || !challengeId || !confirmed || !hasInk || !signaturePngBase64) { setError("Vẽ chữ ký và xác nhận bạn đã đọc hợp đồng trước khi ký."); return; }
    setBusy(true); setError(""); setNotice("");
    try {
      const signedPdf = await customerApiBlob(`${base}/sign`, "POST", {
        challengeId, signaturePngBase64, confirmed,
      });
      setPdfUrl(URL.createObjectURL(signedPdf));
      setState(current => current && ({ ...current, phase: "SIGNED", customerSignedAt: new Date().toISOString() }));
      setNotice("Chữ ký đã được lưu trên bản PDF hoàn tất. Bạn có thể xem hoặc tải bản đã ký bên dưới.");
    } catch (e) { if (handleAuthError(e, router.replace)) setError((e as Error).message); }
    finally { setBusy(false); }
  }

  return <section className={s.companySigningDetail} aria-label="Ký hợp đồng trong cổng khách hàng">
    <header className={s.companySigningDetailHeader}><div><h2><FileSignature size={18}/> Ký hợp đồng</h2><p>{state ? phaseMessage[state.phase] : "Đang kiểm tra trạng thái ký…"}</p></div></header>
    <div className={s.companySigningWork}>
      <div className={s.companySigningPdf}>
        <div className={s.companySigningPdfLabel}><strong>{state?.phase === "SIGNED" ? "Bản PDF đã ký" : "PDF hợp đồng"}</strong><span>{state?.phase === "SIGNED" ? "Đã ký" : "Bản công ty đã ký"}</span></div>
        {pdfUrl ? <WordPdfViewer url={pdfUrl}/> : <div style={{ minHeight: 180, display: "grid", alignContent: "center", justifyItems: "center", padding: 24, color: "var(--muted-foreground)", textAlign: "center" }}>
          {state?.phase === "READY" ? "Xác minh OTP để mở PDF hợp đồng ở đây." : state ? phaseMessage[state.phase] : "Đang tải trạng thái ký…"}
        </div>}
      </div>
      <aside className={s.companySigningActions} aria-label="Thao tác ký của khách hàng">
        {state?.companySignedAt && <p className={s.muted}>Công ty đã ký lúc {dateTime(state.companySignedAt)}.</p>}
        {state?.customerSignedAt && <p className={s.muted}>Bạn đã ký lúc {dateTime(state.customerSignedAt)}.</p>}
        {state?.phase === "WAITING_COMPANY" && <Button type="button" variant="outline" onClick={() => void refreshState()} disabled={busy}><RefreshCw size={15}/>Kiểm tra lại trạng thái</Button>}
        {state?.phase === "READY" && !pdfUrl && !challengeId && <Button onClick={() => void requestOtp()} disabled={busy}><RefreshCw size={15}/>{busy ? "Đang gửi OTP…" : "Gửi OTP để xem và ký"}</Button>}
        {challengeId && !pdfUrl && state?.phase === "READY" && <div style={{ display: "grid", gap: 10 }}>
          <Label htmlFor={`portal-sign-otp-${reviewId}`}>Mã OTP gồm 6 chữ số</Label>
          <Input id={`portal-sign-otp-${reviewId}`} inputMode="numeric" autoComplete="one-time-code" maxLength={6} value={otp} onChange={event => setOtp(event.target.value.replace(/\D/g, "").slice(0, 6))}/>
          <Button onClick={() => void verifyOtp()} disabled={busy || otp.length !== 6}>{busy ? "Đang xác minh…" : "Xác minh OTP và mở PDF"}</Button>
        </div>}
        {pdfUrl && state?.phase !== "SIGNED" && <>
          <div className={s.companySigningSignatureLabel}>Vẽ chữ ký của bạn</div>
          <canvas ref={canvasRef} width={900} height={260} aria-label="Khung vẽ chữ ký của khách hàng" onPointerDown={startDrawing} onPointerMove={draw}
            onPointerUp={() => { drawingRef.current = false; lastPointRef.current = null; }} onPointerCancel={() => { drawingRef.current = false; lastPointRef.current = null; }} className={s.companySigningCanvas}/>
          <Button type="button" variant="outline" onClick={clearSignature}>Xóa chữ ký</Button>
          <label className={s.companySigningConfirm}><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)}/>Tôi đã đọc PDF và đồng ý ký phiên bản này.</label>
          <Button onClick={() => void sign()} disabled={busy || !confirmed || !hasInk}><Check size={15}/>{busy ? "Đang lưu chữ ký…" : "Ký hợp đồng"}</Button>
        </>}
        {state?.phase === "SIGNED" && pdfUrl && <a className={ui.secondaryButton} href={pdfUrl} download="signed-contract.pdf">Tải hợp đồng đã ký</a>}
        {error && <p role="alert" className={ui.error}>{error}</p>}
        {notice && <p role="status" className={ui.notice}>{notice}</p>}
      </aside>
    </div>
  </section>;
}
