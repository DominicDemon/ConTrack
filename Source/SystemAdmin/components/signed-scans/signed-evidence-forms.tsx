"use client";

import { useId, useState, type ReactNode } from "react";
import { format } from "date-fns";
import { CheckCircle2, FileUp, Loader2, XCircle } from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { ConfirmDialog } from "@/components/ui/custom/confirm-dialog";
import { DateFilter } from "@/components/ui/custom/date-filter";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import type { SignedEvidence, SignedVersion } from "@/services/scans/signed-scans";
import { formatSignDate } from "./format";
import { imagesToPdf } from "./images-to-pdf";
import { MAX_SCAN_BYTES, pageCountLabel, ScanPagesPicker, type ScanPages } from "./scan-pages-picker";

const MAX_REASON_LENGTH = 1000;

function Required() {
  return <span className="text-destructive">*</span>;
}

/** The one file sent: a PDF as chosen, or the photos joined into one PDF page by page. */
const toUpload = async (pages: ScanPages) =>
  pages.kind === "pdf"
    ? pages.file
    : imagesToPdf(
        pages.pages.map((page) => page.photo),
        `ban-ky-${format(new Date(), "yyyyMMdd-HHmmss")}.pdf`,
      );

const tooBig = (file: File) =>
  // The server takes requests up to this size; the type of the file is not limited.
  file.size > MAX_SCAN_BYTES ? "Tệp vượt quá giới hạn 20 MB của máy chủ. Hãy scan thành PDF nhẹ hơn hoặc chụp ít trang hơn." : undefined;

/** F18, input: the scan of the paper both parties signed, for the agreed version, with the date on it. */
export function UploadEvidenceForm({
  version,
  onSubmit,
}: {
  version: SignedVersion;
  /** Resolves true when the evidence was saved. */
  onSubmit: (input: { file: File; signDate: string }) => Promise<boolean>;
}) {
  const [pages, setPages] = useState<ScanPages | null>(null);
  const [signDate, setSignDate] = useState<Date | undefined>();
  const [errors, setErrors] = useState<{ file?: string; date?: string }>({});
  const [confirming, setConfirming] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const dateId = useId();

  const openConfirm = () => {
    const found = {
      file: pages ? undefined : "Hãy chụp các trang hoặc chọn tệp PDF bản ký.",
      date: signDate ? undefined : "Hãy chọn ngày ký ghi trên tài liệu.",
    };
    setErrors(found);
    if (!found.file && !found.date) setConfirming(true);
  };

  const submit = async () => {
    if (!pages || !signDate) return;
    setSubmitting(true);
    try {
      const file = await toUpload(pages);
      const problem = tooBig(file);
      if (problem) {
        setErrors({ file: problem });
        setConfirming(false);
        return;
      }
      if (await onSubmit({ file, signDate: format(signDate, "yyyy-MM-dd") })) {
        setConfirming(false);
        setPages(null);
        setSignDate(undefined);
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-5">
      <p className="text-sm text-muted-foreground">
        Bản ký được lưu cho <strong className="text-foreground">phiên bản {version.versionNo}</strong> đã thống nhất. Sau khi
        tải lên, bản ký ở trạng thái chờ kiểm tra; hợp đồng chỉ chuyển sang Đã ký khi được xác nhận hợp lệ.
      </p>
      <div className="space-y-2">
        <Label htmlFor={dateId}>
          Ngày ký trên tài liệu <Required />
        </Label>
        <DateFilter
          id={dateId}
          date={signDate}
          onChange={(next) => {
            setSignDate(next);
            setErrors((current) => ({ ...current, date: undefined }));
          }}
          placeholder="Chọn ngày ký"
          disabled={submitting}
          className="flex-1"
          
        />
        <p className={errors.date ? "text-xs text-destructive" : "text-xs text-muted-foreground"}>
          {errors.date ?? "Ngày ký ghi trên bản giấy, không phải ngày tải tệp lên."}
        </p>
      </div>
      <div className="space-y-2">
        <Label>
          Bản hợp đồng đã ký <Required />
        </Label>
        <ScanPagesPicker
          value={pages}
          onChange={(next) => {
            setPages(next);
            setErrors((current) => ({ ...current, file: undefined }));
          }}
          disabled={submitting}
          error={errors.file}
        />
      </div>
      <Button disabled={submitting} onClick={openConfirm}>
        {submitting ? <Loader2 className="size-4 animate-spin" /> : <FileUp className="size-4" />}
        {pages ? `Tải lên bản ký (${pageCountLabel(pages)})` : "Tải lên bản ký"}
      </Button>
      <ConfirmDialog
        isOpen={confirming}
        onClose={() => setConfirming(false)}
        onConfirm={() => void submit()}
        title="Tải lên bản ký giấy?"
        description={
          <>
            Lưu bản ký ({pages ? pageCountLabel(pages) : ""}) cho phiên bản {version.versionNo}, ngày ký{" "}
            <strong>{signDate ? format(signDate, "dd/MM/yyyy") : "—"}</strong>. Bản ký sẽ chờ kiểm tra chữ ký và dấu.
          </>
        }
        confirmText="Tải lên"
        isLoading={submitting}
      />
    </div>
  );
}

/** F19: a better scan of the same signed paper, or the right file after a wrong one. */
export function SupersedeEvidenceForm({
  evidence,
  onSubmit,
}: {
  evidence: SignedEvidence;
  onSubmit: (input: { file: File; reason: string }) => Promise<boolean>;
}) {
  const [pages, setPages] = useState<ScanPages | null>(null);
  const [reason, setReason] = useState("");
  const [errors, setErrors] = useState<{ file?: string; reason?: string }>({});
  const [confirming, setConfirming] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const reasonId = useId();

  const openConfirm = () => {
    const found = {
      reason: reason.trim() ? undefined : "Hãy nhập lý do thay bản ký.",
      file: pages ? undefined : "Hãy chụp các trang hoặc chọn tệp PDF thay thế.",
    };
    setErrors(found);
    if (!found.file && !found.reason) setConfirming(true);
  };

  const submit = async () => {
    if (!pages) return;
    setSubmitting(true);
    try {
      const file = await toUpload(pages);
      const problem = tooBig(file);
      if (problem) {
        setErrors({ file: problem });
        setConfirming(false);
        return;
      }
      if (await onSubmit({ file, reason: reason.trim() })) {
        setConfirming(false);
        setPages(null);
        setReason("");
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-5">
      <p className="text-sm text-muted-foreground">
        Chỉ dùng khi tải sai tệp hoặc có bản scan rõ hơn của <strong className="text-foreground">cùng</strong> tài liệu đã
        ký. Nếu nội dung thỏa thuận khác, cần lập phiên bản hoặc phụ lục mới. Bản cũ được giữ lại và chỉ bị đánh dấu đã
        thay khi bản mới được xác nhận hợp lệ.
      </p>
      <div className="space-y-2">
        <Label htmlFor={reasonId}>
          Lý do thay bản ký <Required />
        </Label>
        <Textarea
          id={reasonId}
          value={reason}
          maxLength={MAX_REASON_LENGTH}
          disabled={submitting}
          placeholder="Ví dụ: bản trước bị mờ trang có chữ ký..."
          aria-invalid={errors.reason ? true : undefined}
          onChange={(event) => {
            setReason(event.target.value);
            setErrors((current) => ({ ...current, reason: undefined }));
          }}
        />
        {errors.reason && <p className="text-xs text-destructive">{errors.reason}</p>}
      </div>
      <div className="space-y-2">
        <Label>
          Bản ký thay thế <Required />
        </Label>
        <ScanPagesPicker
          value={pages}
          onChange={(next) => {
            setPages(next);
            setErrors((current) => ({ ...current, file: undefined }));
          }}
          disabled={submitting}
          error={errors.file}
        />
        <p className="text-xs text-muted-foreground">Bản mới giữ ngày ký {formatSignDate(evidence.signDate)} của bản giấy.</p>
      </div>
      <Button variant="outline" disabled={submitting} onClick={openConfirm}>
        {submitting ? <Loader2 className="size-4 animate-spin" /> : <FileUp className="size-4" />}
        Tải lên bản thay thế
      </Button>
      <ConfirmDialog
        isOpen={confirming}
        onClose={() => setConfirming(false)}
        onConfirm={() => void submit()}
        title="Tải lên bản ký thay thế?"
        description="Bản thay thế sẽ chờ kiểm tra. Bản đang hợp lệ vẫn giữ nguyên cho tới khi bản mới được xác nhận."
        confirmText="Tải lên"
        isLoading={submitting}
      />
    </div>
  );
}

function CheckRow({
  id,
  checked,
  onChange,
  disabled,
  children,
}: {
  id: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled: boolean;
  children: ReactNode;
}) {
  return (
    <div className="flex items-start gap-3">
      <Checkbox id={id} checked={checked} onCheckedChange={(value) => onChange(value === true)} disabled={disabled} />
      <Label htmlFor={id} className="block leading-snug font-normal">
        <span>{children}</span>
      </Label>
    </div>
  );
}

/** F18 step 4: the business check of signatures and stamps, recorded with whoever did it. */
export function AcceptEvidenceDialog({
  evidence,
  open,
  onOpenChange,
  onSubmit,
}: {
  evidence: SignedEvidence;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (input: { partyASigned: boolean; partyBSigned: boolean; contentMatches: boolean; note: string | null }) => Promise<boolean>;
}) {
  const [checks, setChecks] = useState({ partyA: false, partyB: false, content: false });
  const [note, setNote] = useState("");
  const [error, setError] = useState<string>();
  const [saving, setSaving] = useState(false);
  const baseId = useId();
  const first = evidence.supersedesEvidenceId === null;
  const tick = (key: keyof typeof checks) => (value: boolean) => {
    setChecks((current) => ({ ...current, [key]: value }));
    setError(undefined);
  };

  const submit = async () => {
    if (!checks.partyA || !checks.partyB || !checks.content) {
      setError("Hãy kiểm tra và đánh dấu đủ ba mục.");
      return;
    }
    if (evidence.needsNoteToAccept && !note.trim()) {
      setError("Nội dung đọc được chưa khớp hồ sơ: hãy ghi rõ đã đối chiếu thế nào.");
      return;
    }
    setSaving(true);
    try {
      const saved = await onSubmit({
        partyASigned: checks.partyA,
        partyBSigned: checks.partyB,
        contentMatches: checks.content,
        note: note.trim() || null,
      });
      if (saved) onOpenChange(false);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Xác nhận bản ký hợp lệ</DialogTitle>
          <DialogDescription>
            {first
              ? "Sau khi xác nhận, hệ thống ghi ngày ký, khóa phiên bản và chuyển hợp đồng sang Đã ký."
              : "Sau khi xác nhận, bản ký cũ được đánh dấu đã thay và giữ trong lịch sử."}
          </DialogDescription>
        </DialogHeader>
        {evidence.needsNoteToAccept && (
          <Alert className="border-amber-500/30 bg-amber-500/5">
            <AlertTitle>Nội dung đọc được chưa khớp hồ sơ</AlertTitle>
            <AlertDescription>
              Kiểm tra lại bản giấy với phiên bản đã chốt. Nếu nội dung khác thật, hãy từ chối và xử lý phiên bản trước.
            </AlertDescription>
          </Alert>
        )}
        <div className="space-y-3">
          <CheckRow id={`${baseId}-a`} checked={checks.partyA} disabled={saving} onChange={tick("partyA")}>
            Đã có chữ ký và dấu (nếu cần) của <strong>bên A</strong>
          </CheckRow>
          <CheckRow id={`${baseId}-b`} checked={checks.partyB} disabled={saving} onChange={tick("partyB")}>
            Đã có chữ ký và dấu (nếu cần) của <strong>bên B</strong>
          </CheckRow>
          <CheckRow id={`${baseId}-c`} checked={checks.content} disabled={saving} onChange={tick("content")}>
            Nội dung bản ký khớp <strong>phiên bản {evidence.versionNo}</strong> đã chốt, đủ các trang
          </CheckRow>
        </div>
        <div className="space-y-2">
          <Label htmlFor={`${baseId}-note`}>
            Ghi chú kiểm tra {evidence.needsNoteToAccept && <Required />}
          </Label>
          <Textarea
            id={`${baseId}-note`}
            value={note}
            maxLength={MAX_REASON_LENGTH}
            disabled={saving}
            placeholder="Ví dụ: đã đối chiếu với bản gốc, OCR đọc sai số điện thoại..."
            onChange={(event) => {
              setNote(event.target.value);
              setError(undefined);
            }}
          />
        </div>
        {error && (
          <p role="alert" className="text-sm text-destructive">
            {error}
          </p>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Hủy
          </Button>
          <Button onClick={() => void submit()} disabled={saving}>
            {saving ? <Loader2 className="size-4 animate-spin" /> : <CheckCircle2 className="size-4" />}
            Xác nhận hợp lệ
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export function RejectEvidenceDialog({
  open,
  onOpenChange,
  onSubmit,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (reason: string) => Promise<boolean>;
}) {
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string>();
  const [saving, setSaving] = useState(false);
  const reasonId = useId();

  const submit = async () => {
    if (!reason.trim()) {
      setError("Hãy nhập lý do bản ký không hợp lệ.");
      return;
    }
    setSaving(true);
    try {
      if (await onSubmit(reason.trim())) onOpenChange(false);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Bản ký không hợp lệ</DialogTitle>
          <DialogDescription>
            Bản ký được giữ lại kèm lý do; hợp đồng vẫn chờ ký và có thể tải bản ký khác.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-2">
          <Label htmlFor={reasonId}>
            Lý do <Required />
          </Label>
          <Textarea
            id={reasonId}
            value={reason}
            maxLength={MAX_REASON_LENGTH}
            disabled={saving}
            placeholder="Ví dụ: thiếu dấu của bên A, thiếu trang 3..."
            onChange={(event) => {
              setReason(event.target.value);
              setError(undefined);
            }}
          />
          {error && <p className="text-xs text-destructive">{error}</p>}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Hủy
          </Button>
          <Button variant="destructive" onClick={() => void submit()} disabled={saving}>
            {saving ? <Loader2 className="size-4 animate-spin" /> : <XCircle className="size-4" />}
            Không hợp lệ
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
