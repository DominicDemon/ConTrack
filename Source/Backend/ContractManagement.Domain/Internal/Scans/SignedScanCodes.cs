namespace ContractManagement.Domain.Internal.Scans;

// The master database stores these as plain INT columns without a code table, and
// Master_Database_HuongDan (4.2) asks to settle the numbers before code relies on them. Until the
// team does, every code the paper-signing feature uses is written here and nowhere else.

/// <summary>
/// <c>CONTRACT_DOCUMENTS.contract_document_status</c>, numbered as in the earlier multi-tenant
/// system. Waiting for the team to confirm.
/// </summary>
public enum ContractDocumentStatus
{
    Draft = 0,
    Negotiating = 1,
    PendingApproval = 2,

    /// <summary>Approved; waiting to be signed online (F14 to F16) or on paper (F18).</summary>
    PendingSignature = 3,

    /// <summary>Signed by every party: online, or on paper with the evidence accepted.</summary>
    Signed = 4,

    Completed = 5,
    Cancelled = 6,

    /// <summary>The customer did not agree or did not pay in full.</summary>
    Failed = 7
}

/// <summary>
/// <c>CONTRACT_SIGNED_EVIDENCE.contract_signed_evidence_status</c> (DEFAULT 1). Proposed: the
/// evidence is uploaded first, then checked (F18 steps 4 and 5).
/// </summary>
public enum SignedEvidenceStatus
{
    PendingReview = 1,
    Accepted = 2,
    Rejected = 3,
    Superseded = 4
}

/// <summary><c>CONTRACT_ELECTRONIC_SIGNATURE.contract_electronic_status</c>, as the table comment gives it.</summary>
public enum ElectronicSignatureStatus
{
    Pending = 0,
    Signed = 1,
    Declined = 2
}

public static class ContractDocumentFileRoles
{
    /// <summary>
    /// <c>CONTRACT_DOCUMENT_FILES.contract_document_file_role</c> (CHECK 1 to 5, no names yet) for
    /// the signed paper copy. 5 is assumed until the team settles the mapping.
    /// </summary>
    public const int SignedPaper = 5;
}

/// <summary><c>CONTRACT_SCAN_KEYWORD.contract_scan_keyword_source</c>.</summary>
public enum ScanKeywordSource
{
    Ocr = 1,
    Manual = 2
}

/// <summary>The state of reading a scan as the API reports it, from <see cref="OcrJobStatuses"/>.</summary>
public enum ScanKeywordJobStatus
{
    Pending = 0,
    Processing = 1,
    Done = 2,
    Failed = 3
}

/// <summary><c>OCR_JOB.ocr_job_processing_status</c>, the values listed in MasterDB-Optimize_v1.sql.</summary>
public static class OcrJobStatuses
{
    public const string Queued = "QUEUED";
    public const string Processing = "PROCESSING";
    public const string Success = "SUCCESS";
    public const string Failed = "FAILED";

    /// <summary>Read, but not every kind of keyword was found: the checker compares by eye.</summary>
    public const string NeedReview = "NEED_REVIEW";

    public static ScanKeywordJobStatus ToApiStatus(string? status) => status switch
    {
        Processing => ScanKeywordJobStatus.Processing,
        Success or NeedReview => ScanKeywordJobStatus.Done,
        Failed => ScanKeywordJobStatus.Failed,
        _ => ScanKeywordJobStatus.Pending,
    };
}

/// <summary><c>OCR_BATCH.batch_status</c>, the values listed in MasterDB-Optimize_v1.sql.</summary>
public static class OcrBatchStatuses
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
}
