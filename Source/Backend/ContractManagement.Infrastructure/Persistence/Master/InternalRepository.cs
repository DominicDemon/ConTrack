using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed class InternalRepository(MasterDbContext db, InternalTemplateStorage storage) : IInternalRepository
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal const string SnapshotKey = "__draft.v1";
    public async Task<InternalUser?> Authenticate(string username, string password, CancellationToken ct)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.EmployeeUsername == username && e.EmployeeStatus == "ACTIVE", ct);
        if (employee is null || new PasswordHasher<Employee>().VerifyHashedPassword(employee, employee.EmployeePasswordHash, password) == PasswordVerificationResult.Failed) return null;
        employee.EmployeeLastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetUser(employee.EmployeeId, ct);
    }
    public async Task<InternalUser?> GetUser(long id, CancellationToken ct)
    {
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.EmployeeId == id && e.EmployeeStatus == "ACTIVE", ct);
        if (employee is null) return null;
        var permissions = await (from er in db.EmployeeRoles join r in db.SystemRoles on er.SystemRoleId equals r.SystemRoleId
            join rp in db.SystemRolePermissions on r.SystemRoleId equals rp.SystemRoleId
            join p in db.SystemPermissions on rp.SystemPermissionId equals p.SystemPermissionId
            where er.EmployeeId == id && r.SystemRoleStatus == "ACTIVE" select p.SystemPermissionCode).Distinct().ToArrayAsync(ct);
        // Sales employees may create and manage their own contract drafts. Their list/detail queries
        // remain owner-scoped by InternalDraftService and Drafts; this does not grant company-wide access.
        var isSalesEmployee = await (from assignment in db.EmployeeDepartments
            join department in db.Departments on assignment.DepartmentId equals department.DepartmentId
            where assignment.EmployeeId == id && department.DepartmentStatus == "ACTIVE"
                && (department.DepartmentCode == "KD" || department.DepartmentName.Contains("Kinh Doanh"))
            select assignment.EmployeeId).AnyAsync(ct);
        if (isSalesEmployee)
            permissions = permissions.Append("draft.write").Distinct().ToArray();
        if (employee.EmployeeCode == "ADM-SYSADMIN")
            permissions = permissions.Concat(["draft.write", "draft.manage.all"]).Distinct().ToArray();
        return new(id, employee.EmployeeDisplayName, employee.EmployeeUsername, permissions);
    }
    public async Task<List<TemplateDefinition>> Templates(CancellationToken ct)
    {
        var ids = await db.ContractTemplates.Where(t => t.IsActive && t.ContractTemplateDocumentType == 1).Select(t => t.ContractTemplateId).ToListAsync(ct);
        var result = new List<TemplateDefinition>();
        foreach (var id in ids)
        {
            var file = await db.FileStorages.AsNoTracking().Where(f => f.FileStorageObjecttype == "INTERNAL_TEMPLATE" && f.FileStorageObjectid == id)
                .OrderByDescending(f => f.FileStorageFileid).FirstOrDefaultAsync(ct);
            if (file is not null) result.Add(await storage.Read(file, ct));
        }
        return result;
    }
    public async Task<List<CustomerOption>> Customers(string? search, CancellationToken ct)
    {
        var query = db.Customers.AsNoTracking().Where(c => c.CustomerStatus == 1);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(c => c.CustomerDisplayName.Contains(search) || c.CustomerPersonalPhone.Contains(search));
        var ids = await query.OrderBy(c => c.CustomerDisplayName).Select(c => c.CustomerId).Take(100).ToListAsync(ct);
        var results = new List<CustomerOption>();
        foreach (var id in ids) { var customer = await Customer(id, ct); if (customer is not null) results.Add(customer); }
        return results;
    }
    public async Task<CustomerOption?> Customer(long id, CancellationToken ct)
    {
        var c = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.CustomerId == id && x.CustomerStatus == 1, ct);
        if (c is null) return null;
        var business = await db.CustomerBusinesses.AsNoTracking().Where(b => b.CustomerId == id).OrderBy(b => b.CustomerBusinessId).FirstOrDefaultAsync(ct);
        return new(c.CustomerId, c.CustomerDisplayName, c.CustomerPersonalPhone, new()
        {
            ["CUSTOMER_CONTACT_NAME"] = c.CustomerDisplayName, ["CUSTOMER_CONTACT_PHONE"] = c.CustomerPersonalPhone,
            ["CUSTOMER_COMPANY"] = business?.CustomerBusinessLegalName ?? c.CustomerDisplayName,
            ["CUSTOMER_ADDRESS"] = business?.CustomerBusinessOfficeAddress ?? c.CustomerAddress ?? "",
            ["CUSTOMER_EMAIL"] = c.CustomerEmail ?? "", ["CUSTOMER_TAX_CODE"] = business?.CustomerBusinessCompanyTaxIdentifier ?? ""
        });
    }
    public async Task<CustomerOption> CreateCustomer(CustomerInput input, long actor, CancellationToken ct)
    {
        var name = InternalDraftPolicy.Text(input.Name, 200);
        var phone = PhoneNumber.Normalize(input.Phone);
        if (name.Length == 0 || phone is null) throw new ArgumentException("Nhập tên và số điện thoại Việt Nam hợp lệ.");
        var email = InternalDraftPolicy.Text(input.Email, 255);
        if (email.Length > 0 && !System.Net.Mail.MailAddress.TryCreate(email, out _)) throw new ArgumentException("Email không hợp lệ.");
        var company = InternalDraftPolicy.Text(input.Company, 1000);
        var tax = InternalDraftPolicy.Text(input.TaxCode, 20);
        if (company.Length > 0 && tax.Length == 0) throw new ArgumentException("Thông tin doanh nghiệp cần mã số thuế.");
        if (await db.Customers.AnyAsync(c => c.CustomerPersonalPhone == phone || c.CustomerDisplayName == name || (email != "" && c.CustomerEmail == email), ct))
            throw new InternalAppException(409, "Tên hiển thị, SĐT hoặc email đã tồn tại. Hãy tìm và chọn khách có sẵn.");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var customer = new Customer { CustomerDisplayName = name, CustomerPersonalPhone = phone, CustomerEmail = email.Length == 0 ? null : email,
                CustomerAddress = InternalDraftPolicy.Text(input.Address, 1000), CustomerStatus = 1, CreatedAt = DateTime.UtcNow, CreatedBy = actor };
            customer.CustomerPasswordHash = new PasswordHasher<Customer>().HashPassword(customer, Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            db.Customers.Add(customer);
            await db.SaveChangesAsync(ct);
            if (company.Length > 0) db.CustomerBusinesses.Add(new CustomerBusiness { CustomerId = customer.CustomerId,
                CustomerBusinessLegalName = company, CustomerBusinessCompanyTaxIdentifier = tax, CustomerBusinessOfficeAddress = customer.CustomerAddress,
                CreatedAt = DateTime.UtcNow, CreatedBy = actor });
            db.AuditLogs.Add(new AuditLog { ActionType = "CREATE", EntityName = "CUSTOMER", EntityId = customer.CustomerId.ToString(), ActorType = "EMPLOYEE", ActorId = actor, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (await Customer(customer.CustomerId, ct))!;
        });
    }
    public Task<List<ProductOption>> Products(string? search, CancellationToken ct) => db.Products.AsNoTracking()
        .Where(p => search == null || p.ProductName.Contains(search)).OrderBy(p => p.ProductName).Take(100)
        .Select(p => new ProductOption(p.ProductId, p.ProductName, p.ProductSuggestedPrice ?? 0)).ToListAsync(ct);
    public async Task<bool> ProductsExist(IEnumerable<long> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToArray();
        return await db.Products.CountAsync(p => distinct.Contains(p.ProductId), ct) == distinct.Length;
    }
    public Task<List<EmployeeOption>> Employees(CancellationToken ct) => db.Employees.AsNoTracking().Where(e => e.EmployeeStatus == "ACTIVE")
        .OrderBy(e => e.EmployeeDisplayName).Select(e => new EmployeeOption(e.EmployeeId, e.EmployeeDisplayName)).ToListAsync(ct);
    public async Task<List<DraftSummary>> Drafts(long? owner, string? search, int? status, CancellationToken ct)
    {
        var query = from d in db.ContractDocuments.AsNoTracking() join e in db.Employees on d.EmployeeId equals e.EmployeeId
            join c in db.Customers on d.CustomerId equals c.CustomerId
            join v in db.ContractDynamicValues on d.ContractDocumentId equals v.ContractDocumentId
            where (!status.HasValue || d.ContractDocumentStatus == status) && d.TenantId == null && v.FieldKey == SnapshotKey && v.ContractVersionId == d.ContractDocumentCurrentVersionId
                && (!owner.HasValue || d.EmployeeId == owner)
                && (search == null || d.ContractDocumentName.Contains(search) || d.ContractDocumentCode!.Contains(search) || c.CustomerDisplayName.Contains(search))
            orderby d.UpdatedDate ?? d.CreatedDate descending
            select new { d, e.EmployeeDisplayName, c.CustomerDisplayName, v.FieldValue, Latest = db.ContractReviewRequests
                .Where(r => r.ContractDocumentId == d.ContractDocumentId).OrderByDescending(r => r.ContractReviewRequestId).Select(r => r.ContractReviewRequestStatus).FirstOrDefault() };
        return (await query.Take(200).ToListAsync(ct)).Select(x => new DraftSummary(x.d.ContractDocumentId, x.d.ContractDocumentCode!, x.d.ContractDocumentName,
            x.CustomerDisplayName, x.d.EmployeeId, x.EmployeeDisplayName, x.d.UpdatedDate ?? x.d.CreatedDate,
            JsonSerializer.Deserialize<DraftContent>(x.FieldValue, Json)!.MissingFields.Count, x.d.ContractDocumentStatus, x.Latest)).ToList();
    }
    public async Task<List<ContractArchiveEntry>> ContractArchive(string? search, int? status, CancellationToken ct)
    {
        var query = from d in db.ContractDocuments.AsNoTracking()
            join e in db.Employees.AsNoTracking() on d.EmployeeId equals e.EmployeeId
            join c in db.Customers.AsNoTracking() on d.CustomerId equals c.CustomerId
            join v in db.ContractVersions.AsNoTracking() on d.ContractDocumentCurrentVersionId equals v.ContractVersionId
            where d.TenantId == null && (!status.HasValue || d.ContractDocumentStatus == status)
                && (search == null || d.ContractDocumentName.Contains(search)
                    || (d.ContractDocumentCode != null && d.ContractDocumentCode.Contains(search))
                    || c.CustomerDisplayName.Contains(search))
            orderby d.UpdatedDate ?? d.CreatedDate descending
            select new { d, e.EmployeeDisplayName, c.CustomerDisplayName, v.ContractVersionVersionNo,
                v.ContractVersionIslocked };
        var rows = await query.Take(200).ToListAsync(ct);
        var ids = rows.Select(x => x.d.ContractDocumentId).ToArray();
        var signatures = await db.ContractElectronicSignatures.AsNoTracking()
            .Where(s => ids.Contains(s.ContractDocumentId))
            .GroupBy(s => s.ContractDocumentId)
            .Select(g => new { Id = g.Key, Count = g.Count(), LastSignedAt = g.Max(x => x.ContractElectronicSignedat) })
            .ToDictionaryAsync(x => x.Id, ct);
        return rows.Select(x =>
        {
            signatures.TryGetValue(x.d.ContractDocumentId, out var signing);
            return new ContractArchiveEntry(x.d.ContractDocumentId, x.d.ContractDocumentCode ?? "",
                x.d.ContractDocumentName, x.CustomerDisplayName, x.d.EmployeeId, x.EmployeeDisplayName,
                x.d.UpdatedDate ?? x.d.CreatedDate, x.d.ContractDocumentStatus, x.ContractVersionVersionNo,
                x.ContractVersionIslocked, signing?.Count ?? 0, signing?.LastSignedAt);
        }).ToList();
    }
    public async Task<List<SigningMonitorEntry>> SigningMonitor(string? search, int? status, CancellationToken ct)
    {
        var docs = await (from d in db.ContractDocuments.AsNoTracking()
            join e in db.Employees.AsNoTracking() on d.EmployeeId equals e.EmployeeId
            join c in db.Customers.AsNoTracking() on d.CustomerId equals c.CustomerId
            where d.TenantId == null && (!status.HasValue || d.ContractDocumentStatus == status)
                && (search == null || d.ContractDocumentName.Contains(search)
                    || (d.ContractDocumentCode != null && d.ContractDocumentCode.Contains(search))
                    || c.CustomerDisplayName.Contains(search))
            orderby d.UpdatedDate ?? d.CreatedDate descending
            select new { d, e.EmployeeDisplayName, c.CustomerDisplayName }).Take(200).ToListAsync(ct);
        var ids = docs.Select(x => x.d.ContractDocumentId).ToArray();
        var versionIds = docs.Where(x => x.d.ContractDocumentCurrentVersionId.HasValue)
            .Select(x => x.d.ContractDocumentCurrentVersionId!.Value).Distinct().ToArray();
        var versions = await db.ContractVersions.AsNoTracking().Where(v => versionIds.Contains(v.ContractVersionId))
            .ToDictionaryAsync(v => v.ContractVersionId, ct);
        var currentVersions = docs.Where(x => x.d.ContractDocumentCurrentVersionId.HasValue)
            .ToDictionary(x => x.d.ContractDocumentId, x => x.d.ContractDocumentCurrentVersionId!.Value);
        var signatures = await db.ContractElectronicSignatures.AsNoTracking()
            .Where(s => ids.Contains(s.ContractDocumentId) && versionIds.Contains(s.ContractVersionId)).ToListAsync(ct);
        var currentSignatures = signatures.Where(s => currentVersions.TryGetValue(s.ContractDocumentId, out var currentVersion)
                && s.ContractVersionId == currentVersion)
            .GroupBy(s => s.ContractDocumentId).ToDictionary(g => g.Key, g => new
            {
                Count = g.Count(), Signed = g.Count(x => x.ContractElectronicStatus == 1),
                CompanySigned = g.Any(x => x.ContractElectronicPartytype == 1 && x.ContractElectronicStatus == 1),
                CustomerSigned = g.Any(x => x.ContractElectronicPartytype == 2 && x.ContractElectronicStatus == 1),
                Last = g.Max(x => x.ContractElectronicSignedat)
            });
        var reviews = await db.ContractReviewRequests.AsNoTracking().Where(r => ids.Contains(r.ContractDocumentId))
            .GroupBy(r => r.ContractDocumentId).Select(g => new { Id = g.Key, Status = g.OrderByDescending(x => x.ContractReviewRequestId).Select(x => x.ContractReviewRequestStatus).FirstOrDefault() })
            .ToDictionaryAsync(x => x.Id, ct);
        return docs.Select(x =>
        {
            ContractVersion? version = null;
            var hasVersion = x.d.ContractDocumentCurrentVersionId is long versionId && versions.TryGetValue(versionId, out version);
            currentSignatures.TryGetValue(x.d.ContractDocumentId, out var signing);
            reviews.TryGetValue(x.d.ContractDocumentId, out var review);
            return new SigningMonitorEntry(x.d.ContractDocumentId, x.d.ContractDocumentCode ?? "", x.d.ContractDocumentName,
                x.CustomerDisplayName, x.d.EmployeeId, x.EmployeeDisplayName, x.d.UpdatedDate ?? x.d.CreatedDate,
                x.d.ContractDocumentStatus, hasVersion ? version!.ContractVersionVersionNo : 0,
                signing?.Count ?? 0, signing?.Signed ?? 0, signing?.CompanySigned ?? false, signing?.CustomerSigned ?? false, review?.Status, signing?.Last);
        }).ToList();
    }

    public async Task<SigningMonitorDetail?> SigningMonitorDetail(long id, CancellationToken ct)
    {
        var document = await db.ContractDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.ContractDocumentId == id && d.TenantId == null, ct);
        if (document is null) return null;
        var customerName = await db.Customers.AsNoTracking().Where(c => c.CustomerId == document.CustomerId).Select(c => c.CustomerDisplayName).SingleAsync(ct);
        var employeeName = await db.Employees.AsNoTracking().Where(e => e.EmployeeId == document.EmployeeId).Select(e => e.EmployeeDisplayName).SingleAsync(ct);
        var version = document.ContractDocumentCurrentVersionId is long versionId
            ? await db.ContractVersions.AsNoTracking().SingleOrDefaultAsync(v => v.ContractVersionId == versionId, ct) : null;
        var events = new List<SigningTimelineEvent>
        {
            new(document.CreatedDate, "contract", "Hồ sơ được tạo", $"Người phụ trách: {employeeName}", "complete")
        };
        if (document.UpdatedDate is DateTime updated && updated > document.CreatedDate)
            events.Add(new(updated, "contract", "Hồ sơ được cập nhật", null, "recorded"));
        var reviews = await db.ContractReviewRequests.AsNoTracking().Where(r => r.ContractDocumentId == id).OrderBy(r => r.SentAt).ToListAsync(ct);
        foreach (var review in reviews)
        {
            events.Add(new(review.SentAt, "review", "Gửi hồ sơ cho khách xem xét", $"Phiên bản yêu cầu #{review.ContractReviewRequestId}", "complete"));
            if (review.FirstViewedAt is DateTime viewed) events.Add(new(viewed, "review", "Khách đã mở hồ sơ", null, "complete"));
            if (review.RespondedAt is DateTime responded) events.Add(new(responded, "review", "Khách đã phản hồi", review.ContractReviewRequestCustomerNote, "complete"));
            if (review.FollowupDecidedAt is DateTime decided) events.Add(new(decided, "review", "Công ty đã xử lý phản hồi", review.ContractReviewRequestFollowupDecision, "complete"));
        }
        var approvals = await db.ContractApprovalRequests.AsNoTracking().Where(a => a.ContractDocumentId == id).OrderBy(a => a.SubmittedAt).ToListAsync(ct);
        foreach (var approval in approvals)
        {
            events.Add(new(approval.SubmittedAt, "approval", "Gửi yêu cầu phê duyệt nội bộ", $"Trạng thái phê duyệt: {approval.ContractApprovalRequestStatus}", "complete"));
            if (approval.ContractApprovalRequestResolvedAt is DateTime resolved)
                events.Add(new(resolved, "approval", "Yêu cầu phê duyệt đã được xử lý", approval.ContractApprovalRequestDecisioncomment, "complete"));
        }
        var signatures = await db.ContractElectronicSignatures.AsNoTracking().Where(s => s.ContractDocumentId == id)
            .OrderBy(s => s.CreatedAt).ToListAsync(ct);
        var signatureVersionIds = signatures.Select(s => s.ContractVersionId).Distinct().ToArray();
        var signatureVersions = await db.ContractVersions.AsNoTracking().Where(v => signatureVersionIds.Contains(v.ContractVersionId))
            .ToDictionaryAsync(v => v.ContractVersionId, ct);
        foreach (var signature in signatures)
        {
            var party = signature.ContractElectronicPartytype == 1 ? "Đại diện công ty" : "Khách hàng";
            var signatureVersionNo = signatureVersions.TryGetValue(signature.ContractVersionId, out var signedVersion) ? signedVersion.ContractVersionVersionNo : 0;
            events.Add(new(signature.CreatedAt, "signature", $"Tạo yêu cầu chữ ký: {party} · phiên bản v{signatureVersionNo}", signature.ContractElectronicSignername,
                signature.ContractElectronicStatus == 1 ? "complete" : signature.ContractElectronicStatus == 2 ? "failed" : "pending"));
            if (signature.ContractElectronicSignedat is DateTime signed)
                events.Add(new(signed, "signature", $"Đã ký: {party} · phiên bản v{signatureVersionNo}", $"Người ký: {signature.ContractElectronicSignername}", "complete"));
            if (!string.IsNullOrWhiteSpace(signature.ContractElectronicRejectReason))
                events.Add(new(signature.CreatedAt, "signature", $"Chữ ký bị từ chối: {party}", signature.ContractElectronicRejectReason, "failed"));
        }
        var phoneVerifications = await db.ContractPhoneVerifies.AsNoTracking().Where(p => p.ContractDocumentId == id).ToListAsync(ct);
        events.AddRange(phoneVerifications.Select(p => new SigningTimelineEvent(p.CreatedAt, "identity", "Đăng ký số điện thoại xác minh", p.ContractPhoneVerifyReason, "recorded")));
        var links = await db.ContractAccessLinks.AsNoTracking().Where(l => l.ContractDocumentId == id).ToListAsync(ct);
        foreach (var link in links)
        {
            var completedByCustomer = link.ContractAccessLinkRevocationReason == "CUSTOMER_SIGNED";
            events.Add(new(link.CreatedAt, "link", "Tạo liên kết cho khách", $"Hết hạn: {link.ExpiresAt:u}",
                completedByCustomer ? "complete" : link.RevokedAt is null ? "recorded" : "revoked"));
            if (link.ActivatedAt is DateTime activated) events.Add(new(activated, "link", "Liên kết được kích hoạt", null, "complete"));
            if (link.RevokedAt is DateTime revoked)
                events.Add(new(revoked, "link", completedByCustomer ? "Khách đã hoàn tất ký bằng liên kết" : "Liên kết bị thu hồi",
                    link.ContractAccessLinkRevocationReason, completedByCustomer ? "complete" : "failed"));
        }
        var linkIds = links.Select(l => l.ContractAccessLinkId).ToArray();
        var otpChallenges = await db.ContractCustomerOtpChallenges.AsNoTracking()
            .Where(challenge => linkIds.Contains(challenge.ContractAccessLinkId)).ToListAsync(ct);
        foreach (var challenge in otpChallenges)
        {
            events.Add(new(challenge.CreatedAt, "identity", "Đã yêu cầu mã OTP", null, "recorded"));
            if (challenge.VerifiedAt is DateTime verified)
                events.Add(new(verified, "identity", "Khách đã xác minh OTP", "Xác minh thành công để xem và ký hợp đồng.", "complete"));
        }
        var fileRows = await (from f in db.ContractDocumentFiles.AsNoTracking()
            join storage in db.FileStorages.AsNoTracking() on f.FileStorageFileid equals storage.FileStorageFileid
            where f.ContractDocumentId == id
            select new { f, storage }).ToListAsync(ct);
        events.AddRange(fileRows.Select(x => new SigningTimelineEvent(x.f.CreatedDate, "file", "Lưu tệp hồ sơ",
            $"{x.storage.FileStorageFilename} · {x.storage.FileStorageFilesize} byte", "recorded")));
        var audit = await db.AuditLogs.AsNoTracking().Where(a => a.EntityName == "CONTRACT_DOCUMENTS" && a.EntityId == id.ToString())
            .OrderBy(a => a.CreatedAt).Take(200).ToListAsync(ct);
        events.AddRange(audit.Select(a => new SigningTimelineEvent(a.CreatedAt, "audit", $"Thao tác: {a.ActionType}",
            $"{a.ActorType}{(a.ActorId.HasValue ? $" #{a.ActorId}" : "")}", "recorded")));
        var signers = signatures.Select(signature => new SigningSignerProgress(signature.ContractElectronicSignatureId,
            signatureVersions.TryGetValue(signature.ContractVersionId, out var signerVersion) ? signerVersion.ContractVersionVersionNo : 0,
            signature.ContractElectronicPartytype == 1 ? "Công ty" : "Khách hàng", signature.ContractElectronicSignertype,
            signature.ContractElectronicSignername, signature.ContractElectronicSignertitle, signature.ContractElectronicSigneremail,
            signature.ContractElectronicStatus, signature.ContractElectronicSignedat, signature.ContractElectronicDocumentHashAtSigning,
            signature.ContractElectronicRejectReason)).ToList();
        var artifacts = fileRows.Select(x => new SigningArtifactInfo(x.storage.FileStorageFilename, x.storage.FileStorageContenttype,
            x.storage.FileStorageFilesize, x.storage.FileStorageSha256, x.f.ContractDocumentFileRole, x.f.IsPrimary, x.f.CreatedDate)).ToList();
        events.Sort((a, b) => a.At.CompareTo(b.At));
        return new SigningMonitorDetail(document.ContractDocumentId, document.ContractDocumentCode ?? "", document.ContractDocumentName,
            customerName, employeeName, document.CreatedDate, document.UpdatedDate ?? document.CreatedDate, document.ContractDocumentStatus,
            version?.ContractVersionVersionNo ?? 0, version?.ContractVersionIslocked ?? false, version?.ContractVersionSnapshothash,
            events, signers, artifacts);
    }
    public async Task<DraftRecord?> Draft(long id, CancellationToken ct)
    {
        var d = await db.ContractDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.ContractDocumentId == id && d.TenantId == null, ct);
        if (d is null) return null;
        var v = await db.ContractVersions.AsNoTracking().SingleOrDefaultAsync(v => v.ContractVersionId == d.ContractDocumentCurrentVersionId, ct);
        if (v is null) return null;
        var snapshot = await db.ContractDynamicValues.AsNoTracking().SingleOrDefaultAsync(x => x.ContractDocumentId == id && x.ContractVersionId == v.ContractVersionId && x.FieldKey == SnapshotKey, ct);
        if (snapshot is null) return null;
        var name = await db.Employees.Where(e => e.EmployeeId == d.EmployeeId).Select(e => e.EmployeeDisplayName).SingleAsync(ct);
        var customer = await db.Customers.AsNoTracking().Where(c => c.CustomerId == d.CustomerId).Select(c => new { c.CustomerDisplayName, c.CustomerPersonalPhone }).SingleAsync(ct);
        var latest = await db.ContractReviewRequests.AsNoTracking().Where(r => r.ContractDocumentId == id).OrderByDescending(r => r.ContractReviewRequestId)
            .Select(r => r.ContractReviewRequestStatus).FirstOrDefaultAsync(ct);
        var content = JsonSerializer.Deserialize<DraftContent>(snapshot.FieldValue, Json)!;
        var phone = PhoneNumber.Normalize(customer.CustomerPersonalPhone);
        var blocked = ContractReviewPolicy.CanSend(d.ContractDocumentStatus, latest, v.ContractVersionIslocked, content.MissingFields.Count)
            ?? (phone is null ? "Khách hàng chưa có số điện thoại hợp lệ." : null);
        return new(id, d.ContractDocumentCode!, d.EmployeeId, name, Convert.ToBase64String(d.RowVersion), Convert.ToBase64String(v.ContractVersionRowversion),
            d.UpdatedDate ?? d.CreatedDate, content, d.ContractDocumentStatus, v.ContractVersionVersionNo, v.ContractVersionIslocked,
            ContractReviewPolicy.CanEdit(d.ContractDocumentStatus, v.ContractVersionIslocked), customer.CustomerDisplayName,
            PhoneNumber.Mask(phone ?? customer.CustomerPersonalPhone), latest, blocked);
    }
    public async Task<DraftRecord> Save(long? id, SaveDraftRequest request, InternalUser actor, CancellationToken ct)
    {
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var now = DateTime.UtcNow;
                var content = request.Content;
                ContractDocument document;
                ContractVersion version;
                string? oldData = null;
                var serialized = JsonSerializer.Serialize(content, Json);
                if (id.HasValue)
                {
                    document = await db.ContractDocuments.SingleOrDefaultAsync(d => d.ContractDocumentId == id && d.TenantId == null, ct)
                        ?? throw new InternalAppException(404, "Không tìm thấy bản nháp.");
                    InternalDraftService.RequireAccess(actor, document.EmployeeId);
                    version = await db.ContractVersions.SingleAsync(v => v.ContractVersionId == document.ContractDocumentCurrentVersionId, ct);
                    if (!ContractReviewPolicy.CanEdit(document.ContractDocumentStatus, version.ContractVersionIslocked)) throw new InternalAppException(409, "Nháp đã khóa hoặc chuyển trạng thái.");
                    if (request.RowVersion != Convert.ToBase64String(document.RowVersion) || request.VersionRowVersion != Convert.ToBase64String(version.ContractVersionRowversion))
                        throw new InternalAppException(409, "Nháp đã được người khác sửa. Tải lại bản mới trước khi lưu.");
                    var values = await db.ContractDynamicValues.Where(x => x.ContractDocumentId == id && x.ContractVersionId == version.ContractVersionId).ToListAsync(ct);
                    oldData = values.FirstOrDefault(x => x.FieldKey == SnapshotKey)?.FieldValue;
                    var contentChanged = oldData is not null && !SameDraftSnapshot(oldData, serialized);
                    if (contentChanged && !ContractVersionNotes.IsWorking(version.ContractVersionChangenote))
                    {
                        var previous = version;
                        version = new ContractVersion
                        {
                            ContractDocumentId = document.ContractDocumentId,
                            ContractTemplateId = previous.ContractTemplateId,
                            ContractVersionVersionNo = await db.ContractVersions.Where(v => v.ContractDocumentId == id)
                                .MaxAsync(v => v.ContractVersionVersionNo, ct) + 1,
                            ContractVersionChangenote = $"Chỉnh sửa từ phiên bản {previous.ContractVersionVersionNo}",
                            ContractVersionSubtotal = previous.ContractVersionSubtotal,
                            ContractVersionTotaldiscount = previous.ContractVersionTotaldiscount,
                            ContractVersionTotalvat = previous.ContractVersionTotalvat,
                            ContractVersionTotalamount = previous.ContractVersionTotalamount,
                            ContractVersionCurrencycode = previous.ContractVersionCurrencycode,
                            ContractVersionEffectiveDate = previous.ContractVersionEffectiveDate,
                            ContractVersionExpireDate = previous.ContractVersionExpireDate,
                            ContractVersionCreatedDate = now
                        };
                        db.ContractVersions.Add(version);
                        await db.SaveChangesAsync(ct);
                        document.ContractDocumentCurrentVersionId = version.ContractVersionId;
                    }
                    else
                    {
                        db.ContractDynamicValues.RemoveRange(values);
                        db.ContractDocumentItems.RemoveRange(await db.ContractDocumentItems.Where(x => x.ContractDocumentId == id && x.ContractVersionId == version.ContractVersionId).ToListAsync(ct));
                        db.ContractTerms.RemoveRange(await db.ContractTerms.Where(x => x.ContractDocumentId == id && x.ContractVersionId == version.ContractVersionId).ToListAsync(ct));
                        if (contentChanged && ContractVersionNotes.IsWorking(version.ContractVersionChangenote))
                            version.ContractVersionChangenote = ContractVersionNotes.Display(version.ContractVersionChangenote);
                    }
                }
                else
                {
                    document = new ContractDocument { CustomerId = content.CustomerId, EmployeeId = actor.Id, ContractTemplateId = content.TemplateId,
                        ContractDocumentName = content.Name, ContractDocumentCode = "DRAFT-" + Guid.NewGuid().ToString("N"), ContractDocumentType = 1,
                        ContractDocumentStatus = 0, CreatedDate = now };
                    db.ContractDocuments.Add(document);
                    await db.SaveChangesAsync(ct);
                    version = new ContractVersion { ContractDocumentId = document.ContractDocumentId, ContractTemplateId = content.TemplateId,
                        ContractVersionVersionNo = 1, ContractVersionCurrencycode = "VND", ContractVersionCreatedDate = now };
                    db.ContractVersions.Add(version);
                    await db.SaveChangesAsync(ct);
                    document.ContractDocumentCurrentVersionId = version.ContractVersionId;
                }
                document.ContractDocumentName = content.Name;
                document.UpdatedDate = now;
                version.ContractVersionSubtotal = content.Totals.Subtotal; version.ContractVersionTotaldiscount = content.Totals.Discount;
                version.ContractVersionTotalvat = content.Totals.Vat; version.ContractVersionTotalamount = content.Totals.Total;
                version.ContractVersionEffectiveDate = content.EffectiveDate?.ToDateTime(TimeOnly.MinValue);
                version.ContractVersionExpireDate = content.ExpireDate?.ToDateTime(TimeOnly.MinValue);
                // Always update the version token, including field-only edits.
                version.ContractVersionChangenote ??= "Bản nháp ban đầu";
                foreach (var item in content.Items) db.ContractDocumentItems.Add(new ContractDocumentItem {
                    ContractDocumentId = document.ContractDocumentId, ContractVersionId = version.ContractVersionId, ProductId = item.ProductId,
                    ContractDocumentItemName = item.Name, ContractDocumentItemQuantity = item.Quantity, ContractDocumentItemUnitPrice = item.UnitPrice,
                    ContractDocumentItemLineSubtotal = InternalDraftPolicy.Round(item.Quantity * item.UnitPrice), ContractDocumentItemDiscountPercent = item.DiscountPercent,
                    ContractDocumentItemFixedDiscountAmount = item.DiscountAmount });
                for (var i = 0; i < content.Terms.Count; i++) { var term = content.Terms[i]; db.ContractTerms.Add(new ContractTerm {
                    ContractDocumentId = document.ContractDocumentId, ContractVersionId = version.ContractVersionId, ContractTermTermcode = term.Key,
                    ContractTermTermtitle = term.Title, ContractTermTermcontent = term.Content, ContractTermIsnegotiable = term.Editable,
                    ContractTermDisplayorder = i, CreatedAt = now }); }
                foreach (var field in content.Fields.Append(new KeyValuePair<string,string>(SnapshotKey, serialized))) db.ContractDynamicValues.Add(new ContractDynamicValue {
                    ContractDocumentId = document.ContractDocumentId, ContractVersionId = version.ContractVersionId, FieldKey = field.Key, FieldValue = field.Value });
                db.AuditLogs.Add(new AuditLog { ActionType = id.HasValue ? "UPDATE" : "CREATE", EntityName = "CONTRACT_DOCUMENTS",
                    EntityId = document.ContractDocumentId.ToString(), ActorType = "EMPLOYEE", ActorId = actor.Id, OldData = oldData, NewData = serialized, CreatedAt = now });
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return (await Draft(document.ContractDocumentId, ct))!;
            });
        }
        catch (DbUpdateConcurrencyException) { throw new InternalAppException(409, "Nháp vừa được cập nhật. Tải lại bản mới trước khi lưu."); }
    }

    private static bool SameDraftSnapshot(string current, string incoming)
    {
        try { return JsonNode.DeepEquals(JsonNode.Parse(current), JsonNode.Parse(incoming)); }
        catch (JsonException) { return string.Equals(current, incoming, StringComparison.Ordinal); }
    }
}

public sealed class InternalTemplateStorage(string root)
{
    public string Root { get; } = Path.GetFullPath(root);
    public static string ResolveRoot(string? configuredRoot, string contentRoot, string assemblyDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredRoot))
            return Path.GetFullPath(configuredRoot, contentRoot);

        var candidates = new[]
        {
            Path.Combine(contentRoot, "private-storage", "internal-templates"),
            Path.Combine(contentRoot, "..", "private-storage", "internal-templates"),
            Path.Combine(assemblyDirectory, "..", "..", "Backend", "private-storage", "internal-templates")
        }.Select(Path.GetFullPath).ToArray();

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    public async Task<TemplateDefinition> Read(FileStorage file, CancellationToken ct)
    {
        if (Path.GetFileName(file.FileStorageStoragekey) != file.FileStorageStoragekey) throw new InvalidOperationException("Invalid template storage key.");
        var bytes = await File.ReadAllBytesAsync(Path.Combine(Root, file.FileStorageStoragekey), ct);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(file.FileStorageSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Template checksum mismatch.");
        return JsonSerializer.Deserialize<TemplateDefinition>(bytes, InternalRepository.Json) ?? throw new InvalidOperationException("Invalid template definition.");
    }
}
