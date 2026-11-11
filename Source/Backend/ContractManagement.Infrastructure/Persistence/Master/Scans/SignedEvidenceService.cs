using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal.Scans;
using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

/// <summary>
/// Paper-signing evidence, as Master_Database_HuongDan describes it (F18 upload and check, F19
/// replacement, F29 audit and notification). The evidence is a <c>CONTRACT_SIGNED_EVIDENCE</c> row
/// pointing to a <c>CONTRACT_DOCUMENT_FILES</c> row of the agreed version, which points to the file in
/// <c>FILE_STORAGE</c>. It is uploaded first and checked afterwards; only an accepted first evidence
/// signs the contract. No electronic signature row is ever written for paper.
/// OCR reads the keywords of each file in the background, to check the content and to search by.
/// </summary>
public sealed class SignedEvidenceService(
    ScanMasterDbContext db,
    IFileStore files,
    ICurrentEmployee currentEmployee,
    Microsoft.Extensions.Options.IOptions<ScanOcrOptions> ocrOptions,
    FileProtectionService protection,
    IOcrStagingStore staging)
{
    public const string EvidenceEntity = "CONTRACT_SIGNED_EVIDENCE";
    private const string DocumentEntity = "CONTRACT_DOCUMENTS";
    private const string FileObjectType = "CONTRACT_DOCUMENT";
    private const string NotificationModule = "CONTRACT";

    public const string UploadedAction = "SignedEvidenceUploaded";
    public const string AcceptedAction = "SignedEvidenceAccepted";
    public const string RejectedAction = "SignedEvidenceRejected";
    public const string SupersededAction = "SignedEvidenceSuperseded";
    public const string KeywordAddedAction = "SignedEvidenceKeywordAdded";
    public const string KeywordRemovedAction = "SignedEvidenceKeywordRemoved";
    private const string ContractSignedAction = "ContractSignedOnPaper";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<ScanFileKind, (string Extension, string ContentType)> Kinds = new()
    {
        [ScanFileKind.Pdf] = (".pdf", "application/pdf"),
        [ScanFileKind.Png] = (".png", "image/png"),
        [ScanFileKind.Jpeg] = (".jpg", "image/jpeg"),
    };

    private sealed class ContractRow
    {
        public long DocumentId { get; init; }
        public string? Code { get; init; }
        public string Name { get; init; } = "";
        public int Status { get; init; }
        public long? CurrentVersionId { get; init; }
        public long OwnerId { get; init; }
        public string CustomerName { get; init; } = "";
        public string? TaxCode { get; init; }
        public string? Phone { get; init; }
    }

    private sealed record EvidenceRow(
        ContractSignedEvidence Evidence,
        long DocumentId,
        long VersionId,
        long VersionNo,
        long FileId,
        string FileName,
        string ContentType,
        long FileSize,
        string Sha256,
        string StorageKey);

    // ---------------------------------------------------------------------------------------------
    // Reading
    // ---------------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<ContractListItemDto>> ListContractsAsync(CancellationToken cancellationToken)
    {
        var contracts = await Contracts().OrderByDescending(row => row.DocumentId).Take(200).ToListAsync(cancellationToken);
        var ids = contracts.Select(row => row.DocumentId).ToList();
        var versionIds = contracts.Select(row => row.CurrentVersionId).OfType<long>().ToList();
        var versions = await db.ContractVersions.AsNoTracking()
            .Where(version => versionIds.Contains(version.ContractVersionId))
            .ToDictionaryAsync(version => version.ContractVersionId, cancellationToken);
        var online = await SignedOnlineVersionsAsync(versionIds, cancellationToken);
        var latest = (await EvidenceRows(documentIds: ids).ToListAsync(cancellationToken))
            .Where(row => row.Evidence.ContractSignedEvidenceStatus != (int)SignedEvidenceStatus.Superseded)
            .GroupBy(row => row.DocumentId)
            .ToDictionary(
                group => group.Key,
                group => ((SignedEvidenceStatus)group.MaxBy(row => row.Evidence.ContractSignedEvidenceId)!
                    .Evidence.ContractSignedEvidenceStatus).ToString());

        return contracts.Select(row =>
        {
            var version = row.CurrentVersionId is { } id ? versions.GetValueOrDefault(id) : null;
            return new ContractListItemDto(
                row.DocumentId,
                row.Code,
                row.Name,
                row.CustomerName,
                StatusKey(row.Status),
                version?.ContractVersionTotalamount ?? 0,
                version?.ContractVersionVersionNo,
                version is not null && online.Contains(version.ContractVersionId),
                latest.GetValueOrDefault(row.DocumentId));
        }).ToList();
    }

    public async Task<SignedEvidenceOverviewDto> GetOverviewAsync(long documentId, CancellationToken cancellationToken)
    {
        var contract = await GetContractAsync(documentId, cancellationToken);
        var version = contract.CurrentVersionId is { } versionId
            ? await db.ContractVersions.AsNoTracking().SingleOrDefaultAsync(
                candidate => candidate.ContractVersionId == versionId && candidate.ContractDocumentId == documentId,
                cancellationToken)
            : null;
        var signedOnline = version is not null
            && (await SignedOnlineVersionsAsync([version.ContractVersionId], cancellationToken)).Count > 0;

        var rows = await EvidenceRows(documentIds: [documentId]).ToListAsync(cancellationToken);
        var evidences = await ToDtosAsync(contract, rows, cancellationToken);
        var blocked = UploadBlockedReason(contract, version, signedOnline, rows);

        return new SignedEvidenceOverviewDto(
            contract.DocumentId,
            contract.Code,
            contract.Name,
            StatusKey(contract.Status),
            contract.CustomerName,
            contract.TaxCode,
            contract.Phone,
            version?.ContractVersionTotalamount ?? 0,
            version is null
                ? null
                : new SignedVersionDto(
                    version.ContractVersionId,
                    version.ContractVersionVersionNo,
                    version.ContractVersionIslocked,
                    version.ContractVersionSigndate is { } signDate ? DateOnly.FromDateTime(signDate).ToString("yyyy-MM-dd") : null),
            signedOnline,
            blocked is null,
            blocked,
            evidences.OrderByDescending(item => item.EvidenceId).ToList(),
            await HistoryAsync(rows, cancellationToken));
    }

    public async Task<long> DocumentOfAsync(long evidenceId, CancellationToken cancellationToken) =>
        (await GetEvidenceRowAsync(evidenceId, cancellationToken)).DocumentId;

    public async Task<SignedScanFileDto> GetFileAsync(long evidenceId, string? fileKey, CancellationToken cancellationToken)
    {
        var row = await GetEvidenceRowAsync(evidenceId, cancellationToken);
        var content = await OpenStoredAsync(row, fileKey, cancellationToken);
        return new SignedScanFileDto(row.FileName, row.ContentType, content);
    }

    public async Task<ScanKeywordStateDto> GetKeywordStateAsync(long fileId, CancellationToken cancellationToken)
    {
        var job = await db.OcrJobs.AsNoTracking()
            .Where(candidate => candidate.FileStorageFileid == fileId)
            .OrderByDescending(candidate => candidate.OcrJobId)
            .FirstOrDefaultAsync(cancellationToken);
        var keywords = await db.ContractScanKeywords.AsNoTracking()
            .Where(keyword => keyword.FileStorageFileid == fileId)
            .ToListAsync(cancellationToken);
        return KeywordState(job, keywords, ocrOptions.Value.KeywordMaxPages);
    }

    // ---------------------------------------------------------------------------------------------
    // F18 steps 1 to 3: upload
    // ---------------------------------------------------------------------------------------------

    public async Task<long> UploadAsync(
        long documentId,
        long versionId,
        DateOnly signDate,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var contract = await GetContractAsync(documentId, cancellationToken);
        if (contract.CurrentVersionId != versionId)
        {
            throw new InternalAppException(409, "Phiên bản đã chọn không còn là phiên bản đã thống nhất của hợp đồng. Hãy tải lại trang.");
        }

        var version = await db.ContractVersions.AsNoTracking()
            .SingleAsync(candidate => candidate.ContractVersionId == versionId, cancellationToken);
        var signedOnline = (await SignedOnlineVersionsAsync([versionId], cancellationToken)).Count > 0;
        var rows = await EvidenceRows(documentIds: [documentId]).ToListAsync(cancellationToken);
        if (UploadBlockedReason(contract, version, signedOnline, rows) is { } reason)
        {
            throw new InternalAppException(409, reason);
        }

        return await FileEvidenceAsync(contract, versionId, signDate, fileName, content, replaced: null, replaceReason: null,
            cancellationToken);
    }

    // ---------------------------------------------------------------------------------------------
    // F19: replace an accepted evidence (the old one stays until the new one is accepted)
    // ---------------------------------------------------------------------------------------------

    public async Task<long> SupersedeAsync(
        long evidenceId,
        string fileName,
        byte[] content,
        string reason,
        CancellationToken cancellationToken)
    {
        var old = await GetEvidenceRowAsync(evidenceId, cancellationToken);
        var contract = await GetContractAsync(old.DocumentId, cancellationToken);
        if (old.Evidence.ContractSignedEvidenceStatus != (int)SignedEvidenceStatus.Accepted)
        {
            throw new InternalAppException(409, "Chỉ thay được bản ký đã được xác nhận hợp lệ.");
        }

        if (contract.Status == (int)ContractDocumentStatus.Cancelled)
        {
            throw new InternalAppException(409, "Hợp đồng đã hủy nên không thay bản ký được.");
        }

        var rows = await EvidenceRows(documentIds: [old.DocumentId]).ToListAsync(cancellationToken);
        if (rows.Any(row => row.VersionId == old.VersionId
                && row.Evidence.ContractSignedEvidenceStatus == (int)SignedEvidenceStatus.PendingReview))
        {
            throw new InternalAppException(409, "Đang có một bản ký chờ kiểm tra. Hãy xử lý bản đó trước.");
        }

        // The same paper was signed once: the replacement keeps its version and signing date.
        return await FileEvidenceAsync(contract, old.VersionId, old.Evidence.ContractSignedEvidenceSigndate, fileName, content,
            old.Evidence, reason.Trim(), cancellationToken);
    }

    // ---------------------------------------------------------------------------------------------
    // F18 steps 4 and 5: check
    // ---------------------------------------------------------------------------------------------

    public async Task AcceptAsync(long evidenceId, string? note, CancellationToken cancellationToken)
    {
        var row = await GetEvidenceRowAsync(evidenceId, cancellationToken);
        var contract = await GetContractAsync(row.DocumentId, cancellationToken);
        if (row.Evidence.ContractSignedEvidenceStatus != (int)SignedEvidenceStatus.PendingReview)
        {
            throw new InternalAppException(409, "Bản ký này không còn chờ kiểm tra. Hãy tải lại trang.");
        }

        var replacing = row.Evidence.SupersedesEvidenceId;
        if (replacing is null && contract.Status != (int)ContractDocumentStatus.PendingSignature)
        {
            throw new InternalAppException(409, "Hợp đồng không còn ở trạng thái chờ ký. Hãy tải lại trang.");
        }

        var state = await GetKeywordStateAsync(row.FileId, cancellationToken);
        if (state.Status is nameof(ScanKeywordJobStatus.Pending) or nameof(ScanKeywordJobStatus.Processing))
        {
            throw new InternalAppException(409, "Hệ thống đang đọc nội dung bản ký để đối chiếu. Hãy chờ đọc xong rồi xác nhận.");
        }

        var checks = ContentChecks(contract, state);
        if (NeedsNote(state, checks) && string.IsNullOrWhiteSpace(note))
        {
            throw Invalid(
                "Note",
                "Nội dung đọc được chưa khớp hồ sơ hoặc chưa đọc được. Hãy ghi rõ đã đối chiếu thế nào trước khi xác nhận.");
        }

        var employeeId = currentEmployee.EmployeeId;
        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var evidence = await db.ContractSignedEvidences.SingleAsync(
            candidate => candidate.ContractSignedEvidenceId == evidenceId, cancellationToken);
        db.Entry(evidence).Property(candidate => candidate.Rowversion).OriginalValue = row.Evidence.Rowversion;
        evidence.ContractSignedEvidenceStatus = (int)SignedEvidenceStatus.Accepted;
        db.AuditLogs.Add(Audit(AcceptedAction, EvidenceEntity, evidenceId, null, new { note = note?.Trim() }));

        if (replacing is { } oldId)
        {
            // F19 step 4: the old evidence is marked replaced only now that the new one is valid.
            var old = await db.ContractSignedEvidences.SingleAsync(
                candidate => candidate.ContractSignedEvidenceId == oldId, cancellationToken);
            if (old.ContractSignedEvidenceStatus == (int)SignedEvidenceStatus.Accepted)
            {
                old.ContractSignedEvidenceStatus = (int)SignedEvidenceStatus.Superseded;
                old.SupersededByEmployeeId = employeeId;
                old.SupersededAt = now;
                db.AuditLogs.Add(Audit(SupersededAction, EvidenceEntity, oldId, null, new { by = evidenceId }));
            }
        }
        else
        {
            // F18 step 5: signing date, version lock and overall status.
            var version = await db.ContractVersions.SingleAsync(
                candidate => candidate.ContractVersionId == row.VersionId, cancellationToken);
            version.ContractVersionSigndate = row.Evidence.ContractSignedEvidenceSigndate.ToDateTime(TimeOnly.MinValue);
            if (!version.ContractVersionIslocked)
            {
                version.ContractVersionIslocked = true;
                version.ContractVersionLockeddate = now;
                version.ContractVersionLockedbyemployeeid = (int)employeeId;
            }

            var signed = await db.ContractDocuments
                .Where(document => document.ContractDocumentId == row.DocumentId
                    && document.ContractDocumentStatus == (int)ContractDocumentStatus.PendingSignature)
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(document => document.ContractDocumentStatus, (int)ContractDocumentStatus.Signed)
                        .SetProperty(document => document.UpdatedDate, now),
                    cancellationToken);
            if (signed == 0)
            {
                throw new InternalAppException(409, "Hợp đồng vừa được người khác cập nhật. Hãy tải lại trang.");
            }

            db.AuditLogs.Add(Audit(ContractSignedAction, DocumentEntity, row.DocumentId,
                new { status = StatusKey((int)ContractDocumentStatus.PendingSignature) },
                new { status = StatusKey((int)ContractDocumentStatus.Signed), evidenceId, signDate = row.Evidence.ContractSignedEvidenceSigndate }));
        }

        await MakePrimaryAsync(row, cancellationToken);
        await NotifyAsync(contract, evidenceId, "SIGNED_EVIDENCE_ACCEPTED",
            $"Bản ký giấy hợp lệ: {contract.Code ?? contract.Name}",
            replacing is null
                ? $"Bản ký giấy đã được xác nhận. Hợp đồng chuyển sang Đã ký, ngày ký {row.Evidence.ContractSignedEvidenceSigndate:dd/MM/yyyy}."
                : "Bản ký thay thế đã được xác nhận; bản cũ được giữ trong lịch sử.",
            row.Evidence.UploadedByEmployeeid, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RejectAsync(long evidenceId, string reason, CancellationToken cancellationToken)
    {
        var row = await GetEvidenceRowAsync(evidenceId, cancellationToken);
        var contract = await GetContractAsync(row.DocumentId, cancellationToken);
        if (row.Evidence.ContractSignedEvidenceStatus != (int)SignedEvidenceStatus.PendingReview)
        {
            throw new InternalAppException(409, "Bản ký này không còn chờ kiểm tra. Hãy tải lại trang.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var evidence = await db.ContractSignedEvidences.SingleAsync(
            candidate => candidate.ContractSignedEvidenceId == evidenceId, cancellationToken);
        db.Entry(evidence).Property(candidate => candidate.Rowversion).OriginalValue = row.Evidence.Rowversion;
        evidence.ContractSignedEvidenceStatus = (int)SignedEvidenceStatus.Rejected;
        db.AuditLogs.Add(Audit(RejectedAction, EvidenceEntity, evidenceId, null, new { reason = reason.Trim() }));
        await NotifyAsync(contract, evidenceId, "SIGNED_EVIDENCE_REJECTED",
            $"Bản ký giấy không hợp lệ: {contract.Code ?? contract.Name}",
            $"Lý do: {reason.Trim()}",
            row.Evidence.UploadedByEmployeeid, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // ---------------------------------------------------------------------------------------------
    // Keywords
    // ---------------------------------------------------------------------------------------------

    /// <summary>OCR reads a plain copy; an encrypted file is opened with the employee's key to make one again.</summary>
    public async Task<ScanKeywordStateDto> RetryKeywordsAsync(long evidenceId, string? fileKey, CancellationToken cancellationToken)
    {
        var row = await GetEvidenceRowAsync(evidenceId, cancellationToken);
        var job = await db.OcrJobs
            .Where(candidate => candidate.FileStorageFileid == row.FileId)
            .OrderByDescending(candidate => candidate.OcrJobId)
            .FirstOrDefaultAsync(cancellationToken);
        if (job?.OcrJobProcessingStatus != OcrJobStatuses.Failed)
        {
            throw new InternalAppException(409, "Chỉ đọc lại được bản ký bị lỗi đọc nội dung.");
        }

        if (await staging.ReadAsync(row.FileId, cancellationToken) is null)
        {
            await staging.SaveAsync(row.FileId, await OpenStoredAsync(row, fileKey, cancellationToken), cancellationToken);
        }

        job.OcrJobProcessingStatus = OcrJobStatuses.Queued;
        job.ErrorMessage = null;
        job.CompletedAt = null;
        await db.OcrBatches
            .Where(batch => batch.OcrPatchId == job.OcrBatchId)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(batch => batch.BatchStatus, OcrBatchStatuses.Pending)
                    .SetProperty(batch => batch.UpdatedByEmployeeId, currentEmployee.EmployeeId)
                    .SetProperty(batch => batch.UpdatedAt, DateTime.UtcNow),
                cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await GetKeywordStateAsync(row.FileId, cancellationToken);
    }

    public async Task<StoredScanKeywordDto> AddKeywordAsync(long evidenceId, string type, string value, CancellationToken cancellationToken)
    {
        var row = await GetEvidenceRowAsync(evidenceId, cancellationToken);
        var (keywordType, normalised) = ParseTypedKeyword(type, value);

        if (await db.ContractScanKeywords.AnyAsync(
                keyword => keyword.FileStorageFileid == row.FileId
                    && keyword.ContractScanKeywordType == (int)keywordType
                    && keyword.ContractScanKeywordValue == normalised,
                cancellationToken))
        {
            throw new InternalAppException(409, "Từ khóa này đã có.");
        }

        var keyword = new ContractScanKeyword
        {
            FileStorageFileid = row.FileId,
            ContractScanKeywordType = (int)keywordType,
            ContractScanKeywordValue = normalised,
            ContractScanKeywordSource = (int)ScanKeywordSource.Manual,
            CreatedByEmployeeId = currentEmployee.EmployeeId,
            CreatedAt = DateTime.UtcNow,
        };
        db.ContractScanKeywords.Add(keyword);
        db.AuditLogs.Add(Audit(KeywordAddedAction, EvidenceEntity, evidenceId, null, new { type = keywordType.ToString(), value = normalised }));
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(keyword);
    }

    /// <summary>A keyword typed by staff, in the form OCR keywords are stored in; a validation error when it is not one.</summary>
    internal static (ScanKeywordType Type, string Value) ParseTypedKeyword(string type, string value)
    {
        if (!Enum.TryParse<ScanKeywordType>(type, ignoreCase: false, out var keywordType) || !Enum.IsDefined(keywordType))
        {
            throw Invalid("Type", "Loại từ khóa không hợp lệ.");
        }

        var normalised = ContractScanKeywordExtractor.NormaliseTyped(keywordType, value)
            ?? throw Invalid("Value", keywordType switch
            {
                ScanKeywordType.ContractCode => "Số hợp đồng phải có từ 4 đến 64 ký tự.",
                ScanKeywordType.TaxCode => "Mã số thuế phải có 10 hoặc 13 chữ số.",
                ScanKeywordType.CustomerName => "Tên khách hàng phải có từ 3 đến 200 ký tự.",
                _ => "Số điện thoại phải có 10 hoặc 11 chữ số và bắt đầu bằng 0.",
            });
        return (keywordType, normalised);
    }

    internal static ScanSearchMatchDto ToMatchDto(ContractScanKeyword keyword) => new(
        ((ScanKeywordType)keyword.ContractScanKeywordType).ToString(),
        keyword.ContractScanKeywordValue,
        keyword.ContractScanKeywordPagenumber,
        ((ScanKeywordSource)keyword.ContractScanKeywordSource).ToString());

    public async Task RemoveKeywordAsync(long evidenceId, long keywordId, CancellationToken cancellationToken)
    {
        var row = await GetEvidenceRowAsync(evidenceId, cancellationToken);
        var keyword = await db.ContractScanKeywords.SingleOrDefaultAsync(
                candidate => candidate.ContractScanKeywordId == keywordId && candidate.FileStorageFileid == row.FileId,
                cancellationToken)
            ?? throw new InternalAppException(404, "Không tìm thấy từ khóa.");
        if (keyword.ContractScanKeywordSource != (int)ScanKeywordSource.Manual)
        {
            throw new InternalAppException(409, "Chỉ xóa được từ khóa thêm bằng tay.");
        }

        db.ContractScanKeywords.Remove(keyword);
        db.AuditLogs.Add(Audit(KeywordRemovedAction, EvidenceEntity, evidenceId,
            new { type = ((ScanKeywordType)keyword.ContractScanKeywordType).ToString(), value = keyword.ContractScanKeywordValue }, null));
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---------------------------------------------------------------------------------------------
    // Search: keywords of evidence pending or accepted, back to their contract
    // ---------------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<ScanSearchResultDto>> SearchResultsAsync(
        IReadOnlyCollection<ContractScanKeyword> found,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var fileIds = found.Select(keyword => keyword.FileStorageFileid).Distinct().ToList();
        var rows = (await EvidenceRows(fileIds: fileIds).ToListAsync(cancellationToken))
            .Where(row => row.Evidence.ContractSignedEvidenceStatus is (int)SignedEvidenceStatus.PendingReview
                or (int)SignedEvidenceStatus.Accepted)
            .ToList();
        var documentIds = rows.Select(row => row.DocumentId).Distinct().ToList();
        var contracts = await Contracts().Where(row => documentIds.Contains(row.DocumentId))
            .ToDictionaryAsync(row => row.DocumentId, cancellationToken);
        var byFile = found.ToLookup(keyword => keyword.FileStorageFileid);

        return rows
            .Where(row => contracts.ContainsKey(row.DocumentId))
            .OrderByDescending(row => row.Evidence.UploadedAt)
            .Take(maxResults)
            .Select(row =>
            {
                var contract = contracts[row.DocumentId];
                return new ScanSearchResultDto(
                    contract.DocumentId,
                    contract.Code,
                    contract.Name,
                    contract.CustomerName,
                    StatusKey(contract.Status),
                    row.Evidence.ContractSignedEvidenceId,
                    ((SignedEvidenceStatus)row.Evidence.ContractSignedEvidenceStatus).ToString(),
                    row.Evidence.ContractSignedEvidenceSigndate.ToString("yyyy-MM-dd"),
                    byFile[row.FileId]
                        .DistinctBy(keyword => keyword.ContractScanKeywordId)
                        .Select(keyword => new ScanSearchMatchDto(
                            ((ScanKeywordType)keyword.ContractScanKeywordType).ToString(),
                            keyword.ContractScanKeywordValue,
                            keyword.ContractScanKeywordPagenumber,
                            ((ScanKeywordSource)keyword.ContractScanKeywordSource).ToString()))
                        .ToList());
            })
            .ToList();
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------

    private static ArgumentException Invalid(string property, string message) => new(message, property);

    public static string StatusKey(int status) =>
        Enum.IsDefined(typeof(ContractDocumentStatus), status) ? ((ContractDocumentStatus)status).ToString() : status.ToString();

    /// <summary>The database keeps UTC without a kind; the web app needs the "Z".</summary>
    internal static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string? UploadBlockedReason(
        ContractRow contract,
        ContractVersion? version,
        bool signedOnline,
        IReadOnlyList<EvidenceRow> rows)
    {
        if (version is null)
        {
            return "Hợp đồng chưa có phiên bản đã thống nhất.";
        }

        if (signedOnline)
        {
            return "Hợp đồng đang hoặc đã ký online bằng OTP nên không lưu bản ký giấy.";
        }

        if (contract.Status == (int)ContractDocumentStatus.Signed)
        {
            return "Hợp đồng đã ký. Nếu cần, dùng \"Thay bản ký\" ở bản đang hợp lệ.";
        }

        if (contract.Status != (int)ContractDocumentStatus.PendingSignature)
        {
            return "Chỉ tải bản ký giấy khi hợp đồng đang chờ ký.";
        }

        return rows.Any(row => row.VersionId == version.ContractVersionId
            && row.Evidence.ContractSignedEvidenceStatus == (int)SignedEvidenceStatus.PendingReview)
            ? "Đang có một bản ký chờ kiểm tra. Hãy xác nhận hoặc từ chối bản đó trước."
            : null;
    }

    /// <summary>
    /// F18 step 2 and 3, and F19 steps 2 and 3: the file on disk, then in one transaction its
    /// FILE_STORAGE, CONTRACT_DOCUMENT_FILES and CONTRACT_SIGNED_EVIDENCE rows, the OCR job, the audit
    /// row and the notification.
    /// </summary>
    private async Task<long> FileEvidenceAsync(
        ContractRow contract,
        long versionId,
        DateOnly signDate,
        string fileName,
        byte[] content,
        ContractSignedEvidence? replaced,
        string? replaceReason,
        CancellationToken cancellationToken)
    {
        // Any file is stored. A PDF or picture is served back with its own type; anything else as it came.
        var (extension, contentType) = ScanFileKinds.Detect(content) is { } kind
            ? Kinds[kind]
            : (Path.GetExtension(fileName) is { Length: > 1 and <= 10 } own ? own.ToLowerInvariant() : ".bin", "application/octet-stream");
        var employeeId = currentEmployee.EmployeeId;
        var now = DateTime.UtcNow;
        var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        // A new key every time: the bytes of a stored evidence are never overwritten.
        var storageKey = $"signed-evidence/{contract.DocumentId}/{versionId}/{Guid.NewGuid():N}{extension}";

        // The stored copy is encrypted; OCR reads a plain copy that is deleted once it is read.
        var (storedBytes, fileKey) = protection.Protect(content);
        await files.SaveAsync(storageKey, storedBytes, cancellationToken);
        long? stagedFileId = null;
        try
        {
            // Serializable: two uploads at the same time cannot both pass the "nothing pending" check.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var pending = await (
                    from waiting in db.ContractSignedEvidences
                    join waitingFile in db.ContractDocumentFiles on waiting.ContractDocumentFileId equals waitingFile.ContractDocumentFileId
                    where waitingFile.ContractDocumentId == contract.DocumentId
                        && waitingFile.ContractVersionId == versionId
                        && waiting.ContractSignedEvidenceStatus == (int)SignedEvidenceStatus.PendingReview
                    select waiting.ContractSignedEvidenceId)
                .AnyAsync(cancellationToken);
            if (pending)
            {
                throw new InternalAppException(409, "Đang có một bản ký chờ kiểm tra. Hãy tải lại trang.");
            }

            var file = new FileStorage
            {
                FileStorageObjectid = contract.DocumentId,
                FileStorageObjecttype = FileObjectType,
                FileStorageFiletype = extension.TrimStart('.').ToUpperInvariant(),
                FileStorageFilename = Path.GetFileName(fileName),
                FileStorageFilepath = storageKey,
                FileStorageStoragekey = storageKey,
                FileStorageContenttype = contentType,
                FileStorageFilesize = content.LongLength,
                FileStorageSha256 = sha256,
                FileStorageUploadedbyemployeeid = employeeId,
                FileStorageUploadeddate = now,
            };
            db.FileStorages.Add(file);
            await db.SaveChangesAsync(cancellationToken);
            await protection.GrantAsync(file.FileStorageFileid, fileKey, await FileReadersAsync(contract, employeeId, cancellationToken),
                cancellationToken);
            await staging.SaveAsync(file.FileStorageFileid, content, cancellationToken);
            stagedFileId = file.FileStorageFileid;

            var documentFile = new ContractDocumentFile
            {
                ContractDocumentId = contract.DocumentId,
                ContractVersionId = versionId,
                FileStorageFileid = file.FileStorageFileid,
                ContractDocumentFileRole = ContractDocumentFileRoles.SignedPaper,
                // Becomes the primary file of its role once the evidence is accepted.
                IsPrimary = false,
                CreatedDate = now,
                CreatedEmployeeId = employeeId,
            };
            db.ContractDocumentFiles.Add(documentFile);
            await db.SaveChangesAsync(cancellationToken);

            var evidence = new ContractSignedEvidence
            {
                ContractDocumentFileId = documentFile.ContractDocumentFileId,
                ContractSignedEvidenceStatus = (int)SignedEvidenceStatus.PendingReview,
                ContractSignedEvidenceSigndate = signDate,
                UploadedByEmployeeid = employeeId,
                UploadedAt = now,
                SupersedesEvidenceId = replaced?.ContractSignedEvidenceId,
                ContractSignedEvidenceSupersedereason = replaceReason,
            };
            db.ContractSignedEvidences.Add(evidence);
            await db.SaveChangesAsync(cancellationToken);

            // One upload is one batch of one file in the OCR tables of MasterDB-Optimize_v1.
            var batch = new OcrBatch
            {
                UploadedByEmployeeId = employeeId,
                TotalFiles = 1,
                BatchStatus = OcrBatchStatuses.Pending,
                CreatedAt = now,
            };
            db.OcrBatches.Add(batch);
            await db.SaveChangesAsync(cancellationToken);
            db.OcrJobs.Add(new OcrJob
            {
                OcrBatchId = batch.OcrPatchId,
                FileStorageFileid = file.FileStorageFileid,
                ContractDocumentId = contract.DocumentId,
                OcrJobProcessingStatus = OcrJobStatuses.Queued,
                ScannedEmployeeId = employeeId,
                ScannedAt = now,
            });
            db.AuditLogs.Add(Audit(UploadedAction, EvidenceEntity, evidence.ContractSignedEvidenceId, null, new
            {
                fileId = file.FileStorageFileid,
                versionId,
                signDate,
                sha256,
                supersedes = replaced?.ContractSignedEvidenceId,
                reason = replaceReason,
            }));
            await NotifyAsync(contract, evidence.ContractSignedEvidenceId, "SIGNED_EVIDENCE_UPLOADED",
                $"Bản ký giấy chờ kiểm tra: {contract.Code ?? contract.Name}",
                replaced is null
                    ? $"Bản ký giấy ngày {signDate:dd/MM/yyyy} đã được tải lên, cần kiểm tra chữ ký và dấu trước khi ghi nhận."
                    : $"Bản ký thay thế đã được tải lên, cần kiểm tra. Lý do thay: {replaceReason}",
                employeeId, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return evidence.ContractSignedEvidenceId;
        }
        catch
        {
            // Orphan file of a transaction that did not commit (Huong dan F07 step 6).
            await files.DeleteAsync(storageKey, CancellationToken.None);
            if (stagedFileId is { } staged)
            {
                await staging.DeleteAsync(staged, CancellationToken.None);
            }

            throw;
        }
    }

    /// <summary>
    /// Who may open a contract's paper-signing files: the employee in charge, whoever uploaded one, and every
    /// checker (they see every contract). The same people Contracts() lets see the contract.
    /// </summary>
    private async Task<IReadOnlyCollection<long>> FileReadersAsync(ContractRow contract, long uploaderId, CancellationToken cancellationToken)
    {
        var readers = new HashSet<long> { contract.OwnerId, uploaderId };
        readers.UnionWith(await db.ContractSignedEvidences.AsNoTracking()
            .Join(db.ContractDocumentFiles, evidence => evidence.ContractDocumentFileId, file => file.ContractDocumentFileId,
                (evidence, file) => new { evidence.UploadedByEmployeeid, file.ContractDocumentId })
            .Where(row => row.ContractDocumentId == contract.DocumentId)
            .Select(row => row.UploadedByEmployeeid)
            .ToListAsync(cancellationToken));
        readers.UnionWith(await protection.EmployeesWithPermissionAsync(SigningPermissions.Review, cancellationToken));
        return readers;
    }

    private async Task<byte[]> OpenStoredAsync(EvidenceRow row, string? fileKey, CancellationToken cancellationToken)
    {
        var stored = await files.ReadAsync(row.StorageKey, cancellationToken)
            ?? throw new InternalAppException(404, "Không tìm thấy tệp của bản ký.");
        var contract = await GetContractAsync(row.DocumentId, cancellationToken);
        return await protection.OpenAsync(row.FileId, stored, fileKey,
            await FileReadersAsync(contract, row.Evidence.UploadedByEmployeeid, cancellationToken), cancellationToken);
    }

    /// <summary>The accepted evidence's file becomes the primary signed-paper file of its version.</summary>
    private async Task MakePrimaryAsync(EvidenceRow row, CancellationToken cancellationToken)
    {
        await db.ContractDocumentFiles
            .Where(file => file.ContractDocumentId == row.DocumentId
                && file.ContractVersionId == row.VersionId
                && file.ContractDocumentFileRole == ContractDocumentFileRoles.SignedPaper
                && file.ContractDocumentFileId != row.Evidence.ContractDocumentFileId
                && file.IsPrimary)
            .ExecuteUpdateAsync(setters => setters.SetProperty(file => file.IsPrimary, false), cancellationToken);
        await db.ContractDocumentFiles
            .Where(file => file.ContractDocumentFileId == row.Evidence.ContractDocumentFileId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(file => file.IsPrimary, true), cancellationToken);
    }

    private AuditLog Audit(string action, string entity, long entityId, object? oldData, object? newData) => new()
    {
        ActionType = action,
        EntityName = entity,
        EntityId = entityId.ToString(),
        ActorType = "Employee",
        ActorId = currentEmployee.EmployeeId,
        OldData = oldData is null ? null : JsonSerializer.Serialize(oldData, Json),
        NewData = newData is null ? null : JsonSerializer.Serialize(newData, Json),
        CreatedAt = DateTime.UtcNow,
    };

    /// <summary>F29: one notification for the contract owner and the uploader, never twice for the same event.</summary>
    private async Task NotifyAsync(
        ContractRow contract,
        long evidenceId,
        string eventCode,
        string title,
        string body,
        long uploaderId,
        CancellationToken cancellationToken)
    {
        var eventKey = $"signed-evidence:{evidenceId}:{eventCode}";
        if (await db.Notifications.AnyAsync(notification => notification.NotificationEventkey == eventKey, cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var notification = new Notification
        {
            NotificationEventkey = eventKey,
            NotificationModulecode = NotificationModule,
            NotificationEventcode = eventCode,
            NotificationSubjecttype = DocumentEntity,
            NotificationSubjectid = contract.DocumentId,
            NotificationTitle = title.Length > 600 ? title[..600] : title,
            NotificationBody = body.Length > 2000 ? body[..2000] : body,
            EmployeeId = currentEmployee.EmployeeId,
            NotificationCreatedatutc = now,
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var recipient in new[] { contract.OwnerId, uploaderId }.Distinct())
        {
            db.NotificationRecipients.Add(new NotificationRecipient
            {
                NotificationId = notification.NotificationId,
                EmployeeId = recipient,
                NotificationRecipientCreatedatutc = now,
            });
        }
    }

    private async Task<ContractRow> GetContractAsync(long documentId, CancellationToken cancellationToken) =>
        await Contracts().SingleOrDefaultAsync(row => row.DocumentId == documentId, cancellationToken)
        ?? throw new InternalAppException(404, "Không tìm thấy hợp đồng.");

    /// <summary>An evidence of a contract outside the data scope is "not found" too.</summary>
    private async Task<EvidenceRow> GetEvidenceRowAsync(long evidenceId, CancellationToken cancellationToken)
    {
        var row = await EvidenceRows(evidenceId: evidenceId).SingleOrDefaultAsync(cancellationToken);
        if (row is null || !await Contracts().AnyAsync(contract => contract.DocumentId == row.DocumentId, cancellationToken))
        {
            throw new InternalAppException(404, "Không tìm thấy bản ký.");
        }

        return row;
    }

    private async Task<HashSet<long>> SignedOnlineVersionsAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken) =>
        (await db.ContractElectronicSignatures.AsNoTracking()
            .Where(signature => versionIds.Contains(signature.ContractVersionId)
                && signature.ContractElectronicStatus == (int)ElectronicSignatureStatus.Signed)
            .Select(signature => signature.ContractVersionId)
            .Distinct()
            .ToListAsync(cancellationToken))
        .ToHashSet();

    // Filters go before the projection: EF cannot translate a filter on the record it builds.
    private IQueryable<EvidenceRow> EvidenceRows(
        IReadOnlyCollection<long>? documentIds = null,
        long? evidenceId = null,
        IReadOnlyCollection<long>? fileIds = null) =>
        from evidence in db.ContractSignedEvidences.AsNoTracking()
        join documentFile in db.ContractDocumentFiles on evidence.ContractDocumentFileId equals documentFile.ContractDocumentFileId
        where (documentIds == null || documentIds.Contains(documentFile.ContractDocumentId))
            && (evidenceId == null || evidence.ContractSignedEvidenceId == evidenceId)
            && (fileIds == null || fileIds.Contains(documentFile.FileStorageFileid))
        join file in db.FileStorages on documentFile.FileStorageFileid equals file.FileStorageFileid
        join version in db.ContractVersions on documentFile.ContractVersionId equals (long?)version.ContractVersionId
        select new EvidenceRow(
            evidence,
            documentFile.ContractDocumentId,
            version.ContractVersionId,
            version.ContractVersionVersionNo,
            file.FileStorageFileid,
            file.FileStorageFilename,
            file.FileStorageContenttype,
            file.FileStorageFilesize,
            file.FileStorageSha256,
            file.FileStorageStoragekey);

    /// <summary>A business customer is named and taxed by its company; a person by their own details.</summary>
    /// <summary>
    /// The contracts this employee may see (guide 2.1: the permission and the data scope are checked together;
    /// F27: check the access before reading). A checker sees every contract; anyone else the contracts they are
    /// in charge of (CONTRACT_DOCUMENTS.employee_id) or uploaded a paper-signing evidence for.
    /// A contract outside the scope answers "not found", as if it did not exist.
    /// </summary>
    private IQueryable<ContractRow> Contracts()
    {
        var seesAll = currentEmployee.HasPermission(SigningPermissions.Review);
        var me = currentEmployee.EmployeeId;
        return
        from document in db.ContractDocuments.AsNoTracking()
        where seesAll
            || document.EmployeeId == me
            || (from evidence in db.ContractSignedEvidences
                join file in db.ContractDocumentFiles on evidence.ContractDocumentFileId equals file.ContractDocumentFileId
                where file.ContractDocumentId == document.ContractDocumentId && evidence.UploadedByEmployeeid == me
                select evidence.ContractSignedEvidenceId).Any()
        join customer in db.Customers on document.CustomerId equals customer.CustomerId into customers
        from customer in customers.DefaultIfEmpty()
        join business in db.CustomerBusinesses on document.CustomerId equals business.CustomerId into businesses
        from business in businesses.DefaultIfEmpty()
        select new ContractRow
        {
            DocumentId = document.ContractDocumentId,
            Code = document.ContractDocumentCode,
            Name = document.ContractDocumentName,
            Status = document.ContractDocumentStatus,
            CurrentVersionId = document.ContractDocumentCurrentVersionId,
            OwnerId = document.EmployeeId,
            CustomerName = business != null
                ? business.CustomerBusinessLegalName
                : customer != null ? customer.CustomerDisplayName : "",
            TaxCode = business != null
                ? business.CustomerBusinessCompanyTaxIdentifier
                : customer != null ? customer.CustomerPersonalTaxIdentifier : null,
            Phone = customer != null ? customer.CustomerPersonalPhone : null,
        };
    }

    private async Task<IReadOnlyList<SignedEvidenceDto>> ToDtosAsync(
        ContractRow contract,
        IReadOnlyList<EvidenceRow> rows,
        CancellationToken cancellationToken)
    {
        var fileIds = rows.Select(row => row.FileId).ToList();
        var jobs = (await db.OcrJobs.AsNoTracking()
                .Where(job => fileIds.Contains(job.FileStorageFileid))
                .ToListAsync(cancellationToken))
            .GroupBy(job => job.FileStorageFileid)
            .ToDictionary(group => group.Key, group => group.MaxBy(job => job.OcrJobId)!);
        var keywords = (await db.ContractScanKeywords.AsNoTracking()
                .Where(keyword => fileIds.Contains(keyword.FileStorageFileid))
                .ToListAsync(cancellationToken))
            .ToLookup(keyword => keyword.FileStorageFileid);
        var evidenceKeys = rows.Select(row => row.Evidence.ContractSignedEvidenceId.ToString()).ToList();
        var reviews = (await db.AuditLogs.AsNoTracking()
                .Where(log => log.EntityName == EvidenceEntity
                    && (log.ActionType == AcceptedAction || log.ActionType == RejectedAction)
                    && evidenceKeys.Contains(log.EntityId))
                .ToListAsync(cancellationToken))
            .GroupBy(log => long.Parse(log.EntityId))
            .ToDictionary(group => group.Key, group => group.MaxBy(log => log.CreatedAt)!);
        var people = await PeopleAsync(
            rows.Select(row => (long?)row.Evidence.UploadedByEmployeeid)
                .Concat(rows.Select(row => row.Evidence.SupersededByEmployeeId))
                .Concat(reviews.Values.Select(log => log.ActorId)),
            cancellationToken);
        string Name(long? id) => id is { } value ? people.GetValueOrDefault(value)?.DisplayName ?? $"#{value}" : "";

        return rows.Select(row =>
        {
            var evidence = row.Evidence;
            var state = KeywordState(jobs.GetValueOrDefault(row.FileId), keywords[row.FileId].ToList(), ocrOptions.Value.KeywordMaxPages);
            var checks = ContentChecks(contract, state);
            var review = reviews.GetValueOrDefault(evidence.ContractSignedEvidenceId) is { } log
                ? new EvidenceReviewDto(
                    log.ActionType == AcceptedAction ? "Accepted" : "Rejected",
                    Name(log.ActorId),
                    Utc(log.CreatedAt),
                    ReadText(log.NewData, log.ActionType == AcceptedAction ? "note" : "reason"))
                : null;
            return new SignedEvidenceDto(
                evidence.ContractSignedEvidenceId,
                row.VersionId,
                row.VersionNo,
                ((SignedEvidenceStatus)evidence.ContractSignedEvidenceStatus).ToString(),
                evidence.ContractSignedEvidenceSigndate.ToString("yyyy-MM-dd"),
                row.FileName,
                row.ContentType,
                row.FileSize,
                state.PageCount,
                row.Sha256,
                people.GetValueOrDefault(evidence.UploadedByEmployeeid)
                    ?? new PersonRefDto(evidence.UploadedByEmployeeid, $"#{evidence.UploadedByEmployeeid}", null),
                Utc(evidence.UploadedAt),
                evidence.SupersedesEvidenceId,
                evidence.ContractSignedEvidenceSupersedereason,
                evidence.SupersededAt is { } at ? Utc(at) : null,
                evidence.SupersededByEmployeeId is null ? null : Name(evidence.SupersededByEmployeeId),
                review,
                state,
                checks,
                NeedsNote(state, checks));
        }).ToList();
    }

    private static string? ReadText(string? json, string property)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>F18 step 1: the contract code, customer tax code, phone and name read on the paper, against the contract.</summary>
    private static IReadOnlyList<ContentCheckDto> ContentChecks(ContractRow contract, ScanKeywordStateDto state)
    {
        var done = state.Status == nameof(ScanKeywordJobStatus.Done);
        ContentCheckDto Check(ScanKeywordType type, string? expected, Func<string, string> normalise)
        {
            var found = state.Items.Where(item => item.Type == type.ToString()).Select(item => item.Value).Distinct().ToList();
            var result = !done ? "Pending"
                : string.IsNullOrWhiteSpace(expected) ? "NotFound"
                : found.Count == 0 ? "NotFound"
                : found.Any(value => normalise(value) == normalise(expected)) ? "Match"
                : "Mismatch";
            return new ContentCheckDto(type.ToString(), expected, found, result);
        }

        static string Code(string value) => value.Trim().ToUpperInvariant().Replace(" ", "");
        static string Digits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());
        static string Name(string value) =>
            string.Join(' ', VietnameseText.StripDiacritics(value).ToUpperInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return
        [
            Check(ScanKeywordType.ContractCode, contract.Code, Code),
            Check(ScanKeywordType.TaxCode, contract.TaxCode, Digits),
            Check(ScanKeywordType.PhoneNumber, contract.Phone, Digits),
            Check(ScanKeywordType.CustomerName, contract.CustomerName, Name),
        ];
    }

    /// <summary>The paper could not be compared, or something on it differs: the checker writes down why it is still valid.</summary>
    private static bool NeedsNote(ScanKeywordStateDto state, IReadOnlyList<ContentCheckDto> checks) =>
        state.Status == nameof(ScanKeywordJobStatus.Failed) || checks.Any(check => check.Result == "Mismatch");

    /// <summary>The OCR state of one file, as both the signed-evidence and the scanned-contract screens show it.</summary>
    internal static ScanKeywordStateDto KeywordState(OcrJob? job, IReadOnlyList<ContractScanKeyword> keywords, int maxPages)
    {
        var status = OcrJobStatuses.ToApiStatus(job?.OcrJobProcessingStatus);
        var items = keywords
            .Where(keyword => keyword.ContractScanKeywordSource == (int)ScanKeywordSource.Ocr)
            .OrderBy(keyword => keyword.ContractScanKeywordType)
            .ThenBy(keyword => keyword.ContractScanKeywordPagenumber)
            .Concat(keywords
                .Where(keyword => keyword.ContractScanKeywordSource == (int)ScanKeywordSource.Manual)
                .OrderBy(keyword => keyword.CreatedAt))
            .Select(ToDto)
            .ToList();
        return new ScanKeywordStateDto(
            status.ToString(),
            status == ScanKeywordJobStatus.Done && job?.PageCount is { } pages ? Math.Min(pages, maxPages) : null,
            job?.PageCount,
            status == ScanKeywordJobStatus.Failed ? job?.ErrorMessage : null,
            job?.CompletedAt is { } completedAt ? Utc(completedAt) : null,
            items);
    }

    internal static StoredScanKeywordDto ToDto(ContractScanKeyword keyword) => new(
        keyword.ContractScanKeywordId,
        ((ScanKeywordType)keyword.ContractScanKeywordType).ToString(),
        keyword.ContractScanKeywordValue,
        keyword.ContractScanKeywordPagenumber,
        ((ScanKeywordSource)keyword.ContractScanKeywordSource).ToString());

    private async Task<IReadOnlyList<SignedEvidenceEventDto>> HistoryAsync(
        IReadOnlyList<EvidenceRow> rows,
        CancellationToken cancellationToken)
    {
        var keys = rows.Select(row => row.Evidence.ContractSignedEvidenceId.ToString()).ToList();
        var logs = await db.AuditLogs.AsNoTracking()
            .Where(log => log.EntityName == EvidenceEntity && keys.Contains(log.EntityId))
            .ToListAsync(cancellationToken);
        var people = await PeopleAsync(logs.Select(log => log.ActorId), cancellationToken);
        return logs
            .Select(log => new SignedEvidenceEventDto(
                Utc(log.CreatedAt),
                log.ActionType switch
                {
                    UploadedAction => "Uploaded",
                    AcceptedAction => "Accepted",
                    RejectedAction => "Rejected",
                    SupersededAction => "Superseded",
                    KeywordAddedAction => "KeywordAdded",
                    KeywordRemovedAction => "KeywordRemoved",
                    var other => other,
                },
                log.ActorId is { } actor ? people.GetValueOrDefault(actor)?.DisplayName ?? $"#{actor}" : null,
                long.Parse(log.EntityId),
                log.ActionType switch
                {
                    AcceptedAction => ReadText(log.NewData, "note"),
                    RejectedAction => ReadText(log.NewData, "reason"),
                    UploadedAction => ReadText(log.NewData, "reason"),
                    KeywordAddedAction => ReadText(log.NewData, "value"),
                    KeywordRemovedAction => ReadText(log.OldData, "value"),
                    _ => null,
                }))
            .OrderByDescending(item => item.At)
            .ToList();
    }

    private async Task<Dictionary<long, PersonRefDto>> PeopleAsync(IEnumerable<long?> employeeIds, CancellationToken cancellationToken)
    {
        var ids = employeeIds.OfType<long>().Distinct().ToList();
        var employees = await db.Employees.AsNoTracking()
            .Where(employee => ids.Contains(employee.EmployeeId))
            .Select(employee => new { employee.EmployeeId, employee.EmployeeDisplayName })
            .ToListAsync(cancellationToken);
        var roles = (await (
                from employeeRole in db.EmployeeRoles.AsNoTracking()
                join role in db.SystemRoles on employeeRole.SystemRoleId equals role.SystemRoleId
                where ids.Contains(employeeRole.EmployeeId)
                select new { employeeRole.EmployeeId, role.SystemRoleName })
            .ToListAsync(cancellationToken))
            .GroupBy(item => item.EmployeeId)
            .ToDictionary(group => group.Key, group => group.First().SystemRoleName);
        return employees.ToDictionary(
            employee => employee.EmployeeId,
            employee => new PersonRefDto(employee.EmployeeId, employee.EmployeeDisplayName, roles.GetValueOrDefault(employee.EmployeeId)));
    }
}
