using System.Text.Json;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Internal;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

/// <summary>
/// Removes a failed contract for good: one the customer rejected or that was cancelled. Its versions, terms, values, items,
/// comments, reviews, links, approvals, signatures, files and notifications go with it; the audit log keeps a HARD_DELETE entry.
/// A signed contract, one with invoices, or one that still has documents attached is never deleted.
/// </summary>
public sealed class ContractHardDelete(MasterDbContext db, InternalTemplateStorage storage)
{
    public static bool CanDelete(int status) => status is ContractDocumentStatus.Cancelled or ContractDocumentStatus.Rejected;

    public async Task Delete(long id, InternalUser actor, CancellationToken ct)
    {
        var doc = await db.ContractDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.ContractDocumentId == id && d.TenantId == null, ct)
            ?? throw new InternalAppException(404, "Không tìm thấy hợp đồng.");
        if (!actor.CanManageContractWorkflow && !(actor.CanWrite && doc.EmployeeId == actor.Id))
            throw new InternalAppException(403, "Chỉ người phụ trách hoặc Hành chính được xóa hợp đồng này.");
        if (!CanDelete(doc.ContractDocumentStatus))
            throw new InternalAppException(409, "Chỉ xóa hẳn được hợp đồng đã bị từ chối hoặc đã hủy.");
        if (await db.ContractDocumentRelations.AnyAsync(r => r.ParentDocumentId == id, ct))
            throw new InternalAppException(409, "Hợp đồng còn văn bản đi kèm. Xóa các văn bản đi kèm trước.");
        var versions = await db.ContractVersions.Where(v => v.ContractDocumentId == id).Select(v => v.ContractVersionId).ToListAsync(ct);
        var keys = await db.FileStorages.AsNoTracking()
            .Where(f => (f.FileStorageObjecttype == "CONTRACT_VERSION" && f.FileStorageObjectid != null && versions.Contains(f.FileStorageObjectid.Value)) || (f.FileStorageObjecttype == "CONTRACT_SIGNING" && f.FileStorageObjectid == id)
                || db.ContractDocumentFiles.Any(x => x.ContractDocumentId == id && x.FileStorageFileid == f.FileStorageFileid))
            .Select(f => f.FileStorageStoragekey).ToListAsync(ct);

        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM INVOICE WHERE contract_version_id IN (SELECT contract_version_id FROM CONTRACT_VERSION WHERE contract_document_id = {id})").SingleAsync(ct) > 0)
                throw new InternalAppException(409, "Hợp đồng đã có hóa đơn nên không xóa được.");
            // Children before parents; the tables keep few foreign keys, so the order follows the columns.
            string[] statements =
            [
                "DELETE FROM CONTRACT_CUSTOMER_OTP_CHALLENGE WHERE contract_access_link_id IN (SELECT contract_access_link_id FROM CONTRACT_ACCESS_LINK WHERE contract_document_id = {0}) OR contract_electronic_signature_id IN (SELECT contract_electronic_signature_id FROM CONTRACT_ELECTRONIC_SIGNATURE WHERE contract_document_id = {0})",
                "DELETE FROM CONTRACT_ACCESS_LINK WHERE contract_document_id = {0}",
                "DELETE FROM CONTRACT_ELECTRONIC_SIGNATURE WHERE contract_document_id = {0}",
                "DELETE FROM CONTRACT_NEGOTIATION_COMMENT WHERE contract_document_id = {0}",
                "DELETE FROM CONTRACT_APPROVAL_REQUEST WHERE contract_document_id = {0}",
                "UPDATE CONTRACT_REVIEW_REQUEST SET previous_review_request_id = NULL WHERE contract_document_id = {0}",
                "DELETE FROM CONTRACT_REVIEW_REQUEST WHERE contract_document_id = {0}",
                "DELETE FROM CONTRACT_SIGNED_EVIDENCE WHERE contract_document_file_id IN (SELECT contract_document_file_id FROM CONTRACT_DOCUMENT_FILES WHERE contract_document_id = {0})",
                "DELETE FROM FILE_STORAGE WHERE file_storage_fileid IN (SELECT file_storage_fileid FROM CONTRACT_DOCUMENT_FILES WHERE contract_document_id = {0})",
                "DELETE FROM CONTRACT_DOCUMENT_FILES WHERE contract_document_id = {0}",
                "DELETE FROM CONTRACT_DOCUMENT_ITEMS WHERE contract_document_id = {0}",
                "DELETE FROM CONTRACT_DYNAMIC_VALUES WHERE contract_document_id = {0}",
                "DELETE FROM CONTRACT_HANDOVER_RECORD WHERE contract_document_id = {0}",
                "DELETE FROM CONTRACT_PAYMENT_MILESTONE WHERE contract_document_id = {0}",
                "DELETE FROM CONTRACT_PHONE_VERIFY WHERE contract_document_id = {0}",
                "DELETE FROM CONTRACT_TERM WHERE contract_document_id = {0}",
                "UPDATE SMS_OUTBOX SET contract_document_id = NULL WHERE contract_document_id = {0}",
                "UPDATE OCR_JOB SET contract_document_id = NULL WHERE contract_document_id = {0}",
                "IF OBJECT_ID('dbo.CONTRACT_CHECKLIST', 'U') IS NOT NULL DELETE FROM CONTRACT_CHECKLIST WHERE version_id IN (SELECT contract_version_id FROM CONTRACT_VERSION WHERE contract_document_id = {0})",
                "DELETE FROM FILE_STORAGE WHERE (file_storage_objecttype = 'CONTRACT_VERSION' AND file_storage_objectid IN (SELECT contract_version_id FROM CONTRACT_VERSION WHERE contract_document_id = {0})) OR (file_storage_objecttype = 'CONTRACT_SIGNING' AND file_storage_objectid = {0})",
                "DELETE FROM NOTIFICATION WHERE notification_subjecttype = 'CONTRACT_DOCUMENT' AND notification_subjectid = {0}",
                "DELETE FROM CONTRACT_DOCUMENT_RELATION WHERE child_document_id = {0}",
                "UPDATE CONTRACT_DOCUMENTS SET contract_document_current_version_id = NULL WHERE contract_document_id = {0}",
                "DELETE FROM CONTRACT_VERSION WHERE contract_document_id = {0}",
                "DELETE FROM CONTRACT_DOCUMENTS WHERE contract_document_id = {0}",
            ];
            foreach (var sql in statements) await db.Database.ExecuteSqlRawAsync(sql, [id], ct);
            db.AuditLogs.Add(new() { ActionType = "HARD_DELETE", EntityName = "CONTRACT_DOCUMENTS", EntityId = id.ToString(), ActorType = "EMPLOYEE", ActorId = actor.Id,
                CreatedAt = DateTime.UtcNow, OldData = JsonSerializer.Serialize(new { doc.ContractDocumentCode, doc.ContractDocumentName, doc.CustomerId, doc.ContractDocumentStatus }) });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        // The rows are gone; files in the template store are removed too (best effort: a missing file is fine).
        foreach (var key in keys.Where(k => !string.IsNullOrWhiteSpace(k)))
        {
            var path = Path.GetFullPath(Path.Combine(storage.Root, key));
            if (!path.StartsWith(Path.GetFullPath(storage.Root), StringComparison.OrdinalIgnoreCase)) continue;
            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
