"use client";

import Link from "next/link";
import { useEffect, useId, useRef, useState } from "react";
import { Download, ExternalLink, FileWarning, Loader2, Mail, Send } from "lucide-react";
import { blobErrorMessage, FileKeyCancelled, useFileKey } from "@/components/internal/file-key";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/sonner";
import { getScanErrorMessage } from "@/services/scans/http";
import { scannedContractsApi } from "@/services/scans/scanned-contracts";
import { signedEvidenceApi, type ScanEmailResult, type ScanSearchMatch } from "@/services/scans/signed-scans";
import { EmailInput, isEmail, MAX_EMAILS } from "./email-input";
import { formatKeywordValue, KEYWORD_TYPE_LABELS } from "./format";
import { Highlight } from "./highlight";

/** A search result the actions work on: a paper-signing evidence or a scanned old contract. */
export interface ScanTarget {
  kind: "evidence" | "scan";
  id: string;
  title: string;
  details: { label: string; value: string }[];
  matches: ScanSearchMatch[];
  /** The page with everything about it. */
  href: string;
}

const MAX_NOTE = 1000;

/** Each opened result starts its dialog afresh. */
const keyOf = (target: ScanTarget) => `${target.kind}-${target.id}`;

async function fetchFile(target: ScanTarget, key: string | undefined) {
  if (target.kind === "evidence") return (await signedEvidenceApi.downloadFile(target.id, key)).blob;
  return scannedContractsApi.jobFile(target.id, key);
}

const sendEmail = (target: ScanTarget, recipients: string[], note: string, key: string | undefined) => {
  const input = { recipients, note: note.trim() || undefined };
  return target.kind === "evidence"
    ? signedEvidenceApi.emailFile(target.id, input, key)
    : scannedContractsApi.emailJob(target.id, input, key);
};

function Summary({ target, query }: { target: ScanTarget; query: string }) {
  return (
    <div className="space-y-3 text-sm">
      <dl className="grid grid-cols-[7rem_minmax(0,1fr)] gap-x-3 gap-y-1.5">
        {target.details.map((detail) => (
          <div key={detail.label} className="contents">
            <dt className="text-muted-foreground">{detail.label}</dt>
            <dd className="break-words">
              <Highlight text={detail.value} query={query} />
            </dd>
          </div>
        ))}
      </dl>
      {target.matches.length > 0 && (
        <div>
          <p className="mb-1.5 text-muted-foreground">Từ khóa khớp</p>
          <ul className="flex flex-wrap gap-1.5">
            {target.matches.map((match, index) => (
              <li key={`${match.type}-${match.value}-${index}`} className="rounded border bg-muted/50 px-1.5 py-0.5 tabular-nums">
                <Highlight text={formatKeywordValue(match.type, match.value)} query={query} />
                <span className="ml-1 text-xs text-muted-foreground">
                  {KEYWORD_TYPE_LABELS[match.type].toLowerCase()}
                  {match.page ? `, tr. ${match.page}` : ""}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

// -------------------------------------------------------------------------------------------------------------------
// Review
// -------------------------------------------------------------------------------------------------------------------

/** Shows the file in the search page itself, next to what the search found in it. */
export function ScanReviewDialog({
  target,
  query,
  onClose,
  onEmail,
}: {
  target: ScanTarget | null;
  query: string;
  onClose: () => void;
  onEmail: (target: ScanTarget) => void;
}) {
  return (
    <Dialog open={target !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="flex h-[90vh] w-[calc(100vw-2rem)] max-w-6xl flex-col gap-3 sm:max-w-6xl">
        {target && <ReviewContent key={keyOf(target)} target={target} query={query} onClose={onClose} onEmail={onEmail} />}
      </DialogContent>
    </Dialog>
  );
}

function ReviewContent({
  target,
  query,
  onClose,
  onEmail,
}: {
  target: ScanTarget;
  query: string;
  onClose: () => void;
  onEmail: (target: ScanTarget) => void;
}) {
  const { withFileKey } = useFileKey();
  const [file, setFile] = useState<{ url: string; type: string } | null>(null);
  const [error, setError] = useState<string | null>(null);
  // The latest close handler, without fetching the file again when the parent renders.
  const close = useRef(onClose);
  useEffect(() => {
    close.current = onClose;
  }, [onClose]);

  useEffect(() => {
    let cancelled = false;
    let url: string | null = null;
    withFileKey((key) => fetchFile(target, key)).then(
      (blob) => {
        if (cancelled) return;
        url = URL.createObjectURL(blob);
        setFile({ url, type: blob.type });
      },
      async (loadError: unknown) => {
        if (cancelled) return;
        if (loadError instanceof FileKeyCancelled) close.current();
        else setError(await blobErrorMessage(loadError, "Không mở được tệp."));
      },
    );
    return () => {
      cancelled = true;
      if (url) URL.revokeObjectURL(url);
    };
  }, [target, withFileKey]);

  const isImage = file?.type.startsWith("image/");
  const isPdf = file?.type === "application/pdf";

  return (
    <>
      <DialogHeader>
        <DialogTitle className="pr-8 break-all">
          <Highlight text={target.title} query={query} />
        </DialogTitle>
        <DialogDescription>Xem nhanh bản scan. Tệp chỉ mở bằng key của bạn và không được lưu lại trên máy.</DialogDescription>
      </DialogHeader>
      <div className="grid min-h-0 flex-1 gap-4 lg:grid-cols-[18rem_minmax(0,1fr)]">
        <div className="min-h-0 overflow-y-auto">
          <Summary target={target} query={query} />
        </div>
        <div className="flex min-h-[50vh] items-center justify-center overflow-hidden rounded-lg border bg-muted/30">
          {error ? (
            <div className="flex flex-col items-center gap-2 p-6 text-center text-sm text-destructive">
              <FileWarning className="size-8" aria-hidden="true" />
              {error}
            </div>
          ) : !file ? (
            <Loader2 className="size-6 animate-spin text-muted-foreground" aria-label="Đang mở tệp" />
          ) : isPdf ? (
            <iframe src={file.url} title={`Bản scan ${target.title}`} className="size-full" />
          ) : isImage ? (
            // eslint-disable-next-line @next/next/no-img-element -- a local object URL, not an optimisable image
            <img src={file.url} alt={`Bản scan ${target.title}`} className="max-h-full max-w-full object-contain" />
          ) : (
            <p className="p-6 text-center text-sm text-muted-foreground">Không xem trước được loại tệp này. Hãy tải về.</p>
          )}
        </div>
      </div>
      <DialogFooter className="flex-wrap gap-2 sm:justify-between">
        <Button asChild variant="ghost">
          <Link href={target.href}>
            <ExternalLink aria-hidden="true" />
            Mở trang chi tiết
          </Link>
        </Button>
        <div className="flex flex-wrap gap-2">
          {file && (
            <Button asChild variant="outline">
              <a href={file.url} download>
                <Download aria-hidden="true" />
                Tải về
              </a>
            </Button>
          )}
          <Button onClick={() => onEmail(target)}>
            <Mail aria-hidden="true" />
            Gửi email
          </Button>
        </div>
      </DialogFooter>
    </>
  );
}

// -------------------------------------------------------------------------------------------------------------------
// Email
// -------------------------------------------------------------------------------------------------------------------

/** Sends the scan as a PDF attachment to several addresses, with an optional note. */
export function ScanEmailDialog({ target, onClose }: { target: ScanTarget | null; onClose: () => void }) {
  const [sending, setSending] = useState(false);
  return (
    <Dialog open={target !== null} onOpenChange={(open) => !open && !sending && onClose()}>
      <DialogContent className="sm:max-w-lg">
        {target && <EmailForm key={keyOf(target)} target={target} onClose={onClose} onSending={setSending} />}
      </DialogContent>
    </Dialog>
  );
}

function EmailForm({
  target,
  onClose,
  onSending,
}: {
  target: ScanTarget;
  onClose: () => void;
  onSending: (sending: boolean) => void;
}) {
  const { withFileKey } = useFileKey();
  const [emails, setEmails] = useState<string[]>([]);
  const [note, setNote] = useState("");
  const [sending, setSending] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [result, setResult] = useState<ScanEmailResult | null>(null);
  const emailsId = useId();
  const noteId = useId();

  const wrong = emails.filter((email) => !isEmail(email));
  const tooMany = emails.length > MAX_EMAILS;

  const busy = (value: boolean) => {
    setSending(value);
    onSending(value);
  };

  const send = async () => {
    if (emails.length === 0) return setProblem("Hãy nhập ít nhất một email.");
    if (wrong.length > 0) return setProblem(`Email không hợp lệ: ${wrong.join(", ")}.`);
    if (tooMany) return setProblem(`Mỗi lần gửi tối đa ${MAX_EMAILS} email.`);
    setProblem(null);
    busy(true);
    try {
      const sent = await withFileKey((key) => sendEmail(target, emails, note, key));
      if (sent.failed.length === 0) {
        toast.success(`Đã gửi bản scan tới ${sent.sent.length} email.`);
        busy(false);
        onClose();
        return;
      }
      // Keep only the addresses that failed, so trying again does not send twice.
      setResult(sent);
      setEmails(sent.failed.map((failure) => failure.email));
    } catch (sendError) {
      if (!(sendError instanceof FileKeyCancelled)) setProblem(getScanErrorMessage(sendError, "Không gửi được email."));
    }
    busy(false);
  };

  return (
    <>
      <DialogHeader>
        <DialogTitle>Gửi bản scan qua email</DialogTitle>
        <DialogDescription className="break-all">
          {target.title}: gửi kèm tệp PDF và thông tin hợp đồng. Mỗi người nhận một email riêng.
        </DialogDescription>
      </DialogHeader>
      <div className="space-y-4">
        <div className="space-y-1.5">
          <div className="flex items-baseline justify-between">
            <Label htmlFor={emailsId}>Gửi tới</Label>
            <span className={tooMany ? "text-xs text-destructive" : "text-xs text-muted-foreground"}>
              {emails.length}/{MAX_EMAILS}
            </span>
          </div>
          <EmailInput
            id={emailsId}
            value={emails}
            onChange={(next) => {
              setEmails(next);
              setProblem(null);
            }}
            disabled={sending}
            invalid={wrong.length > 0 || tooMany}
          />
          <p className="text-xs text-muted-foreground">Nhập nhiều email: nhấn Enter, dấu phẩy hoặc dán cả danh sách.</p>
        </div>
        <div className="space-y-1.5">
          <Label htmlFor={noteId}>Lời nhắn (không bắt buộc)</Label>
          <Textarea
            id={noteId}
            rows={3}
            maxLength={MAX_NOTE}
            value={note}
            disabled={sending}
            onChange={(event) => setNote(event.target.value)}
            placeholder="VD: Gửi anh/chị bản hợp đồng đã ký để đối chiếu."
          />
        </div>
        {result && (
          <Alert>
            <AlertDescription className="space-y-1 text-sm">
              <p>
                Đã gửi {result.sent.length} email. {result.failed.length} email chưa gửi được (đã giữ lại ở trên để gửi lại):
              </p>
              <ul className="list-disc pl-5 text-destructive">
                {result.failed.map((failure) => (
                  <li key={failure.email}>
                    {failure.email}: {failure.reason}
                  </li>
                ))}
              </ul>
            </AlertDescription>
          </Alert>
        )}
        {problem && (
          <p role="alert" className="text-sm text-destructive">
            {problem}
          </p>
        )}
        <p className="text-xs text-muted-foreground">
          Tệp gửi đi không còn được mã hóa. Chỉ gửi cho người được phép xem hợp đồng. Mỗi lần gửi đều được ghi nhật ký.
        </p>
      </div>
      <DialogFooter>
        <Button variant="outline" disabled={sending} onClick={onClose}>
          Hủy
        </Button>
        <Button disabled={sending || emails.length === 0} onClick={() => void send()}>
          {sending ? <Loader2 className="animate-spin" aria-hidden="true" /> : <Send aria-hidden="true" />}
          {result ? "Gửi lại" : "Gửi"}
        </Button>
      </DialogFooter>
    </>
  );
}
