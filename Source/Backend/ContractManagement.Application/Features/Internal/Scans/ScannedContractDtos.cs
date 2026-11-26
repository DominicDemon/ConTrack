using ContractManagement.Domain.Internal.Scans;
namespace ContractManagement.Application.Features.Internal.Scans;

/// <summary>One upload of old paper contracts: an OCR_BATCH with one OCR_JOB per file.</summary>
/// <summary>One page of a list; <see cref="Total"/> counts every row the employee may see.</summary>
public sealed record ScanPageDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record ScannedBatchDto(
    long BatchId,
    string Status,
    int TotalFiles,
    DateTime CreatedAt,
    string UploadedBy,
    IReadOnlyList<ScannedJobSummaryDto> Jobs);

/// <summary>
/// One file of a batch. <see cref="OcrStatus"/> is Pending, Processing, Done or Failed;
/// <see cref="JobStatus"/> the OCR_JOB value (QUEUED, PROCESSING, SUCCESS, NEED_REVIEW, FAILED).
/// </summary>
public sealed record ScannedJobSummaryDto(
    long JobId,
    long FileId,
    string FileName,
    long FileSizeBytes,
    string OcrStatus,
    string JobStatus,
    long? ContractId,
    string? ContractCode,
    int? PageCount = null,
    string? FoundContractCode = null,
    string? FoundTaxCode = null,
    string? FoundCustomerName = null,
    string? SameFileAs = null,
    int SameContractCodeCount = 0);

/// <summary>Which scans the list keeps: all, those to check again, those without a contract yet, or the employee's own.</summary>
public enum ScannedBatchFilter { All, Review, NoContract, Mine }

/// <summary>How many scans the employee may see, by state; each count opens the matching filter.</summary>
public sealed record ScannedBatchSummaryDto(int Files, int Review, int NoContract, int Reading, int Mine);

/// <summary>A page of batches plus the counts over every scan the employee may see.</summary>
public sealed record ScannedBatchListDto(
    IReadOnlyList<ScannedBatchDto> Items,
    int Total,
    int Page,
    int PageSize,
    ScannedBatchSummaryDto Summary);

/// <summary>What OCR read, offered to the checker to start the contract from. Every value can be changed.</summary>
public sealed record ScannedSuggestionDto(
    string? ContractCode,
    string? TaxCode,
    string? PhoneNumber,
    string? CustomerName,
    IReadOnlyList<CustomerOptionDto> MatchedCustomers);

public sealed record ScannedJobDetailDto(
    long JobId,
    long BatchId,
    long FileId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string Sha256,
    string UploadedBy,
    DateTime UploadedAt,
    string JobStatus,
    ScanKeywordStateDto Keywords,
    ScannedSuggestionDto Suggestion,
    ScannedContractDto? Contract);

public sealed record CustomerOptionDto(long CustomerId, string Name, string? TaxCode, string? PhoneNumber);

public sealed record ContractTypeOptionDto(int ContractTypeId, string Code, string Name);

/// <summary>A CONTRACTS row with source_type SCANNED. Dates are yyyy-MM-dd.</summary>
public sealed record ScannedContractDto(
    long ContractId,
    string ContractCode,
    string ContractName,
    long? CustomerId,
    string? CustomerName,
    int? ContractTypeId,
    string? ContractTypeName,
    string? Status,
    string? ApprovalStatus,
    string? SignedDate,
    string? EffectiveDate,
    string? ExpirationDate,
    string? Currency,
    double? ContractValue,
    string? Description,
    long? FileId,
    string? FileName,
    long? JobId,
    string CreatedBy,
    DateTime CreatedAt);

public sealed record ScannedFileDto(string FileName, string ContentType, byte[] Content);
