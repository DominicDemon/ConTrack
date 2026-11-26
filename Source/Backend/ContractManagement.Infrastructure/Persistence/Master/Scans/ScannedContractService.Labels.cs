using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

/// <summary>
/// Labels to sort scanned contracts (SCAN_LABEL, SCAN_LABEL_FILE, script 024), instead of folders on each PC:
/// shared by the whole company, several per scan, and the list filters by them.
/// Anyone who uploads scans creates labels and puts them on the scans they see; a checker
/// (contract.scanned.confirm) also renames and deletes labels, since that changes every scan carrying them.
/// </summary>
public sealed partial class ScannedContractService
{
    public const string LabelEntity = "SCAN_LABEL";
    public const string LabelCreatedAction = "ScanLabelCreated";
    public const string LabelUpdatedAction = "ScanLabelUpdated";
    public const string LabelDeletedAction = "ScanLabelDeleted";
    public const string JobLabelsSetAction = "ScannedJobLabelsSet";

    public const int MaxLabelName = 60;
    public const int MaxLabelsPerScan = 10;

    /// <summary>The colours SCAN_LABEL accepts (CK_ScanLabel_Color).</summary>
    public static readonly IReadOnlyList<string> LabelColors = ["slate", "blue", "green", "amber", "red", "violet", "teal", "pink"];

    /// <summary>Every label, by name; the count is of scans the employee may see.</summary>
    public async Task<IReadOnlyList<ScanLabelDto>> ListLabelsAsync(CancellationToken cancellationToken)
    {
        var visibleFiles = VisibleScanFiles();
        var labels = await db.ScanLabels.AsNoTracking()
            .OrderBy(label => label.ScanLabelName)
            .Select(label => new ScanLabelDto(
                label.ScanLabelId,
                label.ScanLabelName,
                label.ScanLabelColor,
                db.ScanLabelFiles.Count(link => link.ScanLabelId == label.ScanLabelId && visibleFiles.Contains(link.FileStorageFileid))))
            .ToListAsync(cancellationToken);
        return labels;
    }

    public async Task<ScanLabelDto> CreateLabelAsync(string? name, string? color, CancellationToken cancellationToken)
    {
        var (cleanName, cleanColor) = CheckLabel(name, color);
        await RefuseTakenNameAsync(cleanName, null, cancellationToken);
        var label = new ScanLabel
        {
            ScanLabelName = cleanName,
            ScanLabelColor = cleanColor,
            CreatedByEmployeeId = currentEmployee.EmployeeId,
            CreatedAt = DateTime.UtcNow,
        };
        db.ScanLabels.Add(label);
        await db.SaveChangesAsync(cancellationToken);
        db.AuditLogs.Add(Audit(LabelCreatedAction, LabelEntity, label.ScanLabelId, new { name = cleanName, color = cleanColor }));
        await db.SaveChangesAsync(cancellationToken);
        return new ScanLabelDto(label.ScanLabelId, label.ScanLabelName, label.ScanLabelColor, 0);
    }

    public async Task<ScanLabelDto> UpdateLabelAsync(long labelId, string? name, string? color, CancellationToken cancellationToken)
    {
        var label = await db.ScanLabels.SingleOrDefaultAsync(row => row.ScanLabelId == labelId, cancellationToken)
            ?? throw new InternalAppException(404, "Không tìm thấy nhãn.");
        var (cleanName, cleanColor) = CheckLabel(name, color);
        await RefuseTakenNameAsync(cleanName, labelId, cancellationToken);
        var old = new { name = label.ScanLabelName, color = label.ScanLabelColor };
        label.ScanLabelName = cleanName;
        label.ScanLabelColor = cleanColor;
        db.AuditLogs.Add(Audit(LabelUpdatedAction, LabelEntity, labelId, new { old, name = cleanName, color = cleanColor }));
        await db.SaveChangesAsync(cancellationToken);
        return (await ListLabelsAsync(cancellationToken)).Single(row => row.LabelId == labelId);
    }

    /// <summary>Takes the label off every scan, then removes it. The scans themselves stay.</summary>
    public async Task DeleteLabelAsync(long labelId, CancellationToken cancellationToken)
    {
        var label = await db.ScanLabels.AsNoTracking().SingleOrDefaultAsync(row => row.ScanLabelId == labelId, cancellationToken)
            ?? throw new InternalAppException(404, "Không tìm thấy nhãn.");
        await db.ExecuteInTransactionAsync(async () =>
        {
            var removed = await db.ScanLabelFiles.Where(link => link.ScanLabelId == labelId).ExecuteDeleteAsync(cancellationToken);
            await db.ScanLabels.Where(row => row.ScanLabelId == labelId).ExecuteDeleteAsync(cancellationToken);
            db.AuditLogs.Add(Audit(LabelDeletedAction, LabelEntity, labelId, new { name = label.ScanLabelName, scans = removed }));
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    /// <summary>Puts exactly these labels on the scan: missing ones are added, the others taken off.</summary>
    public async Task<IReadOnlyList<ScanLabelTagDto>> SetJobLabelsAsync(long jobId, IReadOnlyCollection<long>? labelIds, CancellationToken cancellationToken)
    {
        var (_, file) = await GetJobRowAsync(jobId, cancellationToken);
        var fileId = file.FileStorageFileid;
        var wanted = (labelIds ?? []).Distinct().ToList();
        if (wanted.Count > MaxLabelsPerScan)
        {
            throw new ArgumentException($"Mỗi bản scan gắn tối đa {MaxLabelsPerScan} nhãn.");
        }

        var known = await db.ScanLabels.AsNoTracking().Where(label => wanted.Contains(label.ScanLabelId))
            .Select(label => label.ScanLabelId).ToListAsync(cancellationToken);
        if (known.Count != wanted.Count)
        {
            throw new InternalAppException(404, "Có nhãn không còn tồn tại. Hãy tải lại trang.");
        }

        var current = await db.ScanLabelFiles.AsNoTracking().Where(link => link.FileStorageFileid == fileId)
            .Select(link => link.ScanLabelId).ToListAsync(cancellationToken);
        var added = wanted.Except(current).ToList();
        var removed = current.Except(wanted).ToList();
        if (added.Count > 0 || removed.Count > 0)
        {
            await db.ExecuteInTransactionAsync(async () =>
            {
                await db.ScanLabelFiles.Where(link => link.FileStorageFileid == fileId && removed.Contains(link.ScanLabelId))
                    .ExecuteDeleteAsync(cancellationToken);
                var now = DateTime.UtcNow;
                db.ScanLabelFiles.AddRange(added.Select(labelId => new ScanLabelFile
                {
                    ScanLabelId = labelId,
                    FileStorageFileid = fileId,
                    AssignedByEmployeeId = currentEmployee.EmployeeId,
                    AssignedAt = now,
                }));
                db.AuditLogs.Add(Audit(JobLabelsSetAction, JobEntity, jobId, new { fileId, added, removed }));
                await db.SaveChangesAsync(cancellationToken);
                return true;
            }, cancellationToken);
        }

        return (await LabelsOfAsync([fileId], cancellationToken))[fileId].ToList();
    }

    /// <summary>The labels on each file, by name.</summary>
    private async Task<ILookup<long, ScanLabelTagDto>> LabelsOfAsync(IReadOnlyCollection<long> fileIds, CancellationToken cancellationToken) =>
        (await (
                from link in db.ScanLabelFiles.AsNoTracking()
                join label in db.ScanLabels.AsNoTracking() on link.ScanLabelId equals label.ScanLabelId
                where fileIds.Contains(link.FileStorageFileid)
                orderby label.ScanLabelName
                select new { link.FileStorageFileid, Tag = new ScanLabelTagDto(label.ScanLabelId, label.ScanLabelName, label.ScanLabelColor) })
            .ToListAsync(cancellationToken))
        .ToLookup(row => row.FileStorageFileid, row => row.Tag);

    /// <summary>Files of the scanned contracts the employee may see (the same scope as the list).</summary>
    private IQueryable<long> VisibleScanFiles() =>
        from job in db.OcrJobs.AsNoTracking()
        join batch in ScopedBatches() on job.OcrBatchId equals batch.OcrPatchId
        where job.ContractDocumentId == null
        select job.FileStorageFileid;

    private static (string Name, string Color) CheckLabel(string? name, string? color)
    {
        var cleanName = string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (cleanName.Length == 0)
        {
            throw new ArgumentException("Hãy nhập tên nhãn.");
        }

        if (cleanName.Length > MaxLabelName)
        {
            throw new ArgumentException($"Tên nhãn tối đa {MaxLabelName} ký tự.");
        }

        var cleanColor = string.IsNullOrWhiteSpace(color) ? "slate" : color.Trim().ToLowerInvariant();
        if (!LabelColors.Contains(cleanColor))
        {
            throw new ArgumentException("Màu nhãn không hợp lệ.");
        }

        return (cleanName, cleanColor);
    }

    /// <summary>Names are compared without case or accents, like the database (Latin1_General_100_CI_AI).</summary>
    private async Task RefuseTakenNameAsync(string name, long? exceptId, CancellationToken cancellationToken)
    {
        var taken = await db.ScanLabels.AsNoTracking()
            .Where(label => label.ScanLabelName == name && label.ScanLabelId != exceptId)
            .Select(label => label.ScanLabelName)
            .FirstOrDefaultAsync(cancellationToken);
        if (taken is not null)
        {
            throw new InternalAppException(409, $"Đã có nhãn \"{taken}\". Hãy dùng nhãn đó hoặc đặt tên khác.");
        }
    }
}
