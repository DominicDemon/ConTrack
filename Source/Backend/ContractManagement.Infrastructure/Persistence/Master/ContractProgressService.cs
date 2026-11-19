using System.Text.Json;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Internal;
using ContractManagement.Domain.Entities.Master;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

/// <param name="From">First day (Vietnam time) the contract was created, inclusive.</param>
/// <param name="To">Last day (Vietnam time) the contract was created, inclusive.</param>
public sealed record ProgressFilter(string? Search = null, long? OwnerId = null, string? Stage = null, int? WaitingDays = null, bool Mine = false, bool Warnings = false, int Page = 1,
    DateOnly? From = null, DateOnly? To = null);
public sealed record ProgressRow(long Id, string Code, string Name, string Customer, long OwnerId, string Owner, string Stage, string Department, DateTime? EnteredAt, string? LastActor, int WarningCount, bool Configured, bool NeedsMe, string? InspectionError = null);
public sealed record ProgressPage(List<ProgressRow> Items, int Total, int Page, int PageSize, Dictionary<string, int> Counts, DateTime ServerTime);

public sealed class ContractProgressService(MasterDbContext db, ContractChecklistService checklist)
{
    public async Task<ProgressPage> List(ProgressFilter filter, InternalUser actor, CancellationToken ct)
    {
        InternalDraftService.RequireAccess(actor, actor.Id);
        if (filter.Page < 1 || filter.Page > 1000000 || filter.WaitingDays < 0 || filter.WaitingDays > 36500) throw new ArgumentException("Bộ lọc không hợp lệ.");
        if (filter.From > filter.To) throw new ArgumentException("“Từ ngày” phải trước hoặc bằng “Đến ngày”.");
        var query = from doc in db.ContractDocuments.AsNoTracking()
                    join version in db.ContractVersions on doc.ContractDocumentCurrentVersionId equals version.ContractVersionId
                    join employee in db.Employees on doc.EmployeeId equals employee.EmployeeId
                    join customer in db.Customers on doc.CustomerId equals customer.CustomerId
                    where doc.TenantId == null && (actor.CanManageAll || doc.EmployeeId == actor.Id)
                    && db.ContractDynamicValues.Any(v => v.ContractDocumentId == doc.ContractDocumentId && v.ContractVersionId == doc.ContractDocumentCurrentVersionId && v.FieldKey == InternalRepository.SnapshotKey)
                    select new { doc, version, employee.EmployeeDisplayName, customer.CustomerDisplayName };
        if (filter.OwnerId.HasValue) query = query.Where(x => x.doc.EmployeeId == filter.OwnerId);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            // Also the customer's legal entities: their names and tax codes.
            query = query.Where(x => x.doc.ContractDocumentName.Contains(search) || x.doc.ContractDocumentCode!.Contains(search) || x.CustomerDisplayName.Contains(search)
                || db.CustomerBusinesses.Any(b => b.CustomerId == x.doc.CustomerId && (b.CustomerBusinessCompanyTaxIdentifier.Contains(search) || b.CustomerBusinessLegalName.Contains(search))));
        }
        // Dates are stored in UTC; the filter is in Vietnam days (UTC+7).
        if (filter.From is { } from) { var start = from.ToDateTime(TimeOnly.MinValue).AddHours(-7); query = query.Where(x => x.doc.CreatedDate >= start); }
        if (filter.To is { } to) { var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7); query = query.Where(x => x.doc.CreatedDate < end); }
        var source = await query.ToListAsync(ct);
        var ids = source.Select(x => x.doc.ContractDocumentId).ToArray();
        var versions = source.Select(x => x.version.ContractVersionId).ToArray();
        // Batch scoped snapshots and configuration instead of querying three times per list row.
        var snapshots = await db.ContractDynamicValues.AsNoTracking().Where(x => versions.Contains(x.ContractVersionId) && x.FieldKey == InternalRepository.SnapshotKey)
            .ToDictionaryAsync(x => x.ContractVersionId, x => x.FieldValue, ct);
        var checklists = await db.Set<ContractChecklist>().AsNoTracking().Where(x => x.VersionId.HasValue && versions.Contains(x.VersionId.Value))
            .ToDictionaryAsync(x => x.VersionId!.Value, ct);
        var approvals = await db.ContractApprovalRequests.AsNoTracking().Where(x => ids.Contains(x.ContractDocumentId) && x.InternalStage != null).ToListAsync(ct);
        var reviews = await db.ContractReviewRequests.AsNoTracking().Where(x => versions.Contains(x.ContractVersionId)).ToListAsync(ct);
        var stringIds = ids.Select(x => x.ToString()).ToArray();
        var events = await (from audit in db.AuditLogs.AsNoTracking()
                            join employee in db.Employees on audit.ActorId equals (long?)employee.EmployeeId
                            where audit.EntityName == "CONTRACT_INTERNAL_APPROVAL" && stringIds.Contains(audit.EntityId!)
                            && (audit.ActionType == "SUBMIT" || audit.ActionType == "APPROVE" || audit.ActionType == "RETURN")
                            select new { audit, employee.EmployeeDisplayName }).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var rows = new List<ProgressRow>();
        foreach (var item in source)
        {
            var doc = item.doc; var version = item.version;
            var approval = approvals.Where(x => x.ContractDocumentId == doc.ContractDocumentId && x.ContractVersionId == version.ContractVersionId).MaxBy(x => x.ContractApprovalRequestId);
            var review = reviews.Where(x => x.ContractVersionId == version.ContractVersionId).MaxBy(x => x.ContractReviewRequestId);
            // The former Sếp turn before sending no longer exists: a request left there counts as approved.
            var stage = approval?.InternalStage is "DIRECTOR" ? "APPROVED" : approval?.InternalStage ?? "DRAFT";
            var transition = events.Where(x => x.audit.EntityId == doc.ContractDocumentId.ToString()
                && x.audit.NewData != null && JsonDocument.Parse(x.audit.NewData).RootElement.TryGetProperty("version", out var number) && number.GetInt64() == version.ContractVersionVersionNo).MaxBy(x => x.audit.AuditLogId);
            DateTime? entered = transition?.audit.CreatedAt;
            string? lastActor = transition?.EmployeeDisplayName;
            if (stage == "DRAFT") entered = version.ContractVersionVersionNo > 1 || doc.ContractDocumentCode?.StartsWith("HD-") == true ? version.ContractVersionCreatedDate : null;
            if (review is not null && version.ContractVersionIslocked)
            {
                stage = review.ContractReviewRequestStatus switch { "SENT" or "VIEWED" => "CUSTOMER", "CHANGES_REQUESTED" or "REJECTED" => "CHANGES", "APPROVED" => doc.ContractDocumentStatus == ContractDocumentStatus.PendingApproval
                    ? "FINAL_CHECK" : "SIGNATURE", _ => stage };
                entered = stage == "CUSTOMER" ? review.SentAt : review.RespondedAt;
            }
            if (doc.ContractDocumentStatus is 4 or 5 or 6) { stage = "CLOSED"; entered = null; }
            var department = stage switch { "OFFICE" or "FINAL_CHECK" => "Hành chính", "CUSTOMER" => "Khách hàng", "SIGNATURE" => "Sếp ký, rồi khách ký", "CLOSED" => "Đã đóng", _ => "Sale" };
            var mine = stage switch { "OFFICE" or "FINAL_CHECK" => actor.CanCheck, "SIGNATURE" => actor.CanApprove, "DRAFT" or "RETURNED" or "APPROVED" or "CHANGES" => actor.CanWrite && doc.EmployeeId == actor.Id, _ => false };
            if (filter.Mine && !mine || filter.WaitingDays.HasValue && (entered is null || now - entered < TimeSpan.FromDays(filter.WaitingDays.Value))) continue;
            var config = checklists.GetValueOrDefault(version.ContractVersionId);
            var configured = config?.Configured ?? false;
            var warnings = 1;
            string? inspectionError = null;
            try
            {
                var findings = await checklist.InspectSnapshot(snapshots[version.ContractVersionId], config, ct);
                warnings = findings.Configured ? findings.Findings.Count(x => !x.Found && x.Resolution is null) : 1;
            }
            catch (Exception error) when (error is IOException or ArgumentException or JsonException)
            {
                inspectionError = "Chưa kiểm tra được tài liệu đã lưu. Mở hợp đồng để kiểm tra tệp nguồn.";
            }
            if (filter.Warnings && warnings == 0) continue;
            rows.Add(new(doc.ContractDocumentId, doc.ContractDocumentCode ?? "", doc.ContractDocumentName, item.CustomerDisplayName, doc.EmployeeId, item.EmployeeDisplayName, stage, department, entered, lastActor, warnings, configured, mine, inspectionError));
        }
        var counts = rows.GroupBy(x => x.Stage).ToDictionary(x => x.Key, x => x.Count());
        if (!string.IsNullOrEmpty(filter.Stage)) rows = rows.Where(x => x.Stage == filter.Stage).ToList();
        return new(rows.OrderBy(x => x.EnteredAt == null).ThenBy(x => x.EnteredAt).ThenBy(x => x.Id).Skip((filter.Page - 1) * 25).Take(25).ToList(), rows.Count, filter.Page, 25, counts, now);
    }
}
