import { scanApi } from "../http";
import type { AddScanKeywordInput, ScanKeyword, ScanKeywordJobStatus, ScanKeywordState } from "../signed-scans";

/** OCR_JOB.ocr_job_processing_status, as MasterDB-Optimize_v1 lists it. */
export type OcrJobStatus = "QUEUED" | "PROCESSING" | "SUCCESS" | "NEED_REVIEW" | "FAILED";

export interface ScannedJobSummary {
  jobId: string;
  fileId: string;
  fileName: string;
  fileSizeBytes: string;
  ocrStatus: ScanKeywordJobStatus;
  jobStatus: OcrJobStatus;
  contractId: string | null;
  contractCode: string | null;
}

export interface ScannedBatch {
  batchId: string;
  status: "PENDING" | "PROCESSING" | "COMPLETED" | "FAILED";
  totalFiles: number;
  createdAt: string;
  uploadedBy: string;
  jobs: ScannedJobSummary[];
}

export interface CustomerOption {
  customerId: string;
  name: string;
  taxCode: string | null;
  phoneNumber: string | null;
}

export interface ContractTypeOption {
  contractTypeId: number;
  code: string;
  name: string;
}

export interface ScannedSuggestion {
  contractCode: string | null;
  taxCode: string | null;
  phoneNumber: string | null;
  customerName: string | null;
  matchedCustomers: CustomerOption[];
}

/** A CONTRACTS row with source_type SCANNED. Dates are yyyy-MM-dd. */
export interface ScannedContract {
  contractId: string;
  contractCode: string;
  contractName: string;
  customerId: string | null;
  customerName: string | null;
  contractTypeId: number | null;
  contractTypeName: string | null;
  status: string | null;
  approvalStatus: string | null;
  signedDate: string | null;
  effectiveDate: string | null;
  expirationDate: string | null;
  currency: string | null;
  contractValue: number | null;
  description: string | null;
  fileId: string | null;
  fileName: string | null;
  jobId: string | null;
  createdBy: string;
  createdAt: string;
}

export interface ScannedJobDetail {
  jobId: string;
  batchId: string;
  fileId: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: string;
  sha256: string;
  uploadedBy: string;
  uploadedAt: string;
  jobStatus: OcrJobStatus;
  keywords: ScanKeywordState;
  suggestion: ScannedSuggestion;
  contract: ScannedContract | null;
}

export interface ConfirmScannedContractInput {
  contractCode: string;
  contractName: string;
  customerId: string;
  contractTypeId: number | null;
  signedDate: string;
  effectiveDate: string | null;
  expirationDate: string | null;
  contractValue: number | null;
  currency: string | null;
  description: string | null;
}

/** The API reads the employee's 12-character key from this header (never the URL). */
const keyHeader = (fileKey?: string) => (fileKey ? { "X-File-Key": fileKey } : undefined);

/** Old paper contracts brought in by scanning (ScanEndpoints). */
export const scannedContractsApi = {
  async uploadBatch(files: File[]): Promise<ScannedBatch> {
    const form = new FormData();
    files.forEach((file) => form.append("files", file, file.name));
    const { data } = await scanApi.post<ScannedBatch>("/scanned-contracts/batches", form);
    return data;
  },

  async listBatches(): Promise<ScannedBatch[]> {
    const { data } = await scanApi.get<ScannedBatch[]>("/scanned-contracts/batches");
    return data;
  },

  async getJob(jobId: string): Promise<ScannedJobDetail> {
    const { data } = await scanApi.get<ScannedJobDetail>(`/scanned-contracts/jobs/${jobId}`);
    return data;
  },

  async jobFile(jobId: string, fileKey?: string): Promise<Blob> {
    const { data } = await scanApi.get<Blob>(`/scanned-contracts/jobs/${jobId}/file`, {
      responseType: "blob",
      headers: keyHeader(fileKey),
    });
    return data;
  },

  async retry(jobId: string, fileKey?: string): Promise<ScannedJobDetail> {
    const { data } = await scanApi.post<ScannedJobDetail>(`/scanned-contracts/jobs/${jobId}/retry`, undefined, {
      headers: keyHeader(fileKey),
    });
    return data;
  },

  /** A keyword OCR missed or read wrong; the scan is found by it at once. */
  async addKeyword(jobId: string, input: AddScanKeywordInput): Promise<ScanKeyword> {
    const { data } = await scanApi.post<ScanKeyword>(`/scanned-contracts/jobs/${jobId}/keywords`, input);
    return data;
  },

  async removeKeyword(jobId: string, keywordId: string): Promise<void> {
    await scanApi.delete(`/scanned-contracts/jobs/${jobId}/keywords/${keywordId}`);
  },

  async confirm(jobId: string, input: ConfirmScannedContractInput): Promise<ScannedJobDetail> {
    const { data } = await scanApi.post<ScannedJobDetail>(`/scanned-contracts/jobs/${jobId}/confirm`, input);
    return data;
  },

  async listContracts(): Promise<ScannedContract[]> {
    const { data } = await scanApi.get<ScannedContract[]>("/scanned-contracts");
    return data;
  },

  async contractFile(contractId: string, fileKey?: string): Promise<Blob> {
    const { data } = await scanApi.get<Blob>(`/scanned-contracts/${contractId}/file`, {
      responseType: "blob",
      headers: keyHeader(fileKey),
    });
    return data;
  },

  async findCustomers(q: string): Promise<CustomerOption[]> {
    const { data } = await scanApi.get<CustomerOption[]>("/scanned-contracts/customers", { params: { q } });
    return data;
  },

  async contractTypes(): Promise<ContractTypeOption[]> {
    const { data } = await scanApi.get<ContractTypeOption[]>("/scanned-contracts/contract-types");
    return data;
  },
};

export const OCR_JOB_STATUS_LABELS: Record<OcrJobStatus, { label: string; tone: "info" | "success" | "pending" | "danger" }> = {
  QUEUED: { label: "Chờ đọc", tone: "info" },
  PROCESSING: { label: "Đang đọc", tone: "info" },
  SUCCESS: { label: "Đọc xong", tone: "success" },
  NEED_REVIEW: { label: "Cần kiểm tra kỹ", tone: "pending" },
  FAILED: { label: "Không đọc được", tone: "danger" },
};
