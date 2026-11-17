"use client";

import { useState } from "react";
import { CheckCircle2, Download, Eye, FileSignature, Loader2, XCircle } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utils";
import type { ContentCheck, SignedEvidence } from "@/services/scans/signed-scans";
import { formatDateTime, formatFileSize, formatKeywordValue, formatSignDate, KEYWORD_TYPE_LABELS } from "./format";
import { AcceptEvidenceDialog, RejectEvidenceDialog } from "./signed-evidence-forms";
import { EvidenceStatusBadge, StatusBadge } from "./status-badge";

const CHECK_LABELS: Record<ContentCheck["result"], { label: string; tone: "success" | "danger" | "neutral" | "info" }> = {
  Match: { label: "Khớp", tone: "success" },
  Mismatch: { label: "Không khớp", tone: "danger" },
  NotFound: { label: "Không đọc được", tone: "neutral" },
  Pending: { label: "Đang đọc", tone: "info" },
};

/** F18 step 1: the contract's own values next to what OCR read on the paper. */
function ContentChecks({ evidence }: { evidence: SignedEvidence }) {
  const failed = evidence.keywords.status === "Failed";
  return (
    <div className="space-y-2">
      <p className="text-sm font-semibold">Đối chiếu nội dung với hồ sơ</p>
      {failed ? (
        <p className="text-sm text-muted-foreground">
          Chưa đọc được nội dung bản ký nên chưa đối chiếu tự động. Người kiểm tra cần đối chiếu bằng mắt và ghi chú.
        </p>
      ) : (
        <ul className="divide-y rounded-lg border">
          {evidence.contentChecks.map((check) => {
            const result = CHECK_LABELS[check.result];
            return (
              <li key={check.field} className="space-y-1 px-3 py-2.5 text-sm">
                <div className="flex items-center justify-between gap-3">
                  <span className="font-medium">{KEYWORD_TYPE_LABELS[check.field]}</span>
                  <StatusBadge tone={result.tone}>{result.label}</StatusBadge>
                </div>
                <dl className="grid grid-cols-[6.5rem_minmax(0,1fr)] gap-x-2 gap-y-0.5 text-muted-foreground">
                  <dt>Trên hồ sơ</dt>
                  <dd className="break-words text-foreground tabular-nums">
                    {check.expected ? formatKeywordValue(check.field, check.expected) : "—"}
                  </dd>
                  <dt>Trên bản ký</dt>
                  <dd className="break-words tabular-nums">
                    {check.found.length === 0
                      ? "—"
                      : check.found.map((value) => formatKeywordValue(check.field, value)).join(" · ")}
                  </dd>
                </dl>
              </li>
            );
          })}
        </ul>
      )}
      <p className="text-xs text-muted-foreground">
        OCR chỉ hỗ trợ đối chiếu; chữ ký, con dấu và toàn bộ nội dung vẫn do người kiểm tra xác nhận.
      </p>
    </div>
  );
}

interface SignedEvidenceCardProps {
  evidence: SignedEvidence;
  opening: boolean;
  /** Whether the signed-in employee may accept or reject (F18 steps 4 and 5). */
  canReview: boolean;
  onOpen: (evidence: SignedEvidence, download: boolean) => void;
  onAccept: (
    evidence: SignedEvidence,
    input: { partyASigned: boolean; partyBSigned: boolean; contentMatches: boolean; note: string | null },
  ) => Promise<boolean>;
  onReject: (evidence: SignedEvidence, reason: string) => Promise<boolean>;
}

export function SignedEvidenceCard({ evidence, opening, canReview, onOpen, onAccept, onReject }: SignedEvidenceCardProps) {
  const [accepting, setAccepting] = useState(false);
  const [rejecting, setRejecting] = useState(false);
  const pending = evidence.status === "PendingReview";
  const reading = evidence.keywords.status === "Pending" || evidence.keywords.status === "Processing";
  const isPdf = evidence.contentType === "application/pdf";
  const facts = [
    `Ngày ký ${formatSignDate(evidence.signDate)}`,
    evidence.pageCount ? `${evidence.pageCount} trang` : null,
    formatFileSize(evidence.fileSizeBytes),
  ].filter(Boolean);

  return (
    <Card className={cn(!pending && evidence.status !== "Accepted" && "bg-muted/30")}>
      <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 space-y-1.5">
          <CardTitle className="flex flex-wrap items-center gap-2">
            <FileSignature className="size-5 text-primary" />
            <span className="break-all">{evidence.fileName}</span>
          </CardTitle>
          <div className="flex flex-wrap items-center gap-2">
            <EvidenceStatusBadge status={evidence.status} />
            <Badge variant="outline">Phiên bản {evidence.versionNo}</Badge>
            <span className="text-xs text-muted-foreground">#{evidence.evidenceId}</span>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          {isPdf && (
            <Button variant="outline" size="sm" disabled={opening} onClick={() => onOpen(evidence, false)}>
              {opening ? <Loader2 className="size-4 animate-spin" /> : <Eye className="size-4" />}
              Xem
            </Button>
          )}
          <Button variant="outline" size="sm" disabled={opening} onClick={() => onOpen(evidence, true)}>
            <Download className="size-4" />
            Tải về
          </Button>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="space-y-1 text-sm text-muted-foreground">
          <p>{facts.join(" · ")}</p>
          <p>
            Tải lên bởi {evidence.uploadedBy.displayName} lúc {formatDateTime(evidence.uploadedAt)}
          </p>
          <p className="break-all text-xs">SHA-256: {evidence.sha256}</p>
          {evidence.supersedesEvidenceId && (
            <p>
              Thay cho bản #{evidence.supersedesEvidenceId}
              {evidence.supersedeReason ? `. Lý do: ${evidence.supersedeReason}` : ""}
            </p>
          )}
          {evidence.supersededAt && (
            <p>
              Bị thay lúc {formatDateTime(evidence.supersededAt)}
              {evidence.supersededByName ? ` bởi ${evidence.supersededByName}` : ""}
            </p>
          )}
        </div>

        {evidence.review && (
          <div
            className={cn(
              "rounded-lg border p-3 text-sm",
              evidence.review.result === "Accepted"
                ? "border-emerald-500/20 bg-emerald-500/5"
                : "border-red-500/20 bg-red-500/5",
            )}
          >
            <p className="font-medium">
              {evidence.review.result === "Accepted" ? "Đã xác nhận hợp lệ" : "Không hợp lệ"} · {evidence.review.reviewerName}{" "}
              · {formatDateTime(evidence.review.at)}
            </p>
            {evidence.review.note && <p className="mt-1">{evidence.review.note}</p>}
          </div>
        )}

        {(pending || evidence.status === "Accepted") && <ContentChecks evidence={evidence} />}

        {pending && !canReview && (
          <p className="border-t pt-4 text-sm text-muted-foreground">Bản ký đang chờ người có quyền kiểm tra xác nhận.</p>
        )}

        {pending && canReview && (
          <div className="flex flex-wrap items-center gap-3 border-t pt-4">
            <Button onClick={() => setAccepting(true)} disabled={reading}>
              <CheckCircle2 className="size-4" />
              Xác nhận hợp lệ
            </Button>
            <Button variant="outline" onClick={() => setRejecting(true)}>
              <XCircle className="size-4" />
              Không hợp lệ
            </Button>
            {reading && (
              <p className="flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="size-4 animate-spin" />
                Đang đọc nội dung để đối chiếu...
              </p>
            )}
          </div>
        )}
      </CardContent>
      {accepting && (
        <AcceptEvidenceDialog
          evidence={evidence}
          open={accepting}
          onOpenChange={setAccepting}
          onSubmit={(input) => onAccept(evidence, input)}
        />
      )}
      {rejecting && (
        <RejectEvidenceDialog open={rejecting} onOpenChange={setRejecting} onSubmit={(reason) => onReject(evidence, reason)} />
      )}
    </Card>
  );
}
