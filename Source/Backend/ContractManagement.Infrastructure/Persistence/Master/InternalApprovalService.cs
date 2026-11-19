using System.Text.Json;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed class InternalApprovalService(MasterDbContext db, InternalWordTemplates word)
{
    public const string Workflow = "CONTRACT_PRE_SEND";
    private const string Entity = "CONTRACT_INTERNAL_APPROVAL";

    private async Task<ContractDocument> Document(long id, InternalUser actor, CancellationToken ct)
    {
        var doc = await db.ContractDocuments.SingleOrDefaultAsync(d => d.ContractDocumentId == id && d.TenantId == null, ct)
            ?? throw new InternalAppException(404, "Không tìm thấy hợp đồng.");
        InternalDraftService.RequireAccess(actor, doc.EmployeeId);
        return doc;
    }

    private Task<ContractApprovalRequest?> Latest(long id, long? version, CancellationToken ct) => db.ContractApprovalRequests
        .Where(a => a.ContractDocumentId == id && a.ContractVersionId == version && a.InternalStage != null)
        .OrderByDescending(a => a.ContractApprovalRequestId).FirstOrDefaultAsync(ct);

    private async Task<string> ContentHash(long id, long version, CancellationToken ct)
    {
        var snapshot = await db.ContractDynamicValues.SingleAsync(x => x.ContractDocumentId == id && x.ContractVersionId == version && x.FieldKey == InternalRepository.SnapshotKey, ct);
        var content = JsonSerializer.Deserialize<DraftContent>(snapshot.FieldValue, InternalRepository.Json)!;
        if (content.TemplateSnapshot?.WordStorageKey is not null) await word.ReadWord(content.TemplateSnapshot, ct);
        return SnapshotHash.Compute(snapshot.FieldValue);
    }

    public async Task<InternalApprovalView> View(long id, InternalUser actor, CancellationToken ct)
    {
        var doc = await Document(id, actor, ct);
        var version = await db.ContractVersions.SingleAsync(v => v.ContractVersionId == doc.ContractDocumentCurrentVersionId, ct);
        var approval = await Latest(id, version.ContractVersionId, ct);
        var history = await (from a in db.AuditLogs.AsNoTracking() join e in db.Employees on a.ActorId equals (long?)e.EmployeeId
            where a.EntityName == Entity && a.EntityId == id.ToString() orderby a.AuditLogId
            select new ApprovalEvent(a.AuditLogId, a.ActionType, e.EmployeeDisplayName, a.CreatedAt, a.NewData)).ToListAsync(ct);
        // Sếp no longer approves before sending (only signs): a request left at the former DIRECTOR turn counts as approved.
        var stage = approval?.InternalStage is "DIRECTOR" ? "APPROVED" : approval?.InternalStage ?? "DRAFT";
        var reviewStatus = await db.ContractReviewRequests.Where(r => r.ContractDocumentId == id && r.ContractVersionId == version.ContractVersionId)
            .OrderByDescending(r => r.ContractReviewRequestId).Select(r => r.ContractReviewRequestStatus).FirstOrDefaultAsync(ct);
        if (reviewStatus is not null && version.ContractVersionIslocked && stage == "APPROVED") stage = "CUSTOMER_" + reviewStatus;
        return new(stage, approval is null ? null : Convert.ToBase64String(approval.ContractApprovalRequestRowversion),
            actor.CanWrite && !version.ContractVersionIslocked && stage is "DRAFT" or "RETURNED",
            stage == "OFFICE" && actor.CanCheck, history);
    }

    public async Task Act(long id, string action, ApprovalAction input, InternalUser actor, CancellationToken ct)
    {
        await ReviewData.InTransaction(db, async () =>
        {
            var doc = await Document(id, actor, ct);
            var version = await db.ContractVersions.SingleAsync(v => v.ContractVersionId == doc.ContractDocumentCurrentVersionId, ct);
            ReviewData.Expect(doc.RowVersion, input.RowVersion);
            ReviewData.Expect(version.ContractVersionRowversion, input.VersionRowVersion);
            var approval = await Latest(id, version.ContractVersionId, ct);
            var now = DateTime.UtcNow;
            var note = InternalDraftPolicy.Text(input.Note, 1000);
            if (action == "submit")
            {
                if (!actor.CanWrite) throw new InternalAppException(403, "Chỉ kinh doanh được trình duyệt.");
                if (!ContractReviewPolicy.CanEdit(doc.ContractDocumentStatus, version.ContractVersionIslocked)) throw new InternalAppException(409, "Phiên bản đã khóa.");
                var snapshot = await db.ContractDynamicValues.SingleAsync(x => x.ContractDocumentId == id && x.ContractVersionId == version.ContractVersionId && x.FieldKey == InternalRepository.SnapshotKey, ct);
                var content = JsonSerializer.Deserialize<DraftContent>(snapshot.FieldValue, InternalRepository.Json)!;
                DraftValidation.Prepare(content, content.TemplateSnapshot!);
                if (content.MissingFields.Count > 0) throw new DraftValidationException(DraftValidation.Missing(content));
                var workflow = await db.ApprovalWorkflows.SingleAsync(w => w.ApprovalWorkflowCode == Workflow && w.ApprovalWorkflowIsactive, ct);
                approval = new() { ContractDocumentId = id, ContractVersionId = version.ContractVersionId, ApprovalWorkflowId = workflow.ApprovalWorkflowId,
                    SubmittedByEmployeeId = actor.Id, SubmittedAt = now, InternalStage = "OFFICE", InternalContentHash = await ContentHash(id, version.ContractVersionId, ct) };
                db.ContractApprovalRequests.Add(approval);
                version.ContractVersionIslocked = true;
                doc.ContractDocumentStatus = ContractDocumentStatus.PendingApproval;
            }
            else
            {
                if (approval is null) throw new InternalAppException(409, "Hợp đồng chưa trình duyệt.");
                ReviewData.Expect(approval.ContractApprovalRequestRowversion, input.ApprovalRowVersion);
                // Only Hành chính decides before the contract goes to the customer; Sếp signs later and does not approve.
                if (!(approval.InternalStage == "OFFICE" && actor.CanCheck))
                    throw new InternalAppException(403, "Chỉ Hành chính duyệt hợp đồng trước khi gửi khách.");
                if (approval.InternalContentHash != await ContentHash(id, version.ContractVersionId, ct)) throw new InternalAppException(409, "Nội dung đã thay đổi, cần trình lại.");
                if (action == "return")
                {
                    if (note.Length == 0) throw new ArgumentException("Nhập lý do trả lại.");
                    approval.InternalStage = "RETURNED";
                    approval.ContractApprovalRequestStatus = 2;
                    version.ContractVersionIslocked = false;
                    doc.ContractDocumentStatus = ContractDocumentStatus.Draft;
                }
                else if (action == "approve")
                {
                    approval.InternalStage = "APPROVED";
                    approval.ContractApprovalRequestStatus = 1;
                }
                else throw new ArgumentException("Thao tác không hợp lệ.");
                approval.ContractApprovalRequestDecisioncomment = note;
                if (approval.InternalStage is "RETURNED" or "APPROVED") { approval.ResolvedByEmployeeId = actor.Id; approval.ContractApprovalRequestResolvedAt = now; }
            }
            doc.UpdatedDate = now;
            version.ContractVersionChangenote = "Duyệt nội bộ " + now.ToString("O");
            Audit(id, actor, action.ToUpperInvariant(), new { version = version.ContractVersionVersionNo, stage = approval.InternalStage, note }, now);
            await db.SaveChangesAsync(ct);
            // Sale submits → Hành chính is told; Hành chính approves or returns → the Sale in charge is told.
            var name = $"{doc.ContractDocumentCode} · {doc.ContractDocumentName}" + (version.ContractVersionVersionNo > 1 ? $" (phiên bản {version.ContractVersionVersionNo})" : "");
            var key = $"pre-send:{approval.ContractApprovalRequestId}:{action}:{now.Ticks}";
            if (action == "submit")
                await ReviewData.Notify(db, doc, key, "SUBMITTED_TO_OFFICE", "Hợp đồng chờ Hành chính duyệt", $"{actor.Name} trình {name}" + (note.Length > 0 ? $". Ghi chú: {note}" : ""),
                    await ReviewData.WithPermission(db, "contract.check", ct), now, ct, includeOwner: false, except: actor.Id);
            else if (action == "approve")
                await ReviewData.Notify(db, doc, key, "OFFICE_APPROVED", "Hành chính đã duyệt, có thể gửi khách", name + (note.Length > 0 ? $". Ghi chú: {note}" : ""), [], now, ct, except: actor.Id);
            else if (action == "return")
                await ReviewData.Notify(db, doc, key, "OFFICE_RETURNED", "Hành chính trả lại hợp đồng", $"{name}. Lý do: {note}", [], now, ct, except: actor.Id);
            return true;
        }, ct);
    }

    public async Task RequireApproved(long id, long version, CancellationToken ct)
    {
        var approval = await Latest(id, version, ct);
        if (approval?.InternalStage is not ("APPROVED" or "DIRECTOR") || approval.InternalContentHash != await ContentHash(id, version, ct))
            throw new InternalAppException(409, "Phiên bản cần được Hành chính duyệt trước khi gửi khách.");
    }

    public async Task Comment(long id, InternalCommentInput input, InternalUser actor, CancellationToken ct)
    {
        await ReviewData.InTransaction(db, async () =>
        {
            var doc = await Document(id, actor, ct);
            var version = await db.ContractVersions.SingleAsync(v => v.ContractVersionId == doc.ContractDocumentCurrentVersionId, ct);
            ReviewData.Expect(doc.RowVersion, input.RowVersion);
            ReviewData.Expect(version.ContractVersionRowversion, input.VersionRowVersion);
            var now = DateTime.UtcNow;
            if (input.ResolveId is not null)
            {
                if (!actor.CanCheck) throw new InternalAppException(403, "Chỉ Hành chính được xử lý nhận xét.");
                if (!await db.AuditLogs.AnyAsync(a => a.AuditLogId == input.ResolveId && a.EntityName == Entity && a.EntityId == id.ToString() && a.ActionType == "COMMENT", ct))
                    throw new InternalAppException(404, "Không tìm thấy nhận xét.");
                Audit(id, actor, "RESOLVE", new { version = version.ContractVersionVersionNo, termKey = (string?)null, note = "", resolveId = input.ResolveId }, now);
            }
            else
            {
                // Several comments from one form are saved together, so one row version check covers them all.
                var items = input.Items is { Count: > 0 } ? input.Items : [new InternalCommentItem(input.Content, input.TermKey)];
                if (items.Count > 20) throw new ArgumentException("Gửi tối đa 20 nhận xét một lần.");
                foreach (var item in items)
                {
                    if (string.IsNullOrWhiteSpace(item.Content)) throw new ArgumentException("Nhập nội dung cho từng nhận xét.");
                    if (item.TermKey is not null && !await db.ContractTerms.AnyAsync(t => t.ContractDocumentId == id && t.ContractVersionId == version.ContractVersionId && t.ContractTermTermcode == item.TermKey, ct))
                        throw new ArgumentException("Điều khoản không thuộc phiên bản hiện tại.");
                    Audit(id, actor, "COMMENT", new { version = version.ContractVersionVersionNo, termKey = item.TermKey, note = InternalDraftPolicy.Text(item.Content, 4000), resolveId = (long?)null }, now);
                }
            }
            doc.UpdatedDate = now;
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    private void Audit(long id, InternalUser actor, string action, object data, DateTime now) => db.AuditLogs.Add(new()
    { EntityName = Entity, EntityId = id.ToString(), ActorId = actor.Id, ActorType = "EMPLOYEE", ActionType = action, NewData = JsonSerializer.Serialize(data), CreatedAt = now });
}
