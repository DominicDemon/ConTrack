"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState, type ChangeEvent, type ReactNode } from "react";
import { format } from "date-fns";
import { Camera, Copy, Download, Eye, FileSearch, FileUp, Loader2, MoreHorizontal, ScanLine, Search, Tag, Trash2, Upload, X } from "lucide-react";
import { useScanAccess } from "@/components/internal/scan-access";
import { formatDateTime, formatFileSize, formatMoney } from "@/components/signed-scans/format";
import { imagesToPdf } from "@/components/signed-scans/images-to-pdf";
import { MAX_SCAN_BYTES, pageCountLabel, ScanPagesPicker, type ScanPages } from "@/components/signed-scans/scan-pages-picker";
import type { StatusTone } from "@/components/signed-scans/format";
import { StatusBadge } from "@/components/signed-scans/status-badge";
import { ScanFileButtons, useScanFile } from "./scan-file-buttons";
import { ListPager } from "./list-pager";
import { JobLabelsDialog, LabelChip, LabelFilter, ManageLabelsDialog } from "./scan-labels";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { toast } from "@/components/ui/sonner";
import { SCANNED_PERMISSIONS } from "@/services/scans/auth";
import { getScanErrorMessage } from "@/services/scans/http";
import {
  OCR_JOB_STATUS_LABELS,
  scannedContractsApi,
  type ScannedBatch,
  type ScannedBatchFilter,
  type ScannedBatchList,
  type ScannedBatchSummary,
  type ScannedContract,
  type ScannedJobSummary,
  type ScanLabel,
  type ScanPage,
} from "@/services/scans/scanned-contracts";

const MAX_FILES = 20;
const POLL_MS = 3000;
const BATCHES_PER_PAGE = 10;
const CONTRACTS_PER_PAGE = 20;

export const scannedJobHref = (jobId: string) => `/workspace/scanned-contracts/jobs/${jobId}`;

const formatDay = (value: string | null) => (value ? format(new Date(`${value}T00:00:00`), "dd/MM/yyyy") : "—");

/** Photos of one contract taken with the camera, joined into one PDF and added to the files to upload. */
function CameraDialog({ open, onOpenChange, onAdd }: { open: boolean; onOpenChange: (open: boolean) => void; onAdd: (file: File) => void }) {
  const [pages, setPages] = useState<ScanPages | null>(null);
  const [building, setBuilding] = useState(false);

  const add = async () => {
    if (!pages) return;
    setBuilding(true);
    try {
      const file =
        pages.kind === "pdf"
          ? pages.file
          : await imagesToPdf(
              pages.pages.map((page) => page.photo),
              `hop-dong-scan-${format(new Date(), "yyyyMMdd-HHmmss")}.pdf`,
            );
      onAdd(file);
      setPages(null);
      onOpenChange(false);
    } catch (error) {
      toast.error(getScanErrorMessage(error, "Không ghép được ảnh thành PDF."));
    } finally {
      setBuilding(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>Chụp một hợp đồng bằng camera</DialogTitle>
          <DialogDescription>
            Chụp lần lượt từng trang của cùng một hợp đồng. Các trang được ghép thành một tệp PDF.
          </DialogDescription>
        </DialogHeader>
        <ScanPagesPicker value={pages} onChange={setPages} disabled={building} />
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={building}>
            Hủy
          </Button>
          <Button onClick={() => void add()} disabled={!pages || building}>
            {building && <Loader2 className="size-4 animate-spin" />}
            Thêm vào danh sách{pages ? ` (${pageCountLabel(pages)})` : ""}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function UploadCard({ onUploaded }: { onUploaded: (batch: ScannedBatch) => void }) {
  const [files, setFiles] = useState<File[]>([]);
  const [problem, setProblem] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [camera, setCamera] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  const add = (more: File[]) => {
    setProblem(null);
    const next = [...files, ...more];
    const tooBig = next.find((file) => file.size > MAX_SCAN_BYTES);
    if (next.length > MAX_FILES) setProblem(`Mỗi lượt tải tối đa ${MAX_FILES} tệp.`);
    else if (tooBig) setProblem(`${tooBig.name} vượt quá giới hạn 20 MB của máy chủ.`);
    else if (next.some((file) => file.size === 0)) setProblem("Có tệp không có nội dung.");
    else setFiles(next);
  };

  const picked = (event: ChangeEvent<HTMLInputElement>) => {
    const chosen = Array.from(event.target.files ?? []);
    event.target.value = "";
    if (chosen.length > 0) add(chosen);
  };

  const upload = async () => {
    setUploading(true);
    try {
      const batch = await scannedContractsApi.uploadBatch(files);
      toast.success(`Đã tải lên ${batch.totalFiles} bản scan. Hệ thống đang đọc nội dung.`);
      setFiles([]);
      onUploaded(batch);
    } catch (error) {
      setProblem(getScanErrorMessage(error, "Không tải lên được. Vui lòng thử lại."));
    } finally {
      setUploading(false);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <FileUp className="size-5 text-primary" />
          Tải bản scan hợp đồng giấy
        </CardTitle>
        <CardDescription>
          Hợp đồng giấy cũ chưa có trên hệ thống: scan bằng máy scan rồi chọn tệp PDF, hoặc chụp bằng camera. Mỗi tệp là một
          hợp đồng; tải nhiều tệp một lượt được.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-wrap gap-2">
          <Button type="button" onClick={() => inputRef.current?.click()} disabled={uploading}>
            <FileUp className="size-4" />
            Chọn tệp scan
          </Button>
          <Button type="button" variant="outline" onClick={() => setCamera(true)} disabled={uploading}>
            <Camera className="size-4" />
            Chụp bằng camera
          </Button>
        </div>
        <input ref={inputRef} type="file" multiple className="sr-only" tabIndex={-1} aria-hidden="true" onChange={picked} />

        {files.length > 0 && (
          <ul className="divide-y rounded-lg border">
            {files.map((file, index) => (
              <li key={`${file.name}-${index}`} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
                <span className="min-w-0 truncate">{file.name}</span>
                <span className="flex shrink-0 items-center gap-2 text-muted-foreground">
                  {formatFileSize(file.size)}
                  <Button
                    variant="ghost"
                    size="icon"
                    className="size-7"
                    aria-label={`Bỏ ${file.name}`}
                    disabled={uploading}
                    onClick={() => setFiles(files.filter((_, at) => at !== index))}
                  >
                    <X className="size-4" />
                  </Button>
                </span>
              </li>
            ))}
          </ul>
        )}
        {problem && (
          <p role="alert" className="text-sm text-destructive">
            {problem}
          </p>
        )}
        <Button onClick={() => void upload()} disabled={files.length === 0 || uploading}>
          {uploading ? <Loader2 className="size-4 animate-spin" /> : <Upload className="size-4" />}
          Tải lên {files.length > 0 ? `${files.length} tệp` : ""}
        </Button>
      </CardContent>
      <CameraDialog open={camera} onOpenChange={setCamera} onAdd={(file) => add([file])} />
    </Card>
  );
}

/** One label per batch, from its files: what is left to do, not the raw batch status. */
function batchState(batch: ScannedBatch): { label: string; tone: StatusTone } {
  const reading = batch.jobs.filter((job) => job.jobStatus === "QUEUED" || job.jobStatus === "PROCESSING").length;
  const failed = batch.jobs.filter((job) => job.jobStatus === "FAILED").length;
  const review = batch.jobs.filter((job) => job.jobStatus === "NEED_REVIEW").length;
  if (reading > 0) return { label: `Đang đọc ${batch.jobs.length - reading}/${batch.jobs.length}`, tone: "info" };
  if (failed > 0) return { label: `${failed} tệp không đọc được`, tone: "danger" };
  if (review > 0) return { label: `${review} tệp cần kiểm tra`, tone: "pending" };
  return { label: "Đọc xong", tone: "success" };
}

/** Deletes a scan after a confirmation that names the file; the list reloads when it is gone. */
function DeleteJobDialog({ jobId, fileName, open, onOpenChange, onDeleted }: {
  jobId: string;
  fileName: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onDeleted: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const remove = async () => {
    setBusy(true);
    try {
      await scannedContractsApi.deleteJob(jobId);
      toast.success(`Đã xóa bản scan ${fileName}.`);
      onOpenChange(false);
      onDeleted();
    } catch (error) {
      toast.error(getScanErrorMessage(error, "Không xóa được bản scan."));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Dialog open={open} onOpenChange={(next) => !busy && onOpenChange(next)}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Xóa bản scan?</DialogTitle>
          <DialogDescription>
            Tệp <strong className="[overflow-wrap:anywhere]">{fileName}</strong> và mọi từ khóa đọc được sẽ bị xóa hẳn, không khôi phục
            được. Chỉ nên xóa bản tải nhầm, bản thử hoặc bản không đọc được.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
            Hủy
          </Button>
          <Button variant="destructive" onClick={() => void remove()} disabled={busy}>
            {busy && <Loader2 className="size-4 animate-spin" />}
            Xóa bản scan
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** What the rows need to show and change labels. */
interface RowLabels {
  all: ScanLabel[];
  /** May put labels on scans (contract.scanned.upload). */
  canLabel: boolean;
  /** A click on a label chip filters the list by it. */
  onPick: (labelId: string) => void;
  /** A label was created: reload the label list. */
  onLabelsChanged: () => void;
}

/** One scan: what OCR read, its size, labels, possible duplicates; "Xem" first, the rest in the "⋯" menu. */
function JobRow({ job, canDelete, onDeleted, labels }: {
  job: ScannedJobSummary;
  canDelete: boolean;
  onDeleted: () => void;
  labels: RowLabels;
}) {
  const file = useScanFile({ load: (key) => scannedContractsApi.jobFile(job.jobId, key), fileName: job.fileName });
  const [deleting, setDeleting] = useState(false);
  const [labelling, setLabelling] = useState(false);
  const status = OCR_JOB_STATUS_LABELS[job.jobStatus];
  const reading = job.jobStatus === "QUEUED" || job.jobStatus === "PROCESSING";
  // Not while OCR reads it, and not once a contract was made from it (the server refuses both too).
  const deletable = canDelete && !reading && !job.contractId;
  const facts = [job.pageCount ? `${job.pageCount} trang` : null, formatFileSize(job.fileSizeBytes)].filter(Boolean);
  const found: [string, string][] = [];
  if (job.foundContractCode) found.push(["Số HĐ", job.foundContractCode]);
  if (job.foundTaxCode) found.push(["MST", job.foundTaxCode]);
  if (job.foundCustomerName) found.push(["Bên", job.foundCustomerName]);

  return (
    <li className="flex flex-col gap-2 px-3 py-2.5 text-sm sm:flex-row sm:items-start sm:justify-between">
      <div className="min-w-0 space-y-1">
        <Link href={scannedJobHref(job.jobId)} className="block truncate font-medium hover:underline">
          {job.fileName}
        </Link>
        <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">
          {facts.join(" · ")}
          {found.map(([label, value]) => (
            <span key={label}>
              {" · "}
              {label}: <span className="text-foreground">{value}</span>
            </span>
          ))}
          {!reading && found.length === 0 && " · Chưa đọc được từ khóa nào"}
        </p>
        {job.labels.length > 0 && (
          <div className="flex flex-wrap gap-1.5">
            {job.labels.map((label) => (
              <LabelChip key={label.labelId} label={label} onClick={() => labels.onPick(label.labelId)} />
            ))}
          </div>
        )}
        {(job.sameFileAs || job.sameContractCodeCount > 0) && (
          <div className="flex flex-wrap gap-1.5">
            {job.sameFileAs && (
              <StatusBadge tone="pending">
                <Copy className="mr-1 size-3" />
                Trùng nội dung với {job.sameFileAs}
              </StatusBadge>
            )}
            {job.sameContractCodeCount > 0 && (
              <StatusBadge tone="neutral">Cùng số HĐ với {job.sameContractCodeCount} bản khác</StatusBadge>
            )}
          </div>
        )}
      </div>
      <div className="flex shrink-0 flex-wrap items-center gap-2">
        <StatusBadge tone={status.tone}>
          {reading && <Loader2 className="mr-1 size-3 animate-spin" />}
          {status.label}
        </StatusBadge>
        {job.contractId && <StatusBadge tone="success">Đã lập hồ sơ {job.contractCode}</StatusBadge>}
        <Button variant="outline" size="sm" disabled={file.opening} onClick={() => void file.open(!file.viewable)}>
          {file.opening ? (
            <Loader2 className="size-4 animate-spin" />
          ) : file.viewable ? (
            <Eye className="size-4" />
          ) : (
            <Download className="size-4" />
          )}
          {file.viewable ? "Xem" : "Tải về"}
        </Button>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" aria-label={`Thao tác khác với ${job.fileName}`}>
              <MoreHorizontal className="size-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            {file.viewable && (
              <DropdownMenuItem disabled={file.opening} onSelect={() => void file.open(true)}>
                <Download className="size-4" />
                Tải về
              </DropdownMenuItem>
            )}
            {labels.canLabel && (
              <DropdownMenuItem onSelect={() => setLabelling(true)}>
                <Tag className="size-4" />
                Gắn nhãn
              </DropdownMenuItem>
            )}
            <DropdownMenuItem asChild>
              <Link href={scannedJobHref(job.jobId)}>
                <FileSearch className="size-4" />
                Chi tiết, sửa từ khóa
              </Link>
            </DropdownMenuItem>
            {deletable && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem className="text-destructive focus:text-destructive" onSelect={() => setDeleting(true)}>
                  <Trash2 className="size-4" />
                  Xóa
                </DropdownMenuItem>
              </>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
      {labels.canLabel && (
        <JobLabelsDialog
          jobId={job.jobId}
          fileName={job.fileName}
          current={job.labels}
          labels={labels.all}
          open={labelling}
          onOpenChange={setLabelling}
          onSaved={onDeleted}
          onLabelsChanged={labels.onLabelsChanged}
        />
      )}
      {deletable && (
        <DeleteJobDialog jobId={job.jobId} fileName={job.fileName} open={deleting} onOpenChange={setDeleting} onDeleted={onDeleted} />
      )}
    </li>
  );
}

function BatchList({ batches, filtered, canDelete, onDeleted, labels }: {
  batches: ScannedBatch[];
  filtered: boolean;
  canDelete: boolean;
  /** Something on a row changed (deleted, labelled): reload the list. */
  onDeleted: () => void;
  labels: RowLabels;
}) {
  if (batches.length === 0) {
    return (
      <p className="py-8 text-center text-sm text-muted-foreground">
        {filtered ? "Không có bản scan nào khớp bộ lọc." : "Chưa có lượt scan nào."}
      </p>
    );
  }
  return (
    <div className="space-y-4">
      {batches.map((batch) => {
        const state = batchState(batch);
        const shown = batch.jobs.length < batch.totalFiles ? `${batch.jobs.length}/${batch.totalFiles} tệp khớp` : `${batch.totalFiles} tệp`;
        return (
          <div key={batch.batchId} className="rounded-lg border">
            <div className="flex flex-wrap items-center justify-between gap-2 border-b bg-muted/40 px-3 py-2 text-sm">
              <span>
                <span className="font-medium">Tải lúc {formatDateTime(batch.createdAt)}</span>
                <span className="text-muted-foreground">
                  {" · "}
                  {batch.uploadedBy} · {shown}
                </span>
              </span>
              <StatusBadge tone={state.tone}>{state.label}</StatusBadge>
            </div>
            <ul className="divide-y">
              {batch.jobs.map((job) => (
                <JobRow key={job.jobId} job={job} canDelete={canDelete} onDeleted={onDeleted} labels={labels} />
              ))}
            </ul>
          </div>
        );
      })}
    </div>
  );
}

const FILTERS: { value: ScannedBatchFilter; label: string; count: (summary: ScannedBatchSummary) => number }[] = [
  { value: "all", label: "Tất cả", count: (summary) => summary.files },
  { value: "review", label: "Cần kiểm tra", count: (summary) => summary.review },
  { value: "noContract", label: "Chưa lập hồ sơ", count: (summary) => summary.noContract },
  { value: "mine", label: "Do tôi tải", count: (summary) => summary.mine },
];

/** The counts double as filters: a click shows only those scans. */
function BatchFilters({ summary, filter, onFilter, query, onQuery, labelFilter }: {
  summary: ScannedBatchSummary | null;
  filter: ScannedBatchFilter;
  onFilter: (filter: ScannedBatchFilter) => void;
  query: string;
  onQuery: (query: string) => void;
  labelFilter: ReactNode;
}) {
  return (
    <div className="mb-4 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
      <div className="flex flex-wrap items-center gap-2" role="group" aria-label="Lọc bản scan">
        {FILTERS.map((item) => (
          <Button
            key={item.value}
            type="button"
            size="sm"
            variant={filter === item.value ? "default" : "outline"}
            aria-pressed={filter === item.value}
            onClick={() => onFilter(item.value)}
          >
            {item.label}
            {summary && <span className="ml-1 rounded-full bg-muted/30 px-1.5 text-xs tabular-nums">{item.count(summary)}</span>}
          </Button>
        ))}
        {labelFilter}
        {summary && summary.reading > 0 && (
          <StatusBadge tone="info">
            <Loader2 className="mr-1 size-3 animate-spin" />
            {summary.reading} tệp đang đọc
          </StatusBadge>
        )}
      </div>
      <div className="relative w-full lg:w-72">
        <Search className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
        <Input
          value={query}
          onChange={(event) => onQuery(event.target.value)}
          placeholder="Tìm theo tên tệp"
          aria-label="Tìm theo tên tệp"
          className="pl-8"
        />
      </div>
    </div>
  );
}

/** The contract's own scan file (CONTRACTS.file_storage_fileid), opened with the employee's key. */
function ContractFileButtons({ contract }: { contract: ScannedContract }) {
  if (!contract.fileId) return null;
  return (
    <ScanFileButtons
      load={(key) => scannedContractsApi.contractFile(contract.contractId, key)}
      fileName={contract.fileName ?? `${contract.contractCode}.pdf`}
    />
  );
}

function ContractTable({ contracts }: { contracts: ScannedContract[] }) {
  if (contracts.length === 0) {
    return <p className="py-8 text-center text-sm text-muted-foreground">Chưa có hợp đồng scan nào.</p>;
  }
  return (
    <>
      <div className="hidden md:block">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Số hợp đồng</TableHead>
              <TableHead>Tên hợp đồng</TableHead>
              <TableHead>Khách hàng</TableHead>
              <TableHead>Ngày ký</TableHead>
              <TableHead className="text-right">Giá trị</TableHead>
              <TableHead className="pr-4 text-right">Thao tác</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {contracts.map((contract) => (
              <TableRow key={contract.contractId}>
                <TableCell className="font-semibold">{contract.contractCode}</TableCell>
                <TableCell>{contract.contractName}</TableCell>
                <TableCell>{contract.customerName ?? "—"}</TableCell>
                <TableCell>{formatDay(contract.signedDate)}</TableCell>
                <TableCell className="text-right tabular-nums">
                  {contract.contractValue != null ? formatMoney(contract.contractValue) : "—"}
                </TableCell>
                <TableCell className="pr-4 text-right">
                  <span className="inline-flex flex-wrap justify-end gap-2">
                    <ContractFileButtons contract={contract} />
                    {contract.jobId && (
                      <Button asChild variant="outline" size="sm">
                        <Link href={scannedJobHref(contract.jobId)}>Chi tiết</Link>
                      </Button>
                    )}
                  </span>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      <ul className="space-y-3 md:hidden">
        {contracts.map((contract) => (
          <li key={contract.contractId} className="rounded-lg border p-3 text-sm">
            <p className="font-semibold">{contract.contractCode}</p>
            <p>{contract.contractName}</p>
            <p className="text-muted-foreground">
              {contract.customerName ?? "—"} · ký {formatDay(contract.signedDate)}
            </p>
            <div className="mt-2 flex flex-wrap gap-2">
              <ContractFileButtons contract={contract} />
              {contract.jobId && (
                <Button asChild variant="outline" size="sm">
                  <Link href={scannedJobHref(contract.jobId)}>Chi tiết</Link>
                </Button>
              )}
            </div>
          </li>
        ))}
      </ul>
    </>
  );
}

/** Old paper contracts brought into the system by scanning (CONTRACTS.source_type = SCANNED). */
export function ScannedContractsPage() {
  const { can } = useScanAccess();
  const [batches, setBatches] = useState<ScannedBatchList | null>(null);
  const [filter, setFilter] = useState<ScannedBatchFilter>("all");
  const [query, setQuery] = useState("");
  const [search, setSearch] = useState("");
  const [labels, setLabels] = useState<ScanLabel[]>([]);
  const [labelId, setLabelId] = useState<string | null>(null);
  const [managing, setManaging] = useState(false);
  const [contracts, setContracts] = useState<ScanPage<ScannedContract> | null>(null);
  const [batchPage, setBatchPage] = useState(1);
  const [contractPage, setContractPage] = useState(1);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const [nextBatches, nextContracts] = await Promise.all([
        scannedContractsApi.listBatches(batchPage, BATCHES_PER_PAGE, filter, search, labelId),
        scannedContractsApi.listContracts(contractPage, CONTRACTS_PER_PAGE),
      ]);
      // A page that emptied (e.g. the last row moved on) falls back to the last page that has rows.
      const lastBatchPage = Math.max(1, Math.ceil(nextBatches.total / BATCHES_PER_PAGE));
      const lastContractPage = Math.max(1, Math.ceil(nextContracts.total / CONTRACTS_PER_PAGE));
      if (batchPage > lastBatchPage) setBatchPage(lastBatchPage);
      if (contractPage > lastContractPage) setContractPage(lastContractPage);
      setBatches(nextBatches);
      setContracts(nextContracts);
      setError(null);
    } catch (loadError) {
      setError(getScanErrorMessage(loadError, "Không tải được danh sách."));
    }
  }, [batchPage, contractPage, filter, search, labelId]);

  // Labels with their counts; asked again whenever the list is, since putting a label on changes the counts.
  const loadLabels = useCallback(async () => {
    try {
      const next = await scannedContractsApi.listLabels();
      setLabels(next);
      // The label filtered by was deleted: show every scan again.
      setLabelId((current) => (current && !next.some((label) => label.labelId === current) ? null : current));
    } catch {
      // The list itself reports problems; labels are an extra.
    }
  }, []);
  useEffect(() => {
    const timer = window.setTimeout(() => void loadLabels(), 0);
    return () => window.clearTimeout(timer);
  }, [loadLabels]);

  // The file name is sent once typing pauses, not on every key.
  useEffect(() => {
    const timer = window.setTimeout(() => {
      setSearch((current) => {
        const next = query.trim();
        if (next !== current) setBatchPage(1);
        return next;
      });
    }, 350);
    return () => window.clearTimeout(timer);
  }, [query]);

  // Loaded from a timer callback, not in the effect body itself (react-hooks/set-state-in-effect).
  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  // While OCR is reading a file, ask again every few seconds.
  // Only the shown page is asked again: older pages hold finished batches.
  const reading = batches?.items.some((batch) => batch.jobs.some((job) => job.jobStatus === "QUEUED" || job.jobStatus === "PROCESSING"));
  useEffect(() => {
    if (!reading) return;
    const timer = window.setInterval(() => void load(), POLL_MS);
    return () => window.clearInterval(timer);
  }, [reading, load]);

  return (
    <>
      <div>
        <h1 className="flex items-center gap-2 text-2xl font-bold tracking-tight text-foreground">
          <ScanLine className="size-6 text-primary" />
          Hợp đồng giấy scan
        </h1>
        <p className="mt-1 max-w-3xl text-sm text-muted-foreground">
          Lưu hợp đồng giấy cũ: tải bản scan, hệ thống đọc số hợp đồng, mã số thuế, số điện thoại, tên khách hàng. Đọc xong là
          tìm được ở mục Tìm hợp đồng. Sửa từ khóa nếu đọc sai; tạo hợp đồng từ bản scan là không bắt buộc.
        </p>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertTitle>Không tải được</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {can(SCANNED_PERMISSIONS.upload) && (
        <UploadCard
          onUploaded={() => {
            // The new batch is the newest: show the first page, where it is.
            if (batchPage === 1) void load();
            else setBatchPage(1);
          }}
        />
      )}

      <Card>
        <CardHeader>
          <CardTitle>Các lần tải bản scan</CardTitle>
          <CardDescription>
            Mỗi lần tải lên gồm một hoặc nhiều bản scan, được mã hóa và đọc chữ (OCR) để tìm lại. Mở bản scan để xem tệp, sửa từ
            khóa hoặc lập hồ sơ hợp đồng từ nó.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <BatchFilters
            summary={batches?.summary ?? null}
            filter={filter}
            onFilter={(next) => {
              setFilter(next);
              setBatchPage(1);
            }}
            query={query}
            onQuery={setQuery}
            labelFilter={
              <LabelFilter
                labels={labels}
                value={labelId}
                onChange={(next) => {
                  setLabelId(next);
                  setBatchPage(1);
                }}
                onManage={can(SCANNED_PERMISSIONS.confirm) ? () => setManaging(true) : undefined}
              />
            }
          />
          <ManageLabelsDialog
            labels={labels}
            open={managing}
            onOpenChange={setManaging}
            onChanged={() => {
              void loadLabels();
              void load();
            }}
          />
          {batches ? (
            <>
              <BatchList
                batches={batches.items}
                filtered={filter !== "all" || search !== "" || labelId !== null}
                canDelete={can(SCANNED_PERMISSIONS.upload)}
                onDeleted={() => {
                  void load();
                  void loadLabels();
                }}
                labels={{
                  all: labels,
                  canLabel: can(SCANNED_PERMISSIONS.upload),
                  onPick: (next) => {
                    setLabelId(next);
                    setBatchPage(1);
                  },
                  onLabelsChanged: () => void loadLabels(),
                }}
              />
              <ListPager page={batchPage} pageSize={BATCHES_PER_PAGE} total={batches.total} onPage={setBatchPage} label="lượt scan" />
            </>
          ) : (
            <Skeleton className="h-24 w-full" />
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Hợp đồng đã lập hồ sơ từ bản scan</CardTitle>
          <CardDescription>
            Bản scan đã được đối chiếu và nhập số hợp đồng, khách hàng, ngày ký. Hợp đồng ở đây được quản lý như hợp đồng khác:
            khách hàng xem được trên cổng khách hàng và hệ thống nhắc trước ngày hết hạn. Không bắt buộc với mọi bản scan.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {contracts ? (
            <>
              <ContractTable contracts={contracts.items} />
              <ListPager page={contractPage} pageSize={CONTRACTS_PER_PAGE} total={contracts.total} onPage={setContractPage} label="hợp đồng" />
            </>
          ) : (
            <Skeleton className="h-24 w-full" />
          )}
        </CardContent>
      </Card>
    </>
  );
}
