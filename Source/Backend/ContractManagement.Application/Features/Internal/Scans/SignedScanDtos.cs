using ContractManagement.Domain.Internal.Scans;
namespace ContractManagement.Application.Features.Internal.Scans;

// The shapes of Frontend/services/scans/signed-scans/types.ts. Every status travels as text, so the
// web app does not depend on the numeric codes in the database.

public sealed record PersonRefDto(long EmployeeId, string DisplayName, string? RoleName);

public sealed record StoredScanKeywordDto(long KeywordId, string Type, string Value, int? Page, string Source);

public sealed record ScanKeywordStateDto(
    string Status,
    int? PagesRead,
    int? PageCount,
    string? LastErrorCode,
    DateTime? CompletedAt,
    IReadOnlyList<StoredScanKeywordDto> Items);

/// <summary>A row of the contract list.</summary>
public sealed record ContractListItemDto(
    long DocumentId,
    string? DocumentCode,
    string DocumentName,
    string CustomerName,
    string ContractStatus,
    decimal TotalAmount,
    long? VersionNo,
    bool SignedOnline,
    string? PaperEvidenceStatus);

public sealed record SignedVersionDto(long VersionId, long VersionNo, bool IsLocked, string? SignDate);

/// <summary>
/// F18 step 1: what OCR read on the paper compared with the contract. <paramref name="Result"/> is
/// Match, Mismatch, NotFound (nothing of that kind read) or Pending (OCR not done).
/// </summary>
public sealed record ContentCheckDto(string Field, string? Expected, IReadOnlyList<string> Found, string Result);

/// <summary>F18 steps 4 and 5: who checked the evidence, and the outcome.</summary>
public sealed record EvidenceReviewDto(string Result, string ReviewerName, DateTime At, string? Note);

public sealed record SignedEvidenceDto(
    long EvidenceId,
    long VersionId,
    long VersionNo,
    string Status,
    string SignDate,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    int? PageCount,
    string Sha256,
    PersonRefDto UploadedBy,
    DateTime UploadedAt,
    long? SupersedesEvidenceId,
    string? SupersedeReason,
    DateTime? SupersededAt,
    string? SupersededByName,
    EvidenceReviewDto? Review,
    ScanKeywordStateDto Keywords,
    IReadOnlyList<ContentCheckDto> ContentChecks,
    bool NeedsNoteToAccept);

public sealed record SignedEvidenceEventDto(DateTime At, string Kind, string? ActorName, long EvidenceId, string? Detail);

/// <summary>GET /api/contract-documents/{documentId}/signed-evidence</summary>
public sealed record SignedEvidenceOverviewDto(
    long DocumentId,
    string? DocumentCode,
    string DocumentName,
    string ContractStatus,
    string CustomerName,
    string? CustomerTaxCode,
    string? CustomerPhone,
    decimal TotalAmount,
    SignedVersionDto? CurrentVersion,
    bool SignedOnline,
    bool CanUpload,
    string? UploadBlockedReason,
    IReadOnlyList<SignedEvidenceDto> Evidences,
    IReadOnlyList<SignedEvidenceEventDto> History);

public sealed record ScanSearchMatchDto(string Type, string Value, int? Page, string Source);

public sealed record ScanSearchResultDto(
    long DocumentId,
    string? DocumentCode,
    string DocumentName,
    string CustomerName,
    string ContractStatus,
    long EvidenceId,
    string EvidenceStatus,
    string SignDate,
    IReadOnlyList<ScanSearchMatchDto> Matches);

/// <summary>
/// A scan of an old paper contract (Hợp đồng giấy scan) whose keywords match. Found as soon as OCR has read it;
/// <see cref="ContractId"/> is set only when a contract was also made from it, which is optional.
/// </summary>
public sealed record ScannedSearchResultDto(
    long JobId,
    long FileId,
    string FileName,
    DateTime UploadedAt,
    string UploadedBy,
    long? ContractId,
    string? ContractCode,
    string? ContractName,
    string? CustomerName,
    IReadOnlyList<ScanSearchMatchDto> Matches);

/// <summary><see cref="Items"/>: paper-signing evidence of contracts in the system; <see cref="Scans"/>: scanned old contracts.</summary>
public sealed record ScanSearchResponseDto(
    string Query,
    IReadOnlyList<ScanSearchResultDto> Items,
    IReadOnlyList<ScannedSearchResultDto> Scans);

public sealed record SignedScanFileDto(string FileName, string ContentType, byte[] Content);
