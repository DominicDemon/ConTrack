"use client";

import Link from "next/link";
import { useCallback, useEffect, useId, useMemo, useState, type FormEvent, type ReactNode } from "react";
import { format } from "date-fns";
import { ArrowLeft, CheckCircle2, ChevronDown, Download, Eye, FilePlus2, Loader2, RefreshCw, Search } from "lucide-react";
import { useScanAccess } from "@/components/internal/scan-access";
import { blobErrorMessage, FileKeyCancelled, useFileKey } from "@/components/internal/file-key";
import {
  formatDateTime,
  formatFileSize,
  formatKeywordValue,
  formatMoney,
  KEYWORD_TYPE_LABELS,
  KEYWORD_TYPES,
  keywordErrorMessage,
} from "@/components/signed-scans/format";
import { ScanKeywordsSection } from "@/components/signed-scans/scan-keywords-section";
import { StatusBadge } from "@/components/signed-scans/status-badge";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/sonner";
import { SCANNED_PERMISSIONS } from "@/services/scans/auth";
import { getScanErrorMessage } from "@/services/scans/http";
import {
  OCR_JOB_STATUS_LABELS,
  scannedContractsApi,
  type ContractTypeOption,
  type CustomerOption,
  type ScannedContract,
  type ScannedJobDetail,
} from "@/services/scans/scanned-contracts";

const POLL_MS = 2000;

const formatDay = (value: string | null) => (value ? format(new Date(`${value}T00:00:00`), "dd/MM/yyyy") : "—");

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-4 border-b py-2 text-sm last:border-b-0">
      <span className="text-muted-foreground">{label}</span>
      <span className="min-w-0 text-right font-medium break-words">{children}</span>
    </div>
  );
}

function Required() {
  return <span className="text-destructive">*</span>;
}

/** What OCR read on the paper, grouped by kind. */
function ReadKeywords({ job, onRetry, canRetry }: { job: ScannedJobDetail; onRetry: () => Promise<void>; canRetry: boolean }) {
  const [retrying, setRetrying] = useState(false);
  const status = OCR_JOB_STATUS_LABELS[job.jobStatus];
  const reading = job.jobStatus === "QUEUED" || job.jobStatus === "PROCESSING";

  return (
    <Card>
      <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-2">
        <CardTitle>Hệ thống đọc được</CardTitle>
        <StatusBadge tone={status.tone}>
          {reading && <Loader2 className="mr-1 size-3 animate-spin" />}
          {status.label}
        </StatusBadge>
      </CardHeader>
      <CardContent className="space-y-3">
        {reading && <p className="text-sm text-muted-foreground">Đang đọc nội dung bản scan, vui lòng chờ…</p>}
        {job.jobStatus === "FAILED" && (
          <Alert variant="destructive">
            <AlertTitle>Không đọc được nội dung</AlertTitle>
            <AlertDescription className="space-y-2">
              <p>
                {keywordErrorMessage(job.keywords.lastErrorCode)}. Bản scan vẫn được lưu; người kiểm tra xem tệp bằng mắt và
                tự nhập thông tin.
              </p>
              {canRetry && (
                <Button
                  variant="outline"
                  size="sm"
                  disabled={retrying}
                  onClick={async () => {
                    setRetrying(true);
                    try {
                      await onRetry();
                    } finally {
                      setRetrying(false);
                    }
                  }}
                >
                  {retrying ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />}
                  Đọc lại
                </Button>
              )}
            </AlertDescription>
          </Alert>
        )}
        {job.jobStatus === "NEED_REVIEW" && (
          <p className="text-sm text-amber-700 dark:text-amber-400">
            Không đọc được đủ cả bốn loại thông tin. Hãy xem kỹ bản scan và nhập phần còn thiếu.
          </p>
        )}
        {!reading && job.jobStatus !== "FAILED" && (
          <dl className="divide-y rounded-lg border">
            {KEYWORD_TYPES.map((type) => {
              const values = job.keywords.items.filter((item) => item.type === type);
              return (
                <div key={type} className="grid grid-cols-[8rem_minmax(0,1fr)] gap-2 px-3 py-2 text-sm">
                  <dt className="text-muted-foreground">{KEYWORD_TYPE_LABELS[type]}</dt>
                  <dd className="break-words">
                    {values.length === 0
                      ? "—"
                      : values.map((item) => `${formatKeywordValue(type, item.value)}${item.page ? ` (tr. ${item.page})` : ""}`).join(" · ")}
                  </dd>
                </div>
              );
            })}
          </dl>
        )}
        <p className="text-xs text-muted-foreground">
          Thông tin đọc tự động có thể sai. Người có quyền tải bản scan sửa được từ khóa.
        </p>
      </CardContent>
    </Card>
  );
}

function CustomerPicker({
  value,
  onChange,
  suggested,
  disabled,
  error,
}: {
  value: CustomerOption | null;
  onChange: (customer: CustomerOption | null) => void;
  suggested: CustomerOption[];
  disabled: boolean;
  error?: string;
}) {
  const [query, setQuery] = useState("");
  const [found, setFound] = useState<CustomerOption[] | null>(null);
  const [searching, setSearching] = useState(false);
  const inputId = useId();

  const search = async () => {
    setSearching(true);
    try {
      setFound(await scannedContractsApi.findCustomers(query));
    } catch (searchError) {
      toast.error(getScanErrorMessage(searchError, "Không tìm được khách hàng."));
    } finally {
      setSearching(false);
    }
  };

  const options = found ?? suggested;
  return (
    <div className="space-y-2">
      <Label htmlFor={inputId}>
        Khách hàng <Required />
      </Label>
      {value ? (
        <div className="flex items-center justify-between gap-2 rounded-lg border px-3 py-2 text-sm">
          <span className="min-w-0">
            <span className="font-medium">{value.name}</span>
            <span className="block text-muted-foreground">
              {[value.taxCode && `MST ${value.taxCode}`, value.phoneNumber].filter(Boolean).join(" · ") || "—"}
            </span>
          </span>
          <Button type="button" variant="outline" size="sm" disabled={disabled} onClick={() => onChange(null)}>
            Đổi
          </Button>
        </div>
      ) : (
        <>
          <div className="flex gap-2">
            <Input
              id={inputId}
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter") {
                  event.preventDefault();
                  void search();
                }
              }}
              placeholder="Tên, mã số thuế hoặc số điện thoại"
              disabled={disabled}
              aria-invalid={error ? true : undefined}
            />
            <Button type="button" variant="outline" onClick={() => void search()} disabled={disabled || searching}>
              {searching ? <Loader2 className="size-4 animate-spin" /> : <Search className="size-4" />}
              Tìm
            </Button>
          </div>
          {!found && suggested.length > 0 && (
            <p className="text-xs text-muted-foreground">Khách hàng có mã số thuế hoặc số điện thoại khớp với bản scan:</p>
          )}
          {options.length > 0 ? (
            <ul className="divide-y rounded-lg border">
              {options.map((option) => (
                <li key={option.customerId}>
                  <button
                    type="button"
                    className="w-full px-3 py-2 text-left text-sm hover:bg-accent/50 disabled:opacity-50"
                    disabled={disabled}
                    onClick={() => onChange(option)}
                  >
                    <span className="font-medium">{option.name}</span>
                    <span className="block text-muted-foreground">
                      {[option.taxCode && `MST ${option.taxCode}`, option.phoneNumber].filter(Boolean).join(" · ") || "—"}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          ) : (
            found && <p className="text-sm text-muted-foreground">Không tìm thấy khách hàng nào.</p>
          )}
        </>
      )}
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  );
}

interface FormState {
  contractCode: string;
  contractName: string;
  customer: CustomerOption | null;
  contractTypeId: string;
  signedDate: string;
  effectiveDate: string;
  expirationDate: string;
  contractValue: string;
  currency: string;
  description: string;
}

/** The checker settles the details on the paper and creates the CONTRACTS row (source_type SCANNED). */
function ConfirmForm({ job, onConfirmed }: { job: ScannedJobDetail; onConfirmed: (job: ScannedJobDetail) => void }) {
  const [types, setTypes] = useState<ContractTypeOption[]>([]);
  const [form, setForm] = useState<FormState>(() => ({
    contractCode: job.suggestion.contractCode ?? "",
    contractName: "",
    customer: job.suggestion.matchedCustomers.length === 1 ? job.suggestion.matchedCustomers[0] : null,
    contractTypeId: "",
    signedDate: "",
    effectiveDate: "",
    expirationDate: "",
    contractValue: "",
    currency: "VND",
    description: "",
  }));
  const [errors, setErrors] = useState<Partial<Record<keyof FormState, string>>>({});
  const [submitting, setSubmitting] = useState(false);
  const ids = { code: useId(), name: useId(), type: useId(), signed: useId(), effective: useId(), expiration: useId(), value: useId(), currency: useId(), description: useId() };

  useEffect(() => {
    scannedContractsApi.contractTypes().then(setTypes, () => setTypes([]));
  }, []);

  const set = <K extends keyof FormState>(key: K, value: FormState[K]) => {
    setForm((current) => ({ ...current, [key]: value }));
    setErrors((current) => ({ ...current, [key]: undefined }));
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const value = form.contractValue.trim() === "" ? null : Number(form.contractValue.replace(/[.\s]/g, "").replace(",", "."));
    const found: Partial<Record<keyof FormState, string>> = {
      contractCode: form.contractCode.trim() ? undefined : "Hãy nhập số hợp đồng.",
      contractName: form.contractName.trim() ? undefined : "Hãy nhập tên hợp đồng.",
      customer: form.customer ? undefined : "Hãy chọn khách hàng.",
      signedDate: form.signedDate ? undefined : "Hãy nhập ngày ký ghi trên hợp đồng.",
      expirationDate:
        form.effectiveDate && form.expirationDate && form.expirationDate < form.effectiveDate
          ? "Ngày hết hạn phải sau ngày hiệu lực."
          : undefined,
      contractValue: value !== null && (!Number.isFinite(value) || value < 0) ? "Giá trị phải là số không âm." : undefined,
    };
    setErrors(found);
    if (Object.values(found).some(Boolean) || !form.customer) return;

    setSubmitting(true);
    try {
      const next = await scannedContractsApi.confirm(job.jobId, {
        contractCode: form.contractCode.trim(),
        contractName: form.contractName.trim(),
        customerId: form.customer.customerId,
        contractTypeId: form.contractTypeId ? Number(form.contractTypeId) : null,
        signedDate: form.signedDate,
        effectiveDate: form.effectiveDate || null,
        expirationDate: form.expirationDate || null,
        contractValue: value,
        currency: form.currency.trim() || null,
        description: form.description.trim() || null,
      });
      toast.success("Đã tạo hợp đồng từ bản scan.");
      onConfirmed(next);
    } catch (confirmError) {
      toast.error(getScanErrorMessage(confirmError, "Không tạo được hợp đồng."));
    } finally {
      setSubmitting(false);
    }
  };

  const hint = (key: keyof FormState) => errors[key] && <p className="text-xs text-destructive">{errors[key]}</p>;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Tạo hợp đồng từ bản scan</CardTitle>
        <CardDescription>
          Không bắt buộc: bản scan đã được lưu và tìm được. Tạo hợp đồng khi muốn quản lý nó cùng các hợp đồng khác (khách
          hàng, ngày hiệu lực, giá trị).
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} noValidate className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor={ids.code}>
                Số hợp đồng <Required />
              </Label>
              <Input id={ids.code} value={form.contractCode} onChange={(e) => set("contractCode", e.target.value)} disabled={submitting} />
              {hint("contractCode")}
            </div>
            <div className="space-y-2">
              <Label htmlFor={ids.type}>Loại hợp đồng</Label>
              <select
                id={ids.type}
                className="h-9 w-full rounded-md border bg-transparent px-3 text-sm"
                value={form.contractTypeId}
                onChange={(e) => set("contractTypeId", e.target.value)}
                disabled={submitting || types.length === 0}
              >
                <option value="">{types.length === 0 ? "Chưa có loại hợp đồng" : "Không chọn"}</option>
                {types.map((type) => (
                  <option key={type.contractTypeId} value={type.contractTypeId}>
                    {type.name}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <div className="space-y-2">
            <Label htmlFor={ids.name}>
              Tên hợp đồng <Required />
            </Label>
            <Input id={ids.name} value={form.contractName} onChange={(e) => set("contractName", e.target.value)} disabled={submitting} />
            {hint("contractName")}
          </div>
          <CustomerPicker
            value={form.customer}
            onChange={(customer) => set("customer", customer)}
            suggested={job.suggestion.matchedCustomers}
            disabled={submitting}
            error={errors.customer}
          />
          {job.suggestion.customerName && (
            <p className="text-xs text-muted-foreground">Tên trên bản scan: {job.suggestion.customerName}</p>
          )}
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="space-y-2">
              <Label htmlFor={ids.signed}>
                Ngày ký <Required />
              </Label>
              <Input id={ids.signed} type="date" value={form.signedDate} onChange={(e) => set("signedDate", e.target.value)} disabled={submitting} />
              {hint("signedDate")}
            </div>
            <div className="space-y-2">
              <Label htmlFor={ids.effective}>Ngày hiệu lực</Label>
              <Input id={ids.effective} type="date" value={form.effectiveDate} onChange={(e) => set("effectiveDate", e.target.value)} disabled={submitting} />
            </div>
            <div className="space-y-2">
              <Label htmlFor={ids.expiration}>Ngày hết hạn</Label>
              <Input id={ids.expiration} type="date" value={form.expirationDate} onChange={(e) => set("expirationDate", e.target.value)} disabled={submitting} />
              {hint("expirationDate")}
            </div>
          </div>
          <div className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_8rem]">
            <div className="space-y-2">
              <Label htmlFor={ids.value}>Giá trị hợp đồng</Label>
              <Input id={ids.value} inputMode="decimal" value={form.contractValue} onChange={(e) => set("contractValue", e.target.value)} placeholder="VD: 1500000000" disabled={submitting} />
              {hint("contractValue")}
            </div>
            <div className="space-y-2">
              <Label htmlFor={ids.currency}>Tiền tệ</Label>
              <Input id={ids.currency} value={form.currency} onChange={(e) => set("currency", e.target.value)} disabled={submitting} />
            </div>
          </div>
          <div className="space-y-2">
            <Label htmlFor={ids.description}>Ghi chú</Label>
            <Textarea id={ids.description} value={form.description} onChange={(e) => set("description", e.target.value)} rows={3} disabled={submitting} />
          </div>
          <Button type="submit" disabled={submitting}>
            {submitting ? <Loader2 className="size-4 animate-spin" /> : <CheckCircle2 className="size-4" />}
            Tạo hợp đồng
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

function CreatedContract({ contract }: { contract: ScannedContract }) {
  return (
    <Card className="border-emerald-500/30">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <CheckCircle2 className="size-5 text-emerald-600" />
          Đã tạo hợp đồng {contract.contractCode}
        </CardTitle>
        <CardDescription>
          Tạo bởi {contract.createdBy} lúc {formatDateTime(contract.createdAt)}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Row label="Tên hợp đồng">{contract.contractName}</Row>
        <Row label="Khách hàng">{contract.customerName ?? "—"}</Row>
        <Row label="Loại hợp đồng">{contract.contractTypeName ?? "—"}</Row>
        <Row label="Ngày ký">{formatDay(contract.signedDate)}</Row>
        <Row label="Hiệu lực">
          {formatDay(contract.effectiveDate)} – {formatDay(contract.expirationDate)}
        </Row>
        <Row label="Giá trị">
          {contract.contractValue != null ? `${formatMoney(contract.contractValue)}${contract.currency && contract.currency !== "VND" ? ` (${contract.currency})` : ""}` : "—"}
        </Row>
        {contract.description && <Row label="Ghi chú">{contract.description}</Row>}
      </CardContent>
    </Card>
  );
}

/** Shown once OCR is done: the scan is kept and searchable; making a contract from it is optional. */
function SavedNotice({ job }: { job: ScannedJobDetail }) {
  const count = job.keywords.items.length;
  return (
    <Card className="border-emerald-500/30">
      <CardContent className="flex gap-3 py-5 text-sm">
        <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-emerald-600" />
        <div className="space-y-1">
          <p className="font-semibold">Đã lưu bản scan</p>
          <p className="text-muted-foreground">
            {count > 0
              ? `Tìm được bằng ${count} từ khóa ở mục Tìm hợp đồng. Nếu hệ thống đọc sai, sửa ở phần từ khóa.`
              : "Chưa có từ khóa nào nên chưa tìm được. Hãy thêm từ khóa bằng tay."}
          </p>
        </div>
      </CardContent>
    </Card>
  );
}

/** One scan of an old paper contract: the file, what OCR read and, optionally, a contract made from it. */
export function ScannedJobPage({ jobId }: { jobId: string }) {
  const { can } = useScanAccess();
  const { ensureKey, withFileKey } = useFileKey();
  const [job, setJob] = useState<ScannedJobDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [opening, setOpening] = useState(false);
  const [creating, setCreating] = useState(false);

  const load = useCallback(async () => {
    try {
      setJob(await scannedContractsApi.getJob(jobId));
      setError(null);
    } catch (loadError) {
      setError(getScanErrorMessage(loadError, "Không tải được bản scan."));
    }
  }, [jobId]);

  useEffect(() => {
    void load();
  }, [load]);

  const reading = job?.jobStatus === "QUEUED" || job?.jobStatus === "PROCESSING";
  useEffect(() => {
    if (!reading) return;
    const timer = window.setInterval(() => void load(), POLL_MS);
    return () => window.clearInterval(timer);
  }, [reading, load]);

  const retry = async () => {
    if (!job) return;
    try {
      setJob(await withFileKey((key) => scannedContractsApi.retry(job.jobId, key)));
    } catch (retryError) {
      if (!(retryError instanceof FileKeyCancelled)) toast.error(getScanErrorMessage(retryError, "Không đọc lại được."));
    }
  };

  const isPdf = useMemo(() => job?.contentType === "application/pdf", [job]);

  const open = async (download: boolean) => {
    if (!job) return;
    let preview: Window | null = null;
    setOpening(true);
    try {
      // The key is asked first, so the new tab is still opened from the click that gave it.
      if (isPdf && !download) {
        await ensureKey();
        preview = window.open("about:blank", "_blank");
      }
      const blob = await withFileKey((key) => scannedContractsApi.jobFile(job.jobId, key), {
        beforeAsk: () => preview?.close(),
        afterAsk: () => {
          if (isPdf && !download) preview = window.open("about:blank", "_blank");
        },
      });
      const url = URL.createObjectURL(new Blob([blob], { type: job.contentType }));
      if (preview) {
        preview.opener = null;
        preview.location.replace(url);
      } else {
        const link = document.createElement("a");
        link.href = url;
        link.download = job.fileName;
        link.click();
      }
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (openError) {
      preview?.close();
      if (!(openError instanceof FileKeyCancelled)) toast.error(await blobErrorMessage(openError, "Không mở được tệp."));
    } finally {
      setOpening(false);
    }
  };

  return (
    <>
      <div>
        <Button asChild variant="ghost" className="mb-2 -ml-3 text-muted-foreground">
          <Link href="/workspace/scanned-contracts">
            <ArrowLeft className="mr-2 size-4" />
            Quay lại hợp đồng giấy scan
          </Link>
        </Button>
        <h1 className="text-2xl font-bold tracking-tight break-all text-foreground">{job?.fileName ?? `Bản scan #${jobId}`}</h1>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertTitle>Không tải được</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {!job && !error && <Skeleton className="h-64 w-full" />}

      {job && (
        <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(0,1.2fr)]">
          <div className="space-y-6">
            <Card>
              <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-2">
                <CardTitle>Bản scan</CardTitle>
                <div className="flex gap-2">
                  {isPdf && (
                    <Button variant="outline" size="sm" disabled={opening} onClick={() => void open(false)}>
                      {opening ? <Loader2 className="size-4 animate-spin" /> : <Eye className="size-4" />}
                      Xem
                    </Button>
                  )}
                  <Button variant="outline" size="sm" disabled={opening} onClick={() => void open(true)}>
                    <Download className="size-4" />
                    Tải về
                  </Button>
                </div>
              </CardHeader>
              <CardContent>
                <Row label="Lượt scan">#{job.batchId}</Row>
                <Row label="Kích thước">{formatFileSize(job.fileSizeBytes)}</Row>
                <Row label="Tải lên">
                  {job.uploadedBy} · {formatDateTime(job.uploadedAt)}
                </Row>
                <Row label="SHA-256">
                  <span className="font-mono text-xs break-all">{job.sha256}</span>
                </Row>
              </CardContent>
            </Card>
            {can(SCANNED_PERMISSIONS.upload) ? (
              <ScanKeywordsSection
                keywords={job.keywords}
                onRetry={retry}
                onAddKeyword={async (input) => {
                  try {
                    await scannedContractsApi.addKeyword(job.jobId, input);
                    toast.success("Đã thêm từ khóa. Bản scan tìm được bằng từ khóa này ngay.");
                    await load();
                    return true;
                  } catch (addError) {
                    toast.error(getScanErrorMessage(addError, "Không thêm được từ khóa."));
                    return false;
                  }
                }}
                onRemoveKeyword={async (keyword) => {
                  try {
                    await scannedContractsApi.removeKeyword(job.jobId, keyword.keywordId);
                    await load();
                  } catch (removeError) {
                    toast.error(getScanErrorMessage(removeError, "Không xóa được từ khóa."));
                  }
                }}
              />
            ) : (
              <ReadKeywords job={job} canRetry={false} onRetry={retry} />
            )}
          </div>
          <div className="space-y-6">
            {job.contract ? (
              <CreatedContract contract={job.contract} />
            ) : reading ? (
              <Card>
                <CardContent className="flex items-center gap-2 py-8 text-sm text-muted-foreground">
                  <Loader2 className="size-4 animate-spin" />
                  Đang đọc bản scan. Đọc xong là tìm được ngay.
                </CardContent>
              </Card>
            ) : (
              <>
                <SavedNotice job={job} />
                {can(SCANNED_PERMISSIONS.confirm) &&
                  (creating ? (
                    <ConfirmForm job={job} onConfirmed={setJob} />
                  ) : (
                    <Button variant="outline" onClick={() => setCreating(true)}>
                      <FilePlus2 className="size-4" />
                      Tạo hợp đồng từ bản scan (không bắt buộc)
                      <ChevronDown className="size-4" />
                    </Button>
                  ))}
              </>
            )}
          </div>
        </div>
      )}
    </>
  );
}
