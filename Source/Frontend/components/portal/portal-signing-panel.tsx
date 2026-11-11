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
    context.strokeStyle = "#111827"; context.lineWidth = 3; context.lineCap = "round"; context.lineJoin = "round";
    context.beginPath(); context.moveTo(lastPointRef.current.x, lastPointRef.current.y); context.lineTo(next.x, next.y); context.stroke();
    lastPointRef.current = next;
  }

  function clearSignature() {
    const canvas = canvasRef.current;
    const context = canvas?.getContext("2d");
    if (canvas && context) context.clearRect(0, 0, canvas.width, canvas.height);
    setConfirmed(false);
  }

  async function sign() {
    const canvas = canvasRef.current;
    const context = canvas?.getContext("2d");
    const hasInk = !!canvas && (context?.getImageData(0, 0, canvas.width, canvas.height).data.some((value, index) => index % 4 === 3 && value > 0) ?? false);
    if (!canvas || !challengeId || !confirmed || !hasInk) { setError("Vẽ chữ ký và xác nhận bạn đã đọc hợp đồng trước khi ký."); return; }
    setBusy(true); setError(""); setNotice("");
    try {
      const signedPdf = await customerApiBlob(`${base}/sign`, "POST", {
        challengeId, signaturePngBase64: canvas.toDataURL("image/png").split(",")[1], confirmed,
      });
      setPdfUrl(URL.createObjectURL(signedPdf));
      setState(current => current && ({ ...current, phase: "SIGNED", customerSignedAt: new Date().toISOString() }));
      setNotice("Chữ ký đã được lưu trên bản PDF hoàn tất. Bạn có thể xem hoặc tải bản đã ký bên dưới.");
    } catch (e) { if (handleAuthError(e, router.replace)) setError((e as Error).message); }
    finally { setBusy(false); }
  }

  return <section className={ui.card} aria-label="Ký hợp đồng trong cổng khách hàng" style={{ marginTop: 18 }}>
    <h2 style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 17 }}><FileSignature size={18}/>Ký hợp đồng</h2>
    <p className={s.muted} style={{ marginTop: 6 }}>{state ? phaseMessage[state.phase] : "Đang kiểm tra trạng thái ký…"}</p>
    {state?.phase === "WAITING_COMPANY" && <Button type="button" variant="outline" style={{ marginTop: 10 }} onClick={() => void refreshState()}><RefreshCw size={15}/>Kiểm tra lại trạng thái</Button>}
    {state?.companySignedAt && <p className={s.muted} style={{ marginTop: 5 }}>Công ty đã ký lúc {dateTime(state.companySignedAt)}.</p>}
    {state?.customerSignedAt && <p className={s.muted} style={{ marginTop: 5 }}>Bạn đã ký lúc {dateTime(state.customerSignedAt)}.</p>}
    {state?.phase === "READY" && !pdfUrl && <Button style={{ marginTop: 14 }} onClick={() => void requestOtp()} disabled={busy}><RefreshCw size={15}/>{busy ? "Đang gửi OTP…" : "Gửi OTP để xem và ký"}</Button>}
    {challengeId && !pdfUrl && state?.phase === "READY" && <div style={{ display: "grid", gap: 10, maxWidth: 420, marginTop: 14 }}>
      <Label htmlFor={`portal-sign-otp-${reviewId}`}>Mã OTP gồm 6 chữ số</Label>
      <Input id={`portal-sign-otp-${reviewId}`} inputMode="numeric" autoComplete="one-time-code" maxLength={6} value={otp} onChange={event => setOtp(event.target.value.replace(/\D/g, "").slice(0, 6))}/>
      <Button onClick={() => void verifyOtp()} disabled={busy || otp.length !== 6}>{busy ? "Đang xác minh…" : "Xác minh OTP và xem PDF"}</Button>
    </div>}
    {pdfUrl && <div style={{ display: "grid", gap: 12, marginTop: 14 }}>
      <iframe title={state?.phase === "SIGNED" ? "Hợp đồng đã ký hoàn tất" : "Hợp đồng công ty đã ký"} src={pdfUrl} style={{ width: "100%", height: "min(70vh, 760px)", border: "1px solid var(--border)", borderRadius: 8 }}/>
      {state?.phase !== "SIGNED" && <>
        <strong>Chữ ký của bạn</strong>
        <canvas ref={canvasRef} width={900} height={220} aria-label="Khung vẽ chữ ký của khách hàng" onPointerDown={startDrawing} onPointerMove={draw}
          onPointerUp={() => { drawingRef.current = false; lastPointRef.current = null; }} onPointerCancel={() => { drawingRef.current = false; lastPointRef.current = null; }}
          style={{ width: "100%", maxWidth: 700, height: 175, border: "1px solid var(--border)", borderRadius: 6, touchAction: "none", background: "white" }}/>
        <div style={{ display: "flex", flexWrap: "wrap", gap: 10, alignItems: "center" }}>
          <Button type="button" variant="outline" onClick={clearSignature}>Xóa chữ ký</Button>
          <label style={{ display: "flex", gap: 8, alignItems: "center" }}><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)}/>Tôi đã đọc và đồng ý ký phiên bản này.</label>
          <Button onClick={() => void sign()} disabled={busy || !confirmed}><Check size={15}/>{busy ? "Đang lưu chữ ký…" : "Ký hợp đồng"}</Button>
        </div>
      </>}
      {state?.phase === "SIGNED" && <a className={ui.secondaryButton} href={pdfUrl} download="signed-contract.pdf" style={{ justifySelf: "start" }}>Tải hợp đồng đã ký</a>}
    </div>}
    {error && <p role="alert" className={ui.error} style={{ marginTop: 12 }}>{error}</p>}
    {notice && <p role="status" className={ui.notice} style={{ marginTop: 12 }}>{notice}</p>}
  </section>;
}
