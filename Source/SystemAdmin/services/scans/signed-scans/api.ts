import { scanApi } from "../http";
import type {
  AcceptSignedEvidenceInput,
  AddScanKeywordInput,
  ContractListItem,
  DownloadedScanFile,
  ScanKeyword,
  ScanKeywordState,
  ScanSearchQuery,
  ScanSearchResponse,
  SignedEvidenceApi,
  SignedEvidenceOverview,
  UploadSignedEvidenceInput,
  ScanEmailInput,
  ScanEmailResult,
} from "./types";

const fileNameFromDisposition = (header: unknown): string | null => {
  if (typeof header !== "string") return null;
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(header);
  if (encoded) {
    try {
      return decodeURIComponent(encoded[1].trim());
    } catch {
      // Fall back to the plain file name below.
    }
  }
  const plain = /filename="?([^";]+)"?/i.exec(header);
  return plain ? plain[1].trim() : null;
};

/** The API reads the employee's 12-character key from this header (never the URL). */
const keyHeader = (fileKey?: string) => (fileKey ? { "X-File-Key": fileKey } : undefined);

/** Calls the internal API. */
export const httpSignedEvidenceApi: SignedEvidenceApi = {
  async listContracts() {
    const { data } = await scanApi.get<ContractListItem[]>("/contract-documents");
    return data;
  },

  async getOverview(documentId: string) {
    const { data } = await scanApi.get<SignedEvidenceOverview>(`/contract-documents/${documentId}/signed-evidence`);
    return data;
  },

  async upload(documentId: string, input: UploadSignedEvidenceInput) {
    const form = new FormData();
    form.append("file", input.file);
    form.append("versionId", String(input.versionId));
    form.append("signDate", input.signDate);
    const { data } = await scanApi.post<SignedEvidenceOverview>(`/contract-documents/${documentId}/signed-evidence`, form);
    return data;
  },

  async accept(evidenceId: string, input: AcceptSignedEvidenceInput) {
    const { data } = await scanApi.post<SignedEvidenceOverview>(`/signed-evidence/${evidenceId}/accept`, input);
    return data;
  },

  async reject(evidenceId: string, reason: string) {
    const { data } = await scanApi.post<SignedEvidenceOverview>(`/signed-evidence/${evidenceId}/reject`, { reason });
    return data;
  },

  async supersede(evidenceId: string, file: File, reason: string) {
    const form = new FormData();
    form.append("file", file);
    form.append("reason", reason);
    const { data } = await scanApi.post<SignedEvidenceOverview>(`/signed-evidence/${evidenceId}/supersede`, form);
    return data;
  },

  async downloadFile(evidenceId: string, fileKey?: string): Promise<DownloadedScanFile> {
    const response = await scanApi.get<Blob>(`/signed-evidence/${evidenceId}/file`, {
      responseType: "blob",
      headers: keyHeader(fileKey),
    });
    return {
      blob: response.data,
      fileName: fileNameFromDisposition(response.headers["content-disposition"]) ?? `ban-ky-${evidenceId}.pdf`,
    };
  },

  async retryKeywords(evidenceId: string, fileKey?: string) {
    const { data } = await scanApi.post<ScanKeywordState>(`/signed-evidence/${evidenceId}/keywords/retry`, undefined, {
      headers: keyHeader(fileKey),
    });
    return data;
  },

  async addKeyword(evidenceId: string, input: AddScanKeywordInput) {
    const { data } = await scanApi.post<ScanKeyword>(`/signed-evidence/${evidenceId}/keywords`, input);
    return data;
  },

  async removeKeyword(evidenceId: string, keywordId: string) {
    await scanApi.delete(`/signed-evidence/${evidenceId}/keywords/${keywordId}`);
  },

  /** Sends the evidence file as a PDF attachment; the server opens it with the sender's key, like "View". */
  async emailFile(evidenceId: string, input: ScanEmailInput, fileKey?: string): Promise<ScanEmailResult> {
    const { data } = await scanApi.post<ScanEmailResult>(`/signed-evidence/${evidenceId}/email`, input, {
      headers: keyHeader(fileKey),
    });
    return data;
  },

  async search(query: ScanSearchQuery) {
    const { data } = await scanApi.get<ScanSearchResponse>("/signed-evidence/search", {
      params: { q: query.q, type: query.type },
    });
    return data;
  },
};
