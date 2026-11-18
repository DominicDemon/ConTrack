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
        var stage = approval?.InternalStage ?? "DRAFT";
        var reviewStatus = await db.ContractReviewRequests.Where(r => r.ContractDocumentId == id && r.ContractVersionId == version.ContractVersionId)
            .OrderByDescending(r => r.ContractReviewRequestId).Select(r => r.ContractReviewRequestStatus).FirstOrDefaultAsync(ct);
        if (reviewStatus is not null && version.ContractVersionIslocked && stage == "APPROVED") stage = "CUSTOMER_" + reviewStatus;
        return new(stage, approval is null ? null : Convert.ToBase64String(approval.ContractApprovalRequestRowversion),
            actor.CanWrite && !version.ContractVersionIslocked && stage is "DRAFT" or "RETURNED",
            stage == "OFFICE" && actor.CanReviewLegal || stage == "DIRECTOR" && actor.CanApprove, history);
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
                if (!(approval.InternalStage == "OFFICE" && actor.CanReviewLegal || approval.InternalStage == "DIRECTOR" && actor.CanApprove))
                    throw new InternalAppException(403, "Không đúng quyền hoặc thứ tự duyệt.");
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
                    approval.InternalStage = approval.InternalStage == "OFFICE" ? "DIRECTOR" : "APPROVED";
                    if (approval.InternalStage == "APPROVED") approval.ContractApprovalRequestStatus = 1;
                }
                else throw new ArgumentException("Thao tác không hợp lệ.");
                approval.ContractApprovalRequestDecisioncomment = note;
                if (approval.InternalStage is "RETURNED" or "APPROVED") { approval.ResolvedByEmployeeId = actor.Id; approval.ContractApprovalRequestResolvedAt = now; }
            }
            doc.UpdatedDate = now;
            version.ContractVersionChangenote = "Duyệt nội bộ " + now.ToString("O");
            Audit(id, actor, action.ToUpperInvariant(), new { version = version.ContractVersionVersionNo, stage = approval.InternalStage, note }, now);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    public async Task RequireApproved(long id, long version, CancellationToken ct)
    {
        var approval = await Latest(id, version, ct);
        if (approval?.InternalStage != "APPROVED" || approval.InternalContentHash != await ContentHash(id, version, ct))
            throw new InternalAppException(409, "Phiên bản cần được Hành chính và Sếp duyệt trước khi gửi khách.");
    }

    public async Task Comment(long id, InternalCommentInput input, InternalUser actor, CancellationToken ct)
    {
        await ReviewData.InTransaction(db, async () =>
        {
            var doc = await Document(id, actor, ct);
            var version = await db.ContractVersions.SingleAsync(v => v.ContractVersionId == doc.ContractDocumentCurrentVersionId, ct);
            ReviewData.Expect(doc.RowVersion, input.RowVersion);
            ReviewData.Expect(version.ContractVersionRowversion, input.VersionRowVersion);
            if (input.ResolveId is not null)
            {
                if (!actor.CanReviewLegal) throw new InternalAppException(403, "Chỉ Hành chính được xử lý nhận xét.");
                if (!await db.AuditLogs.AnyAsync(a => a.AuditLogId == input.ResolveId && a.EntityName == Entity && a.EntityId == id.ToString() && a.ActionType == "COMMENT", ct))
                    throw new InternalAppException(404, "Không tìm thấy nhận xét.");
            }
            else if (string.IsNullOrWhiteSpace(input.Content)) throw new ArgumentException("Nhập nhận xét.");
            if (input.TermKey is not null && !await db.ContractTerms.AnyAsync(t => t.ContractDocumentId == id && t.ContractVersionId == version.ContractVersionId && t.ContractTermTermcode == input.TermKey, ct))
                throw new ArgumentException("Điều khoản không thuộc phiên bản hiện tại.");
            var now = DateTime.UtcNow;
            doc.UpdatedDate = now;
            Audit(id, actor, input.ResolveId is null ? "COMMENT" : "RESOLVE", new { version = version.ContractVersionVersionNo, termKey = input.TermKey, note = InternalDraftPolicy.Text(input.Content, 4000), resolveId = input.ResolveId }, now);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    private void Audit(long id, InternalUser actor, string action, object data, DateTime now) => db.AuditLogs.Add(new()
    { EntityName = Entity, EntityId = id.ToString(), ActorId = actor.Id, ActorType = "EMPLOYEE", ActionType = action, NewData = JsonSerializer.Serialize(data), CreatedAt = now });
}
