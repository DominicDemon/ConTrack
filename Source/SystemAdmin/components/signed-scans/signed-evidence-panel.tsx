"use client";

import { useCallback, useEffect, useState, type ReactNode } from "react";
import { CheckCircle2, FileCheck2, FileUp, History, Info, Loader2, RefreshCw, ShieldCheck } from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { toast } from "@/components/ui/sonner";
import { getScanErrorMessage } from "@/services/scans/http";
import {
  signedEvidenceApi,
  type AddScanKeywordInput,
  type ScanKeyword,
  type SignedEvidence,
  type SignedEvidenceEvent,
  type SignedEvidenceOverview,
} from "@/services/scans/signed-scans";
import { formatDateTime, formatKeywordValue, formatMoney, formatSignDate } from "./format";
import { ScanKeywordsSection } from "./scan-keywords-section";
import { SIGNING_PERMISSIONS } from "@/services/scans/auth";
import { useScanAccess } from "@/components/internal/scan-access";
import { blobErrorMessage, FileKeyCancelled, useFileKey } from "@/components/internal/file-key";
import { SignedEvidenceCard } from "./signed-evidence-card";
import { SupersedeEvidenceForm, UploadEvidenceForm } from "./signed-evidence-forms";
import { ContractStatusBadge } from "./status-badge";

/** How often the panel asks again while OCR is still reading a file. */
const POLL_MS = 2000;

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

type LoadState = { kind: "loading" } | { kind: "error"; message: string } | { kind: "ready"; overview: SignedEvidenceOverview };

function InfoRow({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-4 border-b py-2.5 text-sm last:border-b-0">
      <span className="text-muted-foreground">{label}</span>
      <span className="min-w-0 text-right font-medium">{children}</span>
    </div>
  );
}

const describe = (event: SignedEvidenceEvent) => {
  const actor = event.actorName ?? "Hệ thống";
  const subject = `bản ký #${event.evidenceId}`;
  switch (event.kind) {
    case "Uploaded":
      return event.detail ? `${actor} tải lên ${subject} thay thế. Lý do: ${event.detail}` : `${actor} tải lên ${subject}.`;
    case "Accepted":
      return `${actor} xác nhận ${subject} hợp lệ${event.detail ? `. Ghi chú: ${event.detail}` : "."}`;
    case "Rejected":
      return `${actor} đánh dấu ${subject} không hợp lệ. Lý do: ${event.detail ?? ""}`;
    case "Superseded":
      return `${subject} được thay bằng bản mới.`;
    case "KeywordAdded":
      return `${actor} thêm từ khóa ${event.detail ?? ""} cho ${subject}.`;
    case "KeywordRemoved":
      return `${actor} xóa từ khóa ${event.detail ?? ""} của ${subject}.`;
    default:
      return actor;
  }
};

/** Paper signing of a contract (Master_Database_HuongDan F18 and F19), laid out like the old signing panel. */
export function SignedEvidencePanel({
  documentId,
  onOverviewChange,
}: {
  documentId: string;
  /** Pass a stable function such as a state setter: a new function on each render reloads the panel. */
  onOverviewChange?: (overview: SignedEvidenceOverview) => void;
}) {
  const { can } = useScanAccess();
  const { ensureKey, withFileKey } = useFileKey();
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [reloadKey, setReloadKey] = useState(0);
  const [openingId, setOpeningId] = useState<string | null>(null);
  const reload = useCallback(() => setReloadKey((key) => key + 1), []);

  const show = useCallback(
    (overview: SignedEvidenceOverview) => {
      setState({ kind: "ready", overview });
      onOverviewChange?.(overview);
    },
    [onOverviewChange],
  );

  useEffect(() => {
    let cancelled = false;
    signedEvidenceApi.getOverview(documentId).then(
      (overview) => !cancelled && show(overview),
      (error: unknown) => {
        if (cancelled) return;
        // A failed refresh keeps what is already on screen.
        setState((current) =>
          current.kind === "ready" ? current : { kind: "error", message: getScanErrorMessage(error, "Không tải được hồ sơ ký.") },
        );
      },
    );
    return () => {
      cancelled = true;
    };
  }, [documentId, reloadKey, show]);

  const overview = state.kind === "ready" ? state.overview : null;
  const reading = overview?.evidences.some(
    (evidence) => evidence.keywords.status === "Pending" || evidence.keywords.status === "Processing",
  );
  useEffect(() => {
    if (!reading) return;
    const timer = window.setTimeout(reload, POLL_MS);
    return () => window.clearTimeout(timer);
  }, [reading, overview, reload]);

  /** Runs an action that answers with the fresh overview; resolves true when it went through. */
  const act = async (run: () => Promise<SignedEvidenceOverview>, success: string, failure: string) => {
    try {
      show(await run());
      toast.success(success);
      return true;
    } catch (error) {
      toast.error(getScanErrorMessage(error, failure));
      return false;
    }
  };

  /** A PDF opens in a new tab, as in the old app; otherwise the file is downloaded. */
  const open = async (evidence: SignedEvidence, download: boolean) => {
    const wantsPreview = evidence.contentType === "application/pdf" && !download;
    let preview: Window | null = null;
    setOpeningId(evidence.evidenceId);
    try {
      // The key is asked first, so the new tab is still opened from the click that gave it.
      if (wantsPreview) {
        await ensureKey();
        preview = window.open("about:blank", "_blank");
      }
      const { blob, fileName } = await withFileKey((key) => signedEvidenceApi.downloadFile(evidence.evidenceId, key), {
        beforeAsk: () => preview?.close(),
        afterAsk: () => {
          if (wantsPreview) preview = window.open("about:blank", "_blank");
        },
      });
      if (preview) {
        const url = URL.createObjectURL(new Blob([blob], { type: "application/pdf" }));
        preview.opener = null;
        preview.location.replace(url);
        window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
      } else {
        downloadBlob(blob, fileName);
      }
    } catch (error) {
      preview?.close();
      if (!(error instanceof FileKeyCancelled)) toast.error(await blobErrorMessage(error, "Không tải được tệp bản ký."));
    } finally {
      setOpeningId(null);
    }
  };

  const keywordActions = (evidence: SignedEvidence) => ({
    onRetry: async () => {
      try {
        await withFileKey((key) => signedEvidenceApi.retryKeywords(evidence.evidenceId, key));
        toast.success("Đã gửi yêu cầu đọc lại.");
        reload();
      } catch (error) {
        if (!(error instanceof FileKeyCancelled)) toast.error(getScanErrorMessage(error, "Không gửi được yêu cầu đọc lại."));
      }
    },
    onAddKeyword: async (input: AddScanKeywordInput) => {
      try {
        await signedEvidenceApi.addKeyword(evidence.evidenceId, input);
        toast.success("Đã thêm từ khóa.");
        reload();
        return true;
      } catch (error) {
        toast.error(getScanErrorMessage(error, "Không thêm được từ khóa."));
        return false;
      }
    },
    onRemoveKeyword: async (keyword: ScanKeyword) => {
      try {
        await signedEvidenceApi.removeKeyword(evidence.evidenceId, keyword.keywordId);
        toast.success("Đã xóa từ khóa.");
        reload();
      } catch (error) {
        toast.error(getScanErrorMessage(error, "Không xóa được từ khóa."));
      }
    },
  });

  if (state.kind === "loading") {
    return (
      <div className="flex min-h-48 items-center justify-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="size-5 animate-spin text-primary" />
        Đang tải hồ sơ ký...
      </div>
    );
  }

  if (state.kind === "error") {
    return (
      <Alert variant="destructive">
        <AlertTitle>Không tải được hồ sơ ký</AlertTitle>
        <AlertDescription className="mt-2 space-y-3">
          <p>{state.message}</p>
          <Button
            variant="outline"
            onClick={() => {
              setState({ kind: "loading" });
              reload();
            }}
          >
            <RefreshCw className="size-4" /> Thử lại
          </Button>
        </AlertDescription>
      </Alert>
    );
  }

  const data = state.overview;
  const canUploadScan = data.canUpload && can(SIGNING_PERMISSIONS.upload);
  const accepted = data.evidences.find((evidence) => evidence.status === "Accepted");
  const pending = data.evidences.some((evidence) => evidence.status === "PendingReview");
  const working = data.evidences.filter((evidence) => evidence.status === "PendingReview" || evidence.status === "Accepted");
  const method = data.signedOnline ? "Ký online (OTP)" : accepted ? "Ký giấy, bằng chứng hợp lệ" : "Chưa ghi nhận";

  return (
    <div className="space-y-6">
      {data.signedOnline ? (
        <Alert className="border-emerald-500/30 bg-emerald-500/5">
          <CheckCircle2 className="size-4 text-emerald-600" />
          <AlertTitle>Hợp đồng ký online</AlertTitle>
          <AlertDescription>Hợp đồng đang hoặc đã ký online bằng OTP nên không lưu bản ký giấy.</AlertDescription>
        </Alert>
      ) : canUploadScan ? (
        <Alert className="border-blue-500/30 bg-blue-500/5">
          <ShieldCheck className="size-4 text-blue-600" />
          <AlertTitle>Ký giấy và lưu bằng chứng</AlertTitle>
          <AlertDescription>
            Các bên đã ký ngoài hệ thống? Tải bản scan hoặc PDF có đủ chữ ký. Bản ký được kiểm tra chữ ký, con dấu và đối
            chiếu nội dung trước khi hợp đồng chuyển sang Đã ký.
          </AlertDescription>
        </Alert>
      ) : (
        data.uploadBlockedReason &&
        !accepted &&
        !pending && (
          <Alert className="border-amber-500/30 bg-amber-500/5">
            <Info className="size-4 text-amber-600" />
            <AlertTitle>Chưa tải được bản ký giấy</AlertTitle>
            <AlertDescription>{data.uploadBlockedReason}</AlertDescription>
          </Alert>
        )
      )}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FileCheck2 className="size-5 text-primary" />
            Thông tin ký
          </CardTitle>
        </CardHeader>
        <CardContent className="grid gap-x-8 md:grid-cols-2">
          <div>
            <InfoRow label="Trạng thái hợp đồng">
              <ContractStatusBadge status={data.contractStatus} />
            </InfoRow>
            <InfoRow label="Cách ký">{method}</InfoRow>
            <InfoRow label="Phiên bản đã thống nhất">
              {data.currentVersion ? `Phiên bản ${data.currentVersion.versionNo}${data.currentVersion.isLocked ? " (đã khóa)" : ""}` : "—"}
            </InfoRow>
            <InfoRow label="Ngày ký">{data.currentVersion?.signDate ? formatSignDate(data.currentVersion.signDate) : "—"}</InfoRow>
          </div>
          <div>
            <InfoRow label="Khách hàng">{data.customerName}</InfoRow>
            <InfoRow label="Mã số thuế">
              {data.customerTaxCode ? formatKeywordValue("TaxCode", data.customerTaxCode) : "—"}
            </InfoRow>
            <InfoRow label="Số điện thoại">
              {data.customerPhone ? formatKeywordValue("PhoneNumber", data.customerPhone) : "—"}
            </InfoRow>
            <InfoRow label="Giá trị">{formatMoney(data.totalAmount)}</InfoRow>
          </div>
        </CardContent>
      </Card>

      {canUploadScan && data.currentVersion && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <FileUp className="size-5 text-primary" />
              Tải bản ký giấy
            </CardTitle>
          </CardHeader>
          <CardContent>
            <UploadEvidenceForm
              version={data.currentVersion}
              onSubmit={({ file, signDate }) =>
                act(
                  () => signedEvidenceApi.upload(documentId, { file, versionId: data.currentVersion!.versionId, signDate }),
                  "Đã tải lên bản ký. Bản ký đang chờ kiểm tra.",
                  "Không tải lên được bản ký.",
                )
              }
            />
          </CardContent>
        </Card>
      )}

      {data.evidences.map((evidence) => (
        <div key={evidence.evidenceId} className="space-y-6">
          <SignedEvidenceCard
            evidence={evidence}
            opening={openingId === evidence.evidenceId}
            canReview={can(SIGNING_PERMISSIONS.review)}
            onOpen={(item, download) => void open(item, download)}
            onAccept={(item, input) =>
              act(() => signedEvidenceApi.accept(item.evidenceId, input), "Đã xác nhận bản ký hợp lệ.", "Không xác nhận được.")
            }
            onReject={(item, reason) =>
              act(() => signedEvidenceApi.reject(item.evidenceId, reason), "Đã ghi nhận bản ký không hợp lệ.", "Không ghi nhận được.")
            }
          />
          {working.includes(evidence) && <ScanKeywordsSection keywords={evidence.keywords} {...keywordActions(evidence)} />}
        </div>
      ))}

      {accepted && !pending && data.contractStatus !== "Cancelled" && can(SIGNING_PERMISSIONS.upload) && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <FileUp className="size-5 text-primary" />
              Thay bản ký đang hợp lệ
            </CardTitle>
          </CardHeader>
          <CardContent>
            <SupersedeEvidenceForm
              key={accepted.evidenceId}
              evidence={accepted}
              onSubmit={({ file, reason }) =>
                act(
                  () => signedEvidenceApi.supersede(accepted.evidenceId, file, reason),
                  "Đã tải lên bản thay thế. Bản này đang chờ kiểm tra.",
                  "Không tải lên được bản thay thế.",
                )
              }
            />
          </CardContent>
        </Card>
      )}

      {data.history.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <History className="size-5 text-primary" />
              Nhật ký bằng chứng ký
            </CardTitle>
          </CardHeader>
          <CardContent>
            <ol className="space-y-3.5">
              {data.history.map((event, index) => (
                <li key={`${event.at}-${event.kind}-${index}`} className="flex gap-3">
                  <span
                    aria-hidden="true"
                    className={
                      event.kind === "Rejected"
                        ? "mt-1.5 size-2.5 shrink-0 rounded-full bg-red-600"
                        : "mt-1.5 size-2.5 shrink-0 rounded-full bg-primary"
                    }
                  />
                  <div className="min-w-0">
                    <p className="text-sm text-muted-foreground">
                      <time dateTime={event.at}>{formatDateTime(event.at)}</time>
                    </p>
                    <p>{describe(event)}</p>
                  </div>
                </li>
              ))}
            </ol>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
