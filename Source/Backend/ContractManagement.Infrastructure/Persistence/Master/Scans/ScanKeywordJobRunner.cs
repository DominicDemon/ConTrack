using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal.Scans;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

/// <summary>
/// Runs one job of the OCR queue in <c>OCR_JOB</c> (MasterDB-Optimize_v1): claims a QUEUED job, reads the
/// stored scan, keeps the text in <c>ocr_text</c> and replaces the keywords OCR read before. Keywords added
/// by hand stay. OCR_JOB has no attempt counter, so an engine failure is tried again within the same run,
/// up to <see cref="MaxAttempts"/> times, before the job is marked FAILED for someone to retry.
/// </summary>
public sealed class ScanKeywordJobRunner(ScanMasterDbContext db, IFileStore files, IScanKeywordReader reader, IOcrStagingStore staging)
{
    public const int MaxAttempts = 3;

    private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// A run that stopped half way (the API was shut down) leaves its job PROCESSING. OCR_JOB has no lease,
    /// so the worker calls this once at start: one API instance runs the queue.
    /// </summary>
    public Task<int> RequeueInterruptedAsync(CancellationToken cancellationToken) =>
        db.OcrJobs
            .Where(job => job.OcrJobProcessingStatus == OcrJobStatuses.Processing)
            .ExecuteUpdateAsync(setters => setters.SetProperty(job => job.OcrJobProcessingStatus, OcrJobStatuses.Queued),
                cancellationToken);

    /// <summary>Runs the oldest queued job. False when none is queued.</summary>
    public async Task<bool> RunNextAsync(CancellationToken cancellationToken)
    {
        var job = await db.OcrJobs.AsNoTracking()
            .Where(candidate => candidate.OcrJobProcessingStatus == OcrJobStatuses.Queued)
            .OrderBy(candidate => candidate.ScannedAt)
            .ThenBy(candidate => candidate.OcrJobId)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null)
        {
            return false;
        }

        // Claim it only if it is still queued.
        var claimed = await db.OcrJobs
            .Where(candidate => candidate.OcrJobId == job.OcrJobId && candidate.OcrJobProcessingStatus == OcrJobStatuses.Queued)
            .ExecuteUpdateAsync(setters => setters.SetProperty(candidate => candidate.OcrJobProcessingStatus, OcrJobStatuses.Processing),
                cancellationToken);
        if (claimed == 0)
        {
            return true;
        }

        await UpdateBatchAsync(job.OcrBatchId, CancellationToken.None);
        try
        {
            // The plain copy made at upload (or on "read again"); a file stored before encryption is read as it is.
            var content = await staging.ReadAsync(job.FileStorageFileid, cancellationToken);
            if (content is null)
            {
                var storageKey = await db.FileStorages.AsNoTracking()
                    .Where(file => file.FileStorageFileid == job.FileStorageFileid)
                    .Select(file => file.FileStorageStoragekey)
                    .SingleOrDefaultAsync(cancellationToken);
                content = storageKey is null ? null : await files.ReadAsync(storageKey, cancellationToken);
            }

            if (content is null)
            {
                await FailAsync(job, "ScanFileMissing");
                return true;
            }

            if (FileCrypto.IsEncrypted(content))
            {
                // Only an employee's key opens it: "read again" asks for it and makes a new plain copy.
                await FailAsync(job, "ScanFileLocked");
                return true;
            }

            var result = await ReadWithRetriesAsync(content, cancellationToken);
            await SaveAsync(job, result, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down: the job stays PROCESSING and is queued again when the API starts.
            throw;
        }
        catch (ContractScanOcrException exception)
        {
            await FailAsync(job, exception.FailureCode);
        }
        catch (Exception)
        {
            await FailAsync(job, ContractScanOcrErrorCodes.EngineFailed);
        }

        return true;
    }

    private async Task<ScanKeywordResult> ReadWithRetriesAsync(byte[] content, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await reader.ReadAsync(content, cancellationToken);
            }
            catch (ContractScanOcrException exception) when (exception.IsClientError)
            {
                // The file itself cannot be read: trying again gives the same answer.
                throw;
            }
            catch (Exception) when (attempt < MaxAttempts && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(FirstRetryDelay * Math.Pow(3, attempt - 1), cancellationToken);
            }
        }
    }

    private async Task SaveAsync(OcrJob job, ScanKeywordResult result, CancellationToken cancellationToken)
    {
        var fileId = job.FileStorageFileid;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.ContractScanKeywords
            .Where(keyword => keyword.FileStorageFileid == fileId
                && keyword.ContractScanKeywordSource == (int)ScanKeywordSource.Ocr)
            .ExecuteDeleteAsync(cancellationToken);
        // A keyword already added by hand is kept as it is (one row per type and value; the column ignores case).
        var manual = (await db.ContractScanKeywords.AsNoTracking()
                .Where(keyword => keyword.FileStorageFileid == fileId)
                .Select(keyword => new { keyword.ContractScanKeywordType, keyword.ContractScanKeywordValue })
                .ToListAsync(cancellationToken))
            .Select(keyword => (keyword.ContractScanKeywordType, keyword.ContractScanKeywordValue.ToUpperInvariant()))
            .ToHashSet();

        var now = DateTime.UtcNow;
        db.ContractScanKeywords.AddRange(result.Keywords
            .Where(keyword => !manual.Contains(((int)keyword.Type, keyword.Value.ToUpperInvariant())))
            .Select(keyword => new ContractScanKeyword
            {
                FileStorageFileid = fileId,
                ContractScanKeywordType = (int)keyword.Type,
                ContractScanKeywordValue = keyword.Value,
                ContractScanKeywordPagenumber = keyword.Page,
                ContractScanKeywordSource = (int)ScanKeywordSource.Ocr,
                CreatedAt = now,
            }));
        await db.SaveChangesAsync(cancellationToken);

        // Every kind of keyword found: SUCCESS. Something missing: the checker has to look for it on the paper.
        var foundAll = Enum.GetValues<ScanKeywordType>().All(type => result.Keywords.Any(keyword => keyword.Type == type));
        var status = foundAll ? OcrJobStatuses.Success : OcrJobStatuses.NeedReview;
        await db.OcrJobs
            .Where(candidate => candidate.OcrJobId == job.OcrJobId)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(candidate => candidate.OcrJobProcessingStatus, status)
                    .SetProperty(candidate => candidate.OcrText, result.Text)
                    .SetProperty(candidate => candidate.PageCount, result.PageCount)
                    .SetProperty(candidate => candidate.ErrorMessage, (string?)null)
                    .SetProperty(candidate => candidate.CompletedAt, now),
                cancellationToken);
        await UpdateBatchAsync(job.OcrBatchId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        // Read: the plain copy is no longer needed.
        await staging.DeleteAsync(fileId, CancellationToken.None);
    }

    private async Task FailAsync(OcrJob job, string errorCode)
    {
        await staging.DeleteAsync(job.FileStorageFileid, CancellationToken.None);
        await db.OcrJobs
            .Where(candidate => candidate.OcrJobId == job.OcrJobId)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(candidate => candidate.OcrJobProcessingStatus, OcrJobStatuses.Failed)
                    .SetProperty(candidate => candidate.ErrorMessage, errorCode)
                    .SetProperty(candidate => candidate.CompletedAt, DateTime.UtcNow),
                CancellationToken.None);
        await UpdateBatchAsync(job.OcrBatchId, CancellationToken.None);
    }

    /// <summary>
    /// The batch follows all its jobs: PROCESSING while one is still queued or being read, then COMPLETED,
    /// or FAILED when any file could not be read.
    /// </summary>
    private async Task UpdateBatchAsync(long batchId, CancellationToken cancellationToken)
    {
        var statuses = await db.OcrJobs.AsNoTracking()
            .Where(job => job.OcrBatchId == batchId)
            .Select(job => job.OcrJobProcessingStatus)
            .ToListAsync(cancellationToken);
        var status = statuses.Any(value => value is OcrJobStatuses.Queued or OcrJobStatuses.Processing)
            ? OcrBatchStatuses.Processing
            : statuses.Contains(OcrJobStatuses.Failed) ? OcrBatchStatuses.Failed : OcrBatchStatuses.Completed;
        await db.OcrBatches
            .Where(batch => batch.OcrPatchId == batchId)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(batch => batch.BatchStatus, status)
                    .SetProperty(batch => batch.UpdatedAt, DateTime.UtcNow),
                cancellationToken);
    }
}
