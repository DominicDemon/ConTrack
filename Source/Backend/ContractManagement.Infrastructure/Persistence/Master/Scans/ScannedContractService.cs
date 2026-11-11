using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal.Scans;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

/// <summary>
/// Old paper contracts brought into the system by scanning (CONTRACTS.source_type = SCANNED, MasterDB-Optimize_v1):
/// 1. upload one or more scans: one OCR_BATCH, one FILE_STORAGE and one OCR_JOB per file;
/// 2. OCR runs in the background (ScanKeywordJobRunner) and keeps the text and the keywords;
/// 3. once read, the scan is kept and found by search; staff fix keywords OCR read wrong;
/// 4. optionally, the checker creates a CONTRACTS row from it, to manage it with the other contracts.
/// Not in Master_Database_HuongDan (written for the older script): inferred from the tables, see ScannedContractCodes.
/// </summary>
public sealed class ScannedContractService(
    MasterDbContext db,
    IFileStore files,
    ICurrentEmployee currentEmployee,
    IOptions<ScanOcrOptions> ocrOptions,
    FileProtectionService protection,
    IOcrStagingStore staging)
{
    public const string BatchEntity = "OCR_BATCH";
    public const string ContractEntity = "CONTRACTS";
    public const string BatchUploadedAction = "ScannedBatchUploaded";
    public const string ContractCreatedAction = "ScannedContractCreated";
    public const string JobEntity = "OCR_JOB";
    public const string KeywordAddedAction = "ScanKeywordAdded";
    public const string KeywordRemovedAction = "ScanKeywordRemoved";

    /// <summary>One upload; a scanner usually makes one PDF per contract.</summary>
    public const int MaxFilesPerBatch = 20;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<ScanFileKind, (string Extension, string ContentType)> Kinds = new()
    {
        [ScanFileKind.Pdf] = (".pdf", "application/pdf"),
        [ScanFileKind.Png] = (".png", "image/png"),
        [ScanFileKind.Jpeg] = (".jpg", "image/jpeg"),
    };

    /// <summary>A checker (contract.scanned.confirm) sees every batch and contract; anyone else their own.</summary>
    private bool SeesAll => currentEmployee.HasPermission(ScannedContractPermissions.Confirm);

    // ---------------------------------------------------------------------------------------------
    // 1. Upload
    // ---------------------------------------------------------------------------------------------

    public async Task<long> UploadBatchAsync(IReadOnlyList<(string FileName, byte[] Content)> scans, CancellationToken cancellationToken)
    {
        var employeeId = currentEmployee.EmployeeId;
        var now = DateTime.UtcNow;
        var stored = new List<(string FileName, byte[] Content, string Key, string Extension, string ContentType, byte[]? FileKey)>();
        var staged = new List<long>();
        foreach (var (fileName, content) in scans)
        {
            // Any file is kept; OCR reads PDF, JPG and PNG, anything else ends FAILED and is checked by eye.
            var (extension, contentType) = ScanFileKinds.Detect(content) is { } kind
                ? Kinds[kind]
                : (Path.GetExtension(fileName) is { Length: > 1 and <= 10 } own ? own.ToLowerInvariant() : ".bin", "application/octet-stream");
            var key = $"scanned-contracts/{now:yyyyMM}/{Guid.NewGuid():N}{extension}";
            // The stored copy is encrypted; OCR reads a plain copy that is deleted once it is read.
            var (storedBytes, fileKey) = protection.Protect(content);
            await files.SaveAsync(key, storedBytes, cancellationToken);
            stored.Add((fileName, content, key, extension, contentType, fileKey));
        }

        try
        {
            return await db.ExecuteInTransactionAsync(async () =>
            {
                foreach (var previousFileId in staged)
                {
                    await staging.DeleteAsync(previousFileId, CancellationToken.None);
                }

                staged.Clear();
                var batch = new OcrBatch
                {
                    UploadedByEmployeeId = employeeId,
                    TotalFiles = stored.Count,
                    BatchStatus = OcrBatchStatuses.Pending,
                    CreatedAt = now,
                };
                db.OcrBatches.Add(batch);
                await db.SaveChangesAsync(cancellationToken);
                var readers = await FileReadersAsync(employeeId, null, cancellationToken);

                foreach (var scan in stored)
                {
                    var file = new FileStorage
                    {
                        // Until the contract exists the file belongs to its batch; then to the CONTRACTS row.
                        FileStorageObjecttype = ScannedContractCodes.FileObjectBatch,
                        FileStorageObjectid = batch.OcrPatchId,
                        FileStorageFiletype = scan.Extension.TrimStart('.').ToUpperInvariant(),
                        FileStorageFilename = Path.GetFileName(scan.FileName),
                        FileStorageFilepath = scan.Key,
                        FileStorageStoragekey = scan.Key,
                        FileStorageContenttype = scan.ContentType,
                        FileStorageFilesize = scan.Content.LongLength,
                        FileStorageSha256 = Convert.ToHexString(SHA256.HashData(scan.Content)).ToLowerInvariant(),
                        FileStorageUploadedbyemployeeid = employeeId,
                        FileStorageUploadeddate = now,
                    };
                    db.FileStorages.Add(file);
                    await db.SaveChangesAsync(cancellationToken);
                    await protection.GrantAsync(file.FileStorageFileid, scan.FileKey, readers, cancellationToken);
                    await staging.SaveAsync(file.FileStorageFileid, scan.Content, cancellationToken);
                    staged.Add(file.FileStorageFileid);
                    db.OcrJobs.Add(new OcrJob
                    {
                        OcrBatchId = batch.OcrPatchId,
                        FileStorageFileid = file.FileStorageFileid,
                        // Not a contract drafted in the system: no CONTRACT_DOCUMENTS row.
                        ContractDocumentId = null,
                        OcrJobProcessingStatus = OcrJobStatuses.Queued,
                        ScannedEmployeeId = employeeId,
                        ScannedAt = now,
                    });
                }

                db.AuditLogs.Add(Audit(BatchUploadedAction, BatchEntity, batch.OcrPatchId, new
                {
                    files = stored.Select(scan => scan.FileName).ToList(),
                }));
                await db.SaveChangesAsync(cancellationToken);
                return batch.OcrPatchId;
            }, cancellationToken);
        }
        catch
        {
            // No row points to the bytes: remove them.
            foreach (var scan in stored)
            {
                await files.DeleteAsync(scan.Key, CancellationToken.None);
            }

            foreach (var fileId in staged)
            {
                await staging.DeleteAsync(fileId, CancellationToken.None);
            }

            throw;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Reading
    // ---------------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<ScannedBatchDto>> ListBatchesAsync(CancellationToken cancellationToken)
    {
        var me = currentEmployee.EmployeeId;
        var seesAll = SeesAll;
        var batches = await db.OcrBatches.AsNoTracking()
            .Where(batch => seesAll || batch.UploadedByEmployeeId == me)
            // Only batches of scanned contracts: their jobs have no CONTRACT_DOCUMENTS row.
            .Where(batch => db.OcrJobs.Any(job => job.OcrBatchId == batch.OcrPatchId && job.ContractDocumentId == null))
            .OrderByDescending(batch => batch.OcrPatchId)
            .Take(50)
            .ToListAsync(cancellationToken);
        return await ToBatchDtosAsync(batches, cancellationToken);
    }

    public async Task<ScannedBatchDto> GetBatchAsync(long batchId, CancellationToken cancellationToken)
    {
        var batch = await ScopedBatches().SingleOrDefaultAsync(candidate => candidate.OcrPatchId == batchId, cancellationToken)
            ?? throw new InternalAppException(404, "Không tìm thấy lượt scan.");
        return (await ToBatchDtosAsync([batch], cancellationToken))[0];
    }

    public async Task<ScannedJobDetailDto> GetJobAsync(long jobId, CancellationToken cancellationToken)
    {
        var (job, file) = await GetJobRowAsync(jobId, cancellationToken);
        var keywords = await db.ContractScanKeywords.AsNoTracking()
            .Where(keyword => keyword.FileStorageFileid == file.FileStorageFileid)
            .ToListAsync(cancellationToken);
        var state = SignedEvidenceService.KeywordState(job, keywords, ocrOptions.Value.KeywordMaxPages);
        var contract = await db.Contracts.AsNoTracking()
            .Where(candidate => candidate.FileStorageFileid == file.FileStorageFileid
                && candidate.SourceType == ScannedContractCodes.SourceScanned)
            .FirstOrDefaultAsync(cancellationToken);
        var people = await PeopleAsync([file.FileStorageUploadedbyemployeeid, contract?.EmployeeId], cancellationToken);

        return new ScannedJobDetailDto(
            job.OcrJobId,
            job.OcrBatchId,
            file.FileStorageFileid,
            file.FileStorageFilename,
            file.FileStorageContenttype,
            file.FileStorageFilesize,
            file.FileStorageSha256,
            Name(people, file.FileStorageUploadedbyemployeeid),
            SignedEvidenceService.Utc(file.FileStorageUploadeddate),
            job.OcrJobProcessingStatus,
            state,
            await SuggestAsync(keywords, cancellationToken),
            contract is null ? null : await ToContractDtoAsync(contract, cancellationToken));
    }

    public async Task<ScannedFileDto> GetJobFileAsync(long jobId, string? fileKey, CancellationToken cancellationToken)
    {
        var (_, file) = await GetJobRowAsync(jobId, cancellationToken);
        return await ReadFileAsync(file, fileKey, cancellationToken);
    }

    public async Task<IReadOnlyList<ScannedContractDto>> ListContractsAsync(CancellationToken cancellationToken)
    {
        var contracts = await ScopedContracts()
            .OrderByDescending(contract => contract.ContractId)
            .Take(200)
            .ToListAsync(cancellationToken);
        var result = new List<ScannedContractDto>(contracts.Count);
        foreach (var contract in contracts)
        {
            result.Add(await ToContractDtoAsync(contract, cancellationToken));
        }

        return result;
    }

    public async Task<ScannedFileDto> GetContractFileAsync(long contractId, string? fileKey, CancellationToken cancellationToken)
    {
        var contract = await ScopedContracts().SingleOrDefaultAsync(candidate => candidate.ContractId == contractId, cancellationToken)
            ?? throw new InternalAppException(404, "Không tìm thấy hợp đồng.");
        var file = await db.FileStorages.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.FileStorageFileid == contract.FileStorageFileid, cancellationToken)
            ?? throw new InternalAppException(404, "Không tìm thấy tệp của hợp đồng.");
        return await ReadFileAsync(file, fileKey, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerOptionDto>> FindCustomersAsync(string? query, CancellationToken cancellationToken)
    {
        var text = query?.Trim() ?? "";
        var digits = new string(text.Where(char.IsDigit).ToArray());
        var rows =
            from customer in db.Customers.AsNoTracking()
            join business in db.CustomerBusinesses on customer.CustomerId equals business.CustomerId into businesses
            from business in businesses.DefaultIfEmpty()
            select new { customer, business };
        if (text.Length > 0)
        {
            rows = rows.Where(row =>
                EF.Functions.Collate(row.customer.CustomerDisplayName, LooseCollation).Contains(text)
                || (row.business != null && EF.Functions.Collate(row.business.CustomerBusinessLegalName, LooseCollation).Contains(text))
                || (digits.Length >= 3 && (row.customer.CustomerPersonalPhone.Contains(digits)
                    || (row.business != null && row.business.CustomerBusinessCompanyTaxIdentifier.Contains(digits)))));
        }

        return (await rows.OrderBy(row => row.customer.CustomerId).Take(20).ToListAsync(cancellationToken))
            .Select(row => ToCustomerOption(row.customer, row.business))
            .ToList();
    }

    public async Task<IReadOnlyList<ContractTypeOptionDto>> ListContractTypesAsync(CancellationToken cancellationToken) =>
        await db.ContractTypes.AsNoTracking()
            .OrderBy(type => type.ContractTypeName)
            .Select(type => new ContractTypeOptionDto(type.ContractTypeId, type.ContractTypeCode, type.ContractTypeName))
            .ToListAsync(cancellationToken);

    // ---------------------------------------------------------------------------------------------
    // 3. Keywords and search
    // ---------------------------------------------------------------------------------------------

    /// <summary>A keyword OCR missed or read wrong, typed by staff; searchable at once.</summary>
    public async Task<StoredScanKeywordDto> AddKeywordAsync(long jobId, string type, string value, CancellationToken cancellationToken)
    {
        var (job, file) = await GetJobRowAsync(jobId, cancellationToken);
        if (job.OcrJobProcessingStatus is OcrJobStatuses.Queued or OcrJobStatuses.Processing)
        {
            throw new InternalAppException(409, "Hệ thống đang đọc bản scan. Hãy chờ đọc xong rồi sửa từ khóa.");
        }

        var (keywordType, normalised) = SignedEvidenceService.ParseTypedKeyword(type, value);
        if (await db.ContractScanKeywords.AnyAsync(
                keyword => keyword.FileStorageFileid == file.FileStorageFileid
                    && keyword.ContractScanKeywordType == (int)keywordType
                    && keyword.ContractScanKeywordValue == normalised,
                cancellationToken))
        {
            throw new InternalAppException(409, "Từ khóa này đã có.");
        }

        var keyword = new ContractScanKeyword
        {
            FileStorageFileid = file.FileStorageFileid,
            ContractScanKeywordType = (int)keywordType,
            ContractScanKeywordValue = normalised,
            ContractScanKeywordSource = (int)ScanKeywordSource.Manual,
            CreatedByEmployeeId = currentEmployee.EmployeeId,
            CreatedAt = DateTime.UtcNow,
        };
        db.ContractScanKeywords.Add(keyword);
        db.AuditLogs.Add(Audit(KeywordAddedAction, JobEntity, jobId, new { type = keywordType.ToString(), value = normalised }));
        await db.SaveChangesAsync(cancellationToken);
        return SignedEvidenceService.ToDto(keyword);
    }

    /// <summary>Only keywords typed by hand are removed; what OCR read stays as read.</summary>
    public async Task RemoveKeywordAsync(long jobId, long keywordId, CancellationToken cancellationToken)
    {
        var (_, file) = await GetJobRowAsync(jobId, cancellationToken);
        var keyword = await db.ContractScanKeywords.SingleOrDefaultAsync(
                candidate => candidate.ContractScanKeywordId == keywordId && candidate.FileStorageFileid == file.FileStorageFileid,
                cancellationToken)
            ?? throw new InternalAppException(404, "Không tìm thấy từ khóa.");
        if (keyword.ContractScanKeywordSource != (int)ScanKeywordSource.Manual)
        {
            throw new InternalAppException(409, "Chỉ xóa được từ khóa thêm bằng tay.");
        }

        db.ContractScanKeywords.Remove(keyword);
        db.AuditLogs.Add(Audit(KeywordRemovedAction, JobEntity, jobId,
            new { type = ((ScanKeywordType)keyword.ContractScanKeywordType).ToString(), value = keyword.ContractScanKeywordValue }));
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The scans (with or without a contract made from them) that carry the keywords found, among the batches this
    /// employee may see. Nothing for an employee without the scanned-contract permission.
    /// </summary>
    public async Task<IReadOnlyList<ScannedSearchResultDto>> SearchResultsAsync(
        IReadOnlyCollection<ContractScanKeyword> found,
        int maxResults,
        CancellationToken cancellationToken)
    {
        if (!currentEmployee.HasPermission(ScannedContractPermissions.View) || found.Count == 0)
        {
            return [];
        }

        var fileIds = found.Select(keyword => keyword.FileStorageFileid).Distinct().ToList();
        var rows = (await (
                    from job in db.OcrJobs.AsNoTracking()
                    join batch in ScopedBatches() on job.OcrBatchId equals batch.OcrPatchId
                    join file in db.FileStorages.AsNoTracking() on job.FileStorageFileid equals file.FileStorageFileid
                    where job.ContractDocumentId == null && fileIds.Contains(job.FileStorageFileid)
                    select new
                    {
                        job.OcrJobId,
                        file.FileStorageFileid,
                        file.FileStorageFilename,
                        file.FileStorageUploadeddate,
                        file.FileStorageUploadedbyemployeeid,
                    })
                .ToListAsync(cancellationToken))
            .GroupBy(row => row.FileStorageFileid)
            .Select(group => group.MaxBy(row => row.OcrJobId)!)
            .OrderByDescending(row => row.FileStorageUploadeddate)
            .Take(maxResults)
            .ToList();
        if (rows.Count == 0)
        {
            return [];
        }

        var shownFiles = rows.Select(row => (long?)row.FileStorageFileid).ToList();
        var contracts = (await db.Contracts.AsNoTracking()
                .Where(contract => contract.SourceType == ScannedContractCodes.SourceScanned && shownFiles.Contains(contract.FileStorageFileid))
                .Select(contract => new { contract.ContractId, contract.ContractCode, contract.ContractName, contract.PartnerId, contract.FileStorageFileid })
                .ToListAsync(cancellationToken))
            .GroupBy(contract => contract.FileStorageFileid!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var partnerIds = contracts.Values.Select(contract => contract.PartnerId).OfType<long>().Distinct().ToList();
        var customers = (await (
                    from customer in db.Customers.AsNoTracking()
                    join business in db.CustomerBusinesses on customer.CustomerId equals business.CustomerId into businesses
                    from business in businesses.DefaultIfEmpty()
                    where partnerIds.Contains(customer.CustomerId)
                    select new { customer, business })
                .ToListAsync(cancellationToken))
            .GroupBy(row => row.customer.CustomerId)
            .ToDictionary(group => group.Key, group => ToCustomerOption(group.First().customer, group.First().business).Name);
        var people = await PeopleAsync(rows.Select(row => row.FileStorageUploadedbyemployeeid), cancellationToken);
        var byFile = found.ToLookup(keyword => keyword.FileStorageFileid);

        return rows.Select(row =>
            {
                var contract = contracts.GetValueOrDefault(row.FileStorageFileid);
                return new ScannedSearchResultDto(
                    row.OcrJobId,
                    row.FileStorageFileid,
                    row.FileStorageFilename,
                    SignedEvidenceService.Utc(row.FileStorageUploadeddate),
                    Name(people, row.FileStorageUploadedbyemployeeid),
                    contract?.ContractId,
                    contract?.ContractCode,
                    contract?.ContractName,
                    contract?.PartnerId is { } partner ? customers.GetValueOrDefault(partner) : null,
                    byFile[row.FileStorageFileid]
                        .DistinctBy(keyword => keyword.ContractScanKeywordId)
                        .Select(SignedEvidenceService.ToMatchDto)
                        .ToList());
            })
            .ToList();
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Optionally, create a contract from the scan
    // ---------------------------------------------------------------------------------------------

    /// <summary>OCR reads a plain copy; an encrypted file is opened with the employee's key to make one again.</summary>
    public async Task<long> RetryAsync(long jobId, string? fileKey, CancellationToken cancellationToken)
    {
        var (job, file) = await GetJobRowAsync(jobId, cancellationToken);
        if (job.OcrJobProcessingStatus != OcrJobStatuses.Failed)
        {
            throw new InternalAppException(409, "Chỉ đọc lại được bản scan bị lỗi đọc nội dung.");
        }

        if (await staging.ReadAsync(file.FileStorageFileid, cancellationToken) is null)
        {
            await staging.SaveAsync(file.FileStorageFileid, await OpenAsync(file, fileKey, cancellationToken), cancellationToken);
        }

        await db.OcrJobs
            .Where(candidate => candidate.OcrJobId == jobId && candidate.OcrJobProcessingStatus == OcrJobStatuses.Failed)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(candidate => candidate.OcrJobProcessingStatus, OcrJobStatuses.Queued)
                    .SetProperty(candidate => candidate.ErrorMessage, (string?)null)
                    .SetProperty(candidate => candidate.CompletedAt, (DateTime?)null),
                cancellationToken);
        await db.OcrBatches
            .Where(batch => batch.OcrPatchId == job.OcrBatchId)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(batch => batch.BatchStatus, OcrBatchStatuses.Pending)
                    .SetProperty(batch => batch.UpdatedByEmployeeId, currentEmployee.EmployeeId)
                    .SetProperty(batch => batch.UpdatedAt, DateTime.UtcNow),
                cancellationToken);
        return jobId;
    }

    public async Task<long> ConfirmAsync(long jobId, ConfirmScannedContractInput input, CancellationToken cancellationToken)
    {
        var (job, file) = await GetJobRowAsync(jobId, cancellationToken);
        if (job.OcrJobProcessingStatus is OcrJobStatuses.Queued or OcrJobStatuses.Processing)
        {
            throw new InternalAppException(409, "Hệ thống đang đọc nội dung bản scan. Hãy chờ đọc xong rồi tạo hợp đồng.");
        }

        var code = input.ContractCode.Trim().ToUpperInvariant();
        if (!await db.Customers.AnyAsync(customer => customer.CustomerId == input.CustomerId, cancellationToken))
        {
            throw new InternalAppException(404, "Không tìm thấy khách hàng đã chọn.");
        }

        if (input.ContractTypeId is { } typeId
            && !await db.ContractTypes.AnyAsync(type => type.ContractTypeId == typeId, cancellationToken))
        {
            throw new InternalAppException(404, "Không tìm thấy loại hợp đồng đã chọn.");
        }

        var employeeId = currentEmployee.EmployeeId;
        var now = DateTime.UtcNow;
        return await db.ExecuteInTransactionAsync(async () =>
        {
            if (await db.Contracts.AnyAsync(contract => contract.FileStorageFileid == file.FileStorageFileid, cancellationToken))
            {
                throw new InternalAppException(409, "Bản scan này đã được tạo hợp đồng. Hãy tải lại trang.");
            }

            if (await db.Contracts.AnyAsync(contract => contract.ContractCode == code, cancellationToken))
            {
                throw new InternalAppException(409, $"Số hợp đồng {code} đã có trong hệ thống.");
            }

            var created = new Contract
            {
                ContractTypeId = input.ContractTypeId,
                ContractCode = code,
                ContractName = input.ContractName.Trim(),
                PartnerId = input.CustomerId,
                EmployeeId = employeeId,
                // Scanning an old paper contract only archives it: contract_status and approval_status stay
                // empty, and source_type tells these rows apart.
                SourceType = ScannedContractCodes.SourceScanned,
                SignedDate = input.SignedDate.ToDateTime(TimeOnly.MinValue),
                EffectiveDate = input.EffectiveDate?.ToDateTime(TimeOnly.MinValue),
                ExpirationDate = input.ExpirationDate?.ToDateTime(TimeOnly.MinValue),
                Currency = string.IsNullOrWhiteSpace(input.Currency) ? ScannedContractCodes.DefaultCurrency : input.Currency.Trim().ToUpperInvariant(),
                ContractValue = input.ContractValue,
                ContractDescription = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
                FileStorageFileid = file.FileStorageFileid,
                CreatedDate = now,
                CreatedEmployeeId = checked((int)employeeId),
            };
            db.Contracts.Add(created);
            await db.SaveChangesAsync(cancellationToken);

            await db.FileStorages
                .Where(candidate => candidate.FileStorageFileid == file.FileStorageFileid)
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(candidate => candidate.FileStorageObjecttype, ScannedContractCodes.FileObjectContract)
                        .SetProperty(candidate => candidate.FileStorageObjectid, created.ContractId),
                    cancellationToken);
            db.AuditLogs.Add(Audit(ContractCreatedAction, ContractEntity, created.ContractId, new
            {
                jobId,
                fileId = file.FileStorageFileid,
                sha256 = file.FileStorageSha256,
                code,
                customerId = input.CustomerId,
                signedDate = input.SignedDate,
                ocrStatus = job.OcrJobProcessingStatus,
            }));
            await db.SaveChangesAsync(cancellationToken);
            return created.ContractId;
        }, cancellationToken, System.Data.IsolationLevel.Serializable);
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------

    private const string LooseCollation = "Latin1_General_100_CI_AI";

    private IQueryable<OcrBatch> ScopedBatches()
    {
        var me = currentEmployee.EmployeeId;
        var seesAll = SeesAll;
        return db.OcrBatches.AsNoTracking().Where(batch => seesAll || batch.UploadedByEmployeeId == me);
    }

    private IQueryable<Contract> ScopedContracts()
    {
        var me = currentEmployee.EmployeeId;
        var seesAll = SeesAll;
        return db.Contracts.AsNoTracking()
            .Where(contract => contract.SourceType == ScannedContractCodes.SourceScanned)
            .Where(contract => seesAll || contract.EmployeeId == me || contract.CreatedEmployeeId == (int)me
                || db.FileStorages.Any(file => file.FileStorageFileid == contract.FileStorageFileid
                    && file.FileStorageUploadedbyemployeeid == me));
    }

    /// <summary>A job of a scanned contract (no CONTRACT_DOCUMENTS row) in a batch this employee may see.</summary>
    private async Task<(OcrJob Job, FileStorage File)> GetJobRowAsync(long jobId, CancellationToken cancellationToken)
    {
        var row = await (
                from job in db.OcrJobs.AsNoTracking()
                join batch in ScopedBatches() on job.OcrBatchId equals batch.OcrPatchId
                join file in db.FileStorages.AsNoTracking() on job.FileStorageFileid equals file.FileStorageFileid
                where job.OcrJobId == jobId && job.ContractDocumentId == null
                select new { job, file })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InternalAppException(404, "Không tìm thấy bản scan.");
        return (row.job, row.file);
    }

    private async Task<ScannedFileDto> ReadFileAsync(FileStorage file, string? fileKey, CancellationToken cancellationToken) =>
        new(file.FileStorageFilename, file.FileStorageContenttype, await OpenAsync(file, fileKey, cancellationToken));

    private async Task<byte[]> OpenAsync(FileStorage file, string? fileKey, CancellationToken cancellationToken)
    {
        var stored = await files.ReadAsync(file.FileStorageStoragekey, cancellationToken)
            ?? throw new InternalAppException(404, "Không tìm thấy tệp bản scan.");
        var contractOwner = await db.Contracts.AsNoTracking()
            .Where(contract => contract.FileStorageFileid == file.FileStorageFileid)
            .Select(contract => contract.EmployeeId)
            .FirstOrDefaultAsync(cancellationToken);
        return await protection.OpenAsync(file.FileStorageFileid, stored, fileKey,
            await FileReadersAsync(file.FileStorageUploadedbyemployeeid, contractOwner, cancellationToken), cancellationToken);
    }

    /// <summary>Who may open a scanned contract's file: who uploaded it, who created the contract, and every checker.</summary>
    private async Task<IReadOnlyCollection<long>> FileReadersAsync(long? uploaderId, long? contractOwnerId, CancellationToken cancellationToken)
    {
        var readers = new HashSet<long>(await protection.EmployeesWithPermissionAsync(ScannedContractPermissions.Confirm, cancellationToken));
        if (uploaderId is { } uploader)
        {
            readers.Add(uploader);
        }

        if (contractOwnerId is { } owner)
        {
            readers.Add(owner);
        }

        return readers;
    }

    private async Task<IReadOnlyList<ScannedBatchDto>> ToBatchDtosAsync(IReadOnlyList<OcrBatch> batches, CancellationToken cancellationToken)
    {
        var batchIds = batches.Select(batch => batch.OcrPatchId).ToList();
        var jobs = await (
                from job in db.OcrJobs.AsNoTracking()
                join file in db.FileStorages.AsNoTracking() on job.FileStorageFileid equals file.FileStorageFileid
                where batchIds.Contains(job.OcrBatchId)
                select new { job, file })
            .ToListAsync(cancellationToken);
        var fileIds = jobs.Select(row => (long?)row.file.FileStorageFileid).ToList();
        var contracts = (await db.Contracts.AsNoTracking()
                .Where(contract => fileIds.Contains(contract.FileStorageFileid))
                .Select(contract => new { contract.ContractId, contract.ContractCode, contract.FileStorageFileid })
                .ToListAsync(cancellationToken))
            .ToDictionary(contract => contract.FileStorageFileid!.Value);
        var people = await PeopleAsync(batches.Select(batch => (long?)batch.UploadedByEmployeeId), cancellationToken);

        return batches.Select(batch => new ScannedBatchDto(
                batch.OcrPatchId,
                batch.BatchStatus,
                batch.TotalFiles,
                SignedEvidenceService.Utc(batch.CreatedAt),
                Name(people, batch.UploadedByEmployeeId),
                jobs.Where(row => row.job.OcrBatchId == batch.OcrPatchId)
                    .OrderBy(row => row.job.OcrJobId)
                    .Select(row =>
                    {
                        var contract = contracts.GetValueOrDefault(row.file.FileStorageFileid);
                        return new ScannedJobSummaryDto(
                            row.job.OcrJobId,
                            row.file.FileStorageFileid,
                            row.file.FileStorageFilename,
                            row.file.FileStorageFilesize,
                            OcrJobStatuses.ToApiStatus(row.job.OcrJobProcessingStatus).ToString(),
                            row.job.OcrJobProcessingStatus,
                            contract?.ContractId,
                            contract?.ContractCode);
                    })
                    .ToList()))
            .ToList();
    }

    /// <summary>The first value OCR read of each kind, and the customers its tax code or phone number belongs to.</summary>
    private async Task<ScannedSuggestionDto> SuggestAsync(IReadOnlyList<ContractScanKeyword> keywords, CancellationToken cancellationToken)
    {
        string? First(ScanKeywordType type) => keywords
            .Where(keyword => keyword.ContractScanKeywordType == (int)type)
            .OrderBy(keyword => keyword.ContractScanKeywordSource)
            .ThenBy(keyword => keyword.ContractScanKeywordPagenumber)
            .Select(keyword => keyword.ContractScanKeywordValue)
            .FirstOrDefault();
        List<string> All(ScanKeywordType type) => keywords
            .Where(keyword => keyword.ContractScanKeywordType == (int)type)
            .Select(keyword => keyword.ContractScanKeywordValue)
            .Distinct()
            .ToList();

        var taxCodes = All(ScanKeywordType.TaxCode);
        var phones = All(ScanKeywordType.PhoneNumber);
        var matched = await (
                from customer in db.Customers.AsNoTracking()
                join business in db.CustomerBusinesses on customer.CustomerId equals business.CustomerId into businesses
                from business in businesses.DefaultIfEmpty()
                where (business != null && taxCodes.Contains(business.CustomerBusinessCompanyTaxIdentifier))
                    || (customer.CustomerPersonalTaxIdentifier != null && taxCodes.Contains(customer.CustomerPersonalTaxIdentifier))
                    || phones.Contains(customer.CustomerPersonalPhone)
                    || (business != null && business.CustomerBusinessPhone != null && phones.Contains(business.CustomerBusinessPhone))
                select new { customer, business })
            .Take(10)
            .ToListAsync(cancellationToken);

        return new ScannedSuggestionDto(
            First(ScanKeywordType.ContractCode),
            First(ScanKeywordType.TaxCode),
            First(ScanKeywordType.PhoneNumber),
            First(ScanKeywordType.CustomerName),
            matched.Select(row => ToCustomerOption(row.customer, row.business)).DistinctBy(option => option.CustomerId).ToList());
    }

    private async Task<ScannedContractDto> ToContractDtoAsync(Contract contract, CancellationToken cancellationToken)
    {
        var customer = contract.PartnerId is { } partnerId
            ? await (
                    from candidate in db.Customers.AsNoTracking()
                    join business in db.CustomerBusinesses on candidate.CustomerId equals business.CustomerId into businesses
                    from business in businesses.DefaultIfEmpty()
                    where candidate.CustomerId == partnerId
                    select new { candidate, business })
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var typeName = contract.ContractTypeId is { } typeId
            ? await db.ContractTypes.AsNoTracking().Where(type => type.ContractTypeId == typeId)
                .Select(type => type.ContractTypeName).FirstOrDefaultAsync(cancellationToken)
            : null;
        var file = contract.FileStorageFileid is { } fileId
            ? await db.FileStorages.AsNoTracking().Where(candidate => candidate.FileStorageFileid == fileId)
                .Select(candidate => candidate.FileStorageFilename).FirstOrDefaultAsync(cancellationToken)
            : null;
        var jobId = contract.FileStorageFileid is { } scanId
            ? await db.OcrJobs.AsNoTracking().Where(job => job.FileStorageFileid == scanId)
                .OrderByDescending(job => job.OcrJobId).Select(job => (long?)job.OcrJobId).FirstOrDefaultAsync(cancellationToken)
            : null;
        var people = await PeopleAsync([contract.EmployeeId], cancellationToken);

        return new ScannedContractDto(
            contract.ContractId,
            contract.ContractCode,
            contract.ContractName,
            contract.PartnerId,
            customer is null ? null : ToCustomerOption(customer.candidate, customer.business).Name,
            contract.ContractTypeId,
            typeName,
            contract.ContractStatus,
            contract.ApprovalStatus,
            Day(contract.SignedDate),
            Day(contract.EffectiveDate),
            Day(contract.ExpirationDate),
            contract.Currency,
            contract.ContractValue,
            contract.ContractDescription,
            contract.FileStorageFileid,
            file,
            jobId,
            Name(people, contract.EmployeeId),
            SignedEvidenceService.Utc(contract.CreatedDate));
    }

    private static string? Day(DateTime? value) => value is { } day ? DateOnly.FromDateTime(day).ToString("yyyy-MM-dd") : null;

    private static CustomerOptionDto ToCustomerOption(Customer customer, CustomerBusiness? business) => new(
        customer.CustomerId,
        business?.CustomerBusinessLegalName ?? customer.CustomerDisplayName,
        business?.CustomerBusinessCompanyTaxIdentifier ?? customer.CustomerPersonalTaxIdentifier,
        business?.CustomerBusinessPhone ?? customer.CustomerPersonalPhone);

    private async Task<Dictionary<long, string>> PeopleAsync(IEnumerable<long?> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.OfType<long>().Distinct().ToList();
        return await db.Employees.AsNoTracking()
            .Where(employee => wanted.Contains(employee.EmployeeId))
            .ToDictionaryAsync(employee => employee.EmployeeId, employee => employee.EmployeeDisplayName, cancellationToken);
    }

    private static string Name(IReadOnlyDictionary<long, string> people, long? id) =>
        id is { } key && people.TryGetValue(key, out var name) ? name : "—";

    private AuditLog Audit(string action, string entity, long entityId, object newData) => new()
    {
        ActionType = action,
        EntityName = entity,
        EntityId = entityId.ToString(),
        ActorType = "Employee",
        ActorId = currentEmployee.EmployeeId,
        NewData = JsonSerializer.Serialize(newData, Json),
        CreatedAt = DateTime.UtcNow,
    };
}

/// <summary>What the checker settles before the contract is created. Dates are the ones on the paper.</summary>
public sealed record ConfirmScannedContractInput(
    string ContractCode,
    string ContractName,
    long CustomerId,
    int? ContractTypeId,
    DateOnly SignedDate,
    DateOnly? EffectiveDate,
    DateOnly? ExpirationDate,
    double? ContractValue,
    string? Currency,
    string? Description);
