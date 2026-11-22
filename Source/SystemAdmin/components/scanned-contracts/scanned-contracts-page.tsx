"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState, type ChangeEvent } from "react";
import { format } from "date-fns";
import { Camera, FileUp, Loader2, ScanLine, Upload, X } from "lucide-react";
import { useScanAccess } from "@/components/internal/scan-access";
import { formatDateTime, formatFileSize, formatMoney } from "@/components/signed-scans/format";
import { imagesToPdf } from "@/components/signed-scans/images-to-pdf";
import { MAX_SCAN_BYTES, pageCountLabel, ScanPagesPicker, type ScanPages } from "@/components/signed-scans/scan-pages-picker";
import { StatusBadge } from "@/components/signed-scans/status-badge";
import { ScanFileButtons } from "./scan-file-buttons";
import { ListPager } from "./list-pager";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { toast } from "@/components/ui/sonner";
import { SCANNED_PERMISSIONS } from "@/services/scans/auth";
import { getScanErrorMessage } from "@/services/scans/http";
import {
  OCR_JOB_STATUS_LABELS,
  scannedContractsApi,
  type ScannedBatch,
  type ScannedContract,
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

const BATCH_STATUS: Record<ScannedBatch["status"], { label: string; tone: "info" | "success" | "danger" }> = {
  PENDING: { label: "Chờ đọc", tone: "info" },
  PROCESSING: { label: "Đang đọc", tone: "info" },
  COMPLETED: { label: "Đọc xong", tone: "success" },
  FAILED: { label: "Có tệp không đọc được", tone: "danger" },
};

function BatchList({ batches }: { batches: ScannedBatch[] }) {
  if (batches.length === 0) {
    return <p className="py-8 text-center text-sm text-muted-foreground">Chưa có lượt scan nào.</p>;
  }
  return (
    <div className="space-y-4">
      {batches.map((batch) => (
        <div key={batch.batchId} className="rounded-lg border">
          <div className="flex flex-wrap items-center justify-between gap-2 border-b px-3 py-2 text-sm">
            <span className="font-medium">
              Lượt scan #{batch.batchId} · {batch.totalFiles} tệp · {batch.uploadedBy} · {formatDateTime(batch.createdAt)}
            </span>
            <StatusBadge tone={BATCH_STATUS[batch.status].tone}>{BATCH_STATUS[batch.status].label}</StatusBadge>
          </div>
          <ul className="divide-y">
            {batch.jobs.map((job) => {
              const status = OCR_JOB_STATUS_LABELS[job.jobStatus];
              const reading = job.jobStatus === "QUEUED" || job.jobStatus === "PROCESSING";
              return (
                <li key={job.jobId} className="flex flex-col gap-2 px-3 py-2.5 text-sm sm:flex-row sm:items-center sm:justify-between">
                  <span className="min-w-0 truncate">{job.fileName}</span>
                  <span className="flex shrink-0 flex-wrap items-center gap-2">
                    <StatusBadge tone={status.tone}>
                      {reading && <Loader2 className="mr-1 size-3 animate-spin" />}
                      {status.label}
                    </StatusBadge>
                    {job.contractId ? (
                      <StatusBadge tone="success">Đã tạo {job.contractCode}</StatusBadge>
                    ) : null}
                    <ScanFileButtons load={(key) => scannedContractsApi.jobFile(job.jobId, key)} fileName={job.fileName} />
                    <Button asChild variant="outline" size="sm">
                      <Link href={scannedJobHref(job.jobId)}>Chi tiết</Link>
                    </Button>
                  </span>
                </li>
              );
            })}
          </ul>
        </div>
      ))}
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
  const [batches, setBatches] = useState<ScanPage<ScannedBatch> | null>(null);
  const [contracts, setContracts] = useState<ScanPage<ScannedContract> | null>(null);
  const [batchPage, setBatchPage] = useState(1);
  const [contractPage, setContractPage] = useState(1);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const [nextBatches, nextContracts] = await Promise.all([
        scannedContractsApi.listBatches(batchPage, BATCHES_PER_PAGE),
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
  }, [batchPage, contractPage]);

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
          <CardTitle>Lượt scan gần đây</CardTitle>
          <CardDescription>Mở bản scan để xem tệp, sửa từ khóa hoặc tạo hợp đồng từ nó.</CardDescription>
        </CardHeader>
        <CardContent>
          {batches ? (
            <>
              <BatchList batches={batches.items} />
              <ListPager page={batchPage} pageSize={BATCHES_PER_PAGE} total={batches.total} onPage={setBatchPage} label="lượt scan" />
            </>
          ) : (
            <Skeleton className="h-24 w-full" />
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Hợp đồng đã tạo từ bản scan</CardTitle>
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
