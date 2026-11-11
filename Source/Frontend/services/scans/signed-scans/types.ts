/**
 * API contract between the web app and the API for paper-signing evidence, as
 * Master_Database_HuongDan describes it: F18 (upload the scan of the signed paper for the agreed
 * version, then check it), F19 (replace it) and F29 (audit and notifications). OCR reads keywords
 * from each file in the background, to compare the paper with the contract and to search by.
 *
 * Every status travels as text. The API maps it to the numeric codes in the master database
 * once the team settles them, so nothing here depends on those numbers.
 */

export type ScanKeywordType = "ContractCode" | "TaxCode" | "PhoneNumber" | "CustomerName";

export type ScanKeywordSource = "Ocr" | "Manual";

export type ScanKeywordJobStatus = "Pending" | "Processing" | "Done" | "Failed";

export type ContractStatusKey =
  | "Draft"
  | "Negotiating"
  | "PendingApproval"
  | "PendingSignature"
  | "Signed"
  | "Completed"
  | "Cancelled"
  | "Failed";

export type SignedEvidenceStatus = "PendingReview" | "Accepted" | "Rejected" | "Superseded";

export interface ScanKeyword {
  keywordId: string;
  type: ScanKeywordType;
  /** Contract codes upper case, tax codes and phone numbers digits only, names as on the paper. */
  value: string;
  /** First page the keyword was read on; null for a keyword added by hand. */
  page: number | null;
  source: ScanKeywordSource;
}

export interface ScanKeywordState {
  status: ScanKeywordJobStatus;
  pagesRead: number | null;
  pageCount: number | null;
  /** An error code only, never OCR text, e.g. "OcrEngineUnavailable". */
  lastErrorCode: string | null;
  completedAt: string | null;
  items: ScanKeyword[];
}

export interface PersonRef {
  employeeId: string;
  displayName: string;
  roleName: string | null;
}

/** GET /api/contract-documents */
export interface ContractListItem {
  documentId: string;
  documentCode: string | null;
  documentName: string;
  customerName: string;
  contractStatus: ContractStatusKey;
  totalAmount: number;
  versionNo: string | null;
  signedOnline: boolean;
  /** The newest paper evidence that is not replaced, if any. */
  paperEvidenceStatus: SignedEvidenceStatus | null;
}

export interface SignedVersion {
  versionId: string;
  versionNo: string;
  isLocked: boolean;
  /** yyyy-MM-dd once signed. */
  signDate: string | null;
}

export type ContentCheckResult = "Match" | "Mismatch" | "NotFound" | "Pending";

/** F18 step 1: what OCR read on the paper against the contract. */
export interface ContentCheck {
  field: ScanKeywordType;
  expected: string | null;
  found: string[];
  result: ContentCheckResult;
}

export interface EvidenceReview {
  result: "Accepted" | "Rejected";
  reviewerName: string;
  at: string;
  note: string | null;
}

export interface SignedEvidence {
  evidenceId: string;
  versionId: string;
  versionNo: string;
  status: SignedEvidenceStatus;
  /** The date on the paper, yyyy-MM-dd. */
  signDate: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: string;
  pageCount: number | null;
  sha256: string;
  uploadedBy: PersonRef;
  uploadedAt: string;
  supersedesEvidenceId: string | null;
  supersedeReason: string | null;
  supersededAt: string | null;
  supersededByName: string | null;
  review: EvidenceReview | null;
  keywords: ScanKeywordState;
  contentChecks: ContentCheck[];
  /** OCR found a difference or could not read the paper: accepting needs a note. */
  needsNoteToAccept: boolean;
}

export type SignedEvidenceEventKind =
  | "Uploaded"
  | "Accepted"
  | "Rejected"
  | "Superseded"
  | "KeywordAdded"
  | "KeywordRemoved";

export interface SignedEvidenceEvent {
  at: string;
  kind: SignedEvidenceEventKind;
  actorName: string | null;
  evidenceId: string;
  detail: string | null;
}

/** GET /api/contract-documents/{documentId}/signed-evidence */
export interface SignedEvidenceOverview {
  documentId: string;
  documentCode: string | null;
  documentName: string;
  contractStatus: ContractStatusKey;
  customerName: string;
  customerTaxCode: string | null;
  customerPhone: string | null;
  totalAmount: number;
  currentVersion: SignedVersion | null;
  signedOnline: boolean;
  canUpload: boolean;
  uploadBlockedReason: string | null;
  /** Newest first. */
  evidences: SignedEvidence[];
  /** Newest first. */
  history: SignedEvidenceEvent[];
}

export interface UploadSignedEvidenceInput {
  file: File;
  versionId: string;
  /** The date on the paper, yyyy-MM-dd. */
  signDate: string;
}

export interface AcceptSignedEvidenceInput {
  partyASigned: boolean;
  partyBSigned: boolean;
  contentMatches: boolean;
  note: string | null;
}

export interface AddScanKeywordInput {
  type: ScanKeywordType;
  value: string;
}

export type ScanSearchType = ScanKeywordType | "All";

export interface ScanSearchQuery {
  q: string;
  type: ScanSearchType;
}

export interface ScanSearchMatch {
  type: ScanKeywordType;
  value: string;
  page: number | null;
  source: ScanKeywordSource;
}

export interface ScanSearchResult {
  documentId: string;
  documentCode: string | null;
  documentName: string;
  customerName: string;
  contractStatus: ContractStatusKey;
  evidenceId: string;
  evidenceStatus: SignedEvidenceStatus;
  signDate: string;
  matches: ScanSearchMatch[];
}

/** A scan of an old paper contract; contractId is set only when a contract was also made from it (optional). */
export interface ScannedSearchResult {
  jobId: string;
  fileId: string;
  fileName: string;
  uploadedAt: string;
  uploadedBy: string;
  contractId: string | null;
  contractCode: string | null;
  contractName: string | null;
  customerName: string | null;
  matches: ScanSearchMatch[];
}

export interface ScanSearchResponse {
  query: string;
  /** Paper-signing evidence of contracts in the system. */
  items: ScanSearchResult[];
  /** Scanned old paper contracts (Hợp đồng giấy scan). */
  scans: ScannedSearchResult[];
}

export interface DownloadedScanFile {
  blob: Blob;
  fileName: string;
}

export interface SignedEvidenceApi {
  listContracts(): Promise<ContractListItem[]>;
  getOverview(documentId: string): Promise<SignedEvidenceOverview>;
  upload(documentId: string, input: UploadSignedEvidenceInput): Promise<SignedEvidenceOverview>;
  accept(evidenceId: string, input: AcceptSignedEvidenceInput): Promise<SignedEvidenceOverview>;
  reject(evidenceId: string, reason: string): Promise<SignedEvidenceOverview>;
  supersede(evidenceId: string, file: File, reason: string): Promise<SignedEvidenceOverview>;
  /** fileKey: the employee's 12-character key, needed for an encrypted scan. */
  downloadFile(evidenceId: string, fileKey?: string): Promise<DownloadedScanFile>;
  retryKeywords(evidenceId: string, fileKey?: string): Promise<ScanKeywordState>;
  addKeyword(evidenceId: string, input: AddScanKeywordInput): Promise<ScanKeyword>;
  removeKeyword(evidenceId: string, keywordId: string): Promise<void>;
  search(query: ScanSearchQuery): Promise<ScanSearchResponse>;
}
