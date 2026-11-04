using System.Security.Cryptography;
using System.Text.Json;
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
            if (file is not null) result.Add(DraftCompletionPolicy.UpgradeTemplate(await storage.Read(file, ct)));
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
        return new(c.CustomerId, c.CustomerDisplayName, c.CustomerPersonalPhone, new()
        {
            ["CUSTOMER_CONTACT_NAME"] = c.CustomerDisplayName, ["CUSTOMER_CONTACT_PHONE"] = c.CustomerPersonalPhone,
            ["CUSTOMER_COMPANY"] = c.CustomerDisplayName,
            ["CUSTOMER_ADDRESS"] = c.CustomerAddress ?? "",
            ["CUSTOMER_EMAIL"] = c.CustomerEmail ?? "", ["CUSTOMER_TAX_CODE"] = ""
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
    public Task<List<BusinessOption>> Businesses(long customerId, CancellationToken ct) => db.CustomerBusinesses.AsNoTracking()
        .Where(b => b.CustomerId == customerId).OrderBy(b => b.CustomerBusinessLegalName)
        .Select(b => new BusinessOption(b.CustomerBusinessId, b.CustomerBusinessLegalName, b.CustomerBusinessCompanyTaxIdentifier,
            b.CustomerBusinessOfficeAddress ?? "", b.CustomerBusinessRepresentativeName ?? "")).ToListAsync(ct);
    public async Task<BusinessOption> CreateBusiness(long customerId, BusinessInput input, long actor, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.CustomerId == customerId && c.CustomerStatus == 1, ct)) throw new ArgumentException("Khách hàng không tồn tại.");
        var name = InternalDraftPolicy.Text(input.Name, 1000); var tax = InternalDraftPolicy.Text(input.TaxCode, 20);
        var address = InternalDraftPolicy.Text(input.Address, 1000); var representative = InternalDraftPolicy.Text(input.Representative, 200);
        if (name.Length == 0 || tax.Length == 0 || address.Length == 0 || representative.Length == 0) throw new ArgumentException("Nhập đủ tên pháp lý, mã số thuế, địa chỉ và đại diện.");
        if (await db.CustomerBusinesses.AnyAsync(b => b.CustomerId == customerId && b.CustomerBusinessCompanyTaxIdentifier == tax, ct)) throw new InternalAppException(409, "Pháp nhân đã tồn tại; chọn trong danh sách.");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var b = new CustomerBusiness { CustomerId = customerId, CustomerBusinessLegalName = name, CustomerBusinessCompanyTaxIdentifier = tax,
                CustomerBusinessOfficeAddress = address, CustomerBusinessRepresentativeName = representative, CreatedAt = DateTime.UtcNow, CreatedBy = actor };
            db.CustomerBusinesses.Add(b); await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(new() { ActionType = "CREATE", EntityName = "CUSTOMER_BUSINESS", EntityId = b.CustomerBusinessId.ToString(), ActorType = "EMPLOYEE", ActorId = actor, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return new BusinessOption(b.CustomerBusinessId, name, tax, address, representative);
        });
    }
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
        return (await query.Take(200).ToListAsync(ct)).Select(x => { var content = JsonSerializer.Deserialize<DraftContent>(x.FieldValue, Json)!;
            if (x.d.ContractDocumentStatus == ContractDocumentStatus.Draft || (x.d.ContractDocumentStatus == ContractDocumentStatus.Negotiating && x.Latest is ContractReviewStatus.ChangesRequested or ContractReviewStatus.Rejected))
                DraftValidation.Prepare(content, DraftCompletionPolicy.UpgradeTemplate(content.TemplateSnapshot!));
            return new DraftSummary(x.d.ContractDocumentId, x.d.ContractDocumentCode!, x.d.ContractDocumentName,
            x.CustomerDisplayName, x.d.EmployeeId, x.EmployeeDisplayName, x.d.UpdatedDate ?? x.d.CreatedDate,
            content.MissingFields.Count, x.d.ContractDocumentStatus, x.Latest); }).ToList();
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
        var customer = await db.Customers.AsNoTracking().Where(c => c.CustomerId == d.CustomerId).Select(c => new { c.CustomerDisplayName, c.CustomerPersonalPhone, c.CustomerEmail }).SingleAsync(ct);
        var latest = await db.ContractReviewRequests.AsNoTracking().Where(r => r.ContractDocumentId == id).OrderByDescending(r => r.ContractReviewRequestId)
            .Select(r => r.ContractReviewRequestStatus).FirstOrDefaultAsync(ct);
        var content = JsonSerializer.Deserialize<DraftContent>(snapshot.FieldValue, Json)!;
        if (!v.ContractVersionIslocked) InternalDraftPolicy.Prepare(content, DraftCompletionPolicy.UpgradeTemplate(content.TemplateSnapshot!));
        var phone = ReviewLink.ContactPhone(content.Fields, customer.CustomerPersonalPhone);
        var blocked = ContractReviewPolicy.CanSend(d.ContractDocumentStatus, latest, v.ContractVersionIslocked, content.MissingFields.Count)
            ?? (phone is null ? "Khách hàng chưa có số điện thoại hợp lệ." : null)
            ?? (ReviewLink.ContactEmail(content.Fields, customer.CustomerEmail) is null ? "Nhập email khách hàng trong hợp đồng để gửi link xem." : null);
        var carried = await (from comment in db.ContractNegotiationComments.AsNoTracking()
            join term in db.ContractTerms on (long?)comment.ContractNegotiationCommentTermid equals term.ContractTermTermid into terms
            from term in terms.DefaultIfEmpty()
            where comment.ContractDocumentId == id && comment.ContractVersionId == v.ContractVersionId && comment.ContractNegotiationCommentCarriedforwardfromcommentid != null
            select new CarriedDraftComment(comment.ContractNegotiationCommentId, term == null ? null : term.ContractTermTermcode, comment.ContractNegotiationCommentContent,
                comment.ContractNegotiationCommentCarriedforwardfromcommentid, comment.ContractNegotiationCommentCarriedforwardfromversionid, comment.ContractNegotiationCommentState == 1)).ToListAsync(ct);
        return new(id, d.ContractDocumentCode!, d.EmployeeId, name, Convert.ToBase64String(d.RowVersion), Convert.ToBase64String(v.ContractVersionRowversion),
            d.UpdatedDate ?? d.CreatedDate, content, d.ContractDocumentStatus, v.ContractVersionVersionNo, v.ContractVersionIslocked,
            ContractReviewPolicy.CanEdit(d.ContractDocumentStatus, v.ContractVersionIslocked), customer.CustomerDisplayName,
            PhoneNumber.Mask(phone ?? customer.CustomerPersonalPhone), latest, blocked, DraftValidation.Missing(content), carried);
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
                var commentTerms = new Dictionary<long, string>();
                var pendingComments = new List<ContractNegotiationComment>();
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
                    db.ContractDynamicValues.RemoveRange(values);
                    db.ContractDocumentItems.RemoveRange(await db.ContractDocumentItems.Where(x => x.ContractDocumentId == id && x.ContractVersionId == version.ContractVersionId).ToListAsync(ct));
                    db.ContractPaymentMilestones.RemoveRange(await db.ContractPaymentMilestones.Where(x => x.ContractDocumentId == id && x.ContractVersionId == version.ContractVersionId).ToListAsync(ct));
                    await db.SaveChangesAsync(ct);
                    var previousTerms = await db.ContractTerms.Where(x => x.ContractDocumentId == id && x.ContractVersionId == version.ContractVersionId).ToListAsync(ct);
                    commentTerms = previousTerms.ToDictionary(t => t.ContractTermTermid, t => t.ContractTermTermcode);
                    pendingComments = await db.ContractNegotiationComments.Where(x => x.ContractDocumentId == id && x.ContractVersionId == version.ContractVersionId).ToListAsync(ct);
                    db.ContractTerms.RemoveRange(previousTerms);
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
                version.ContractVersionChangenote = "Cập nhật nháp " + now.ToString("O");
                foreach (var item in content.Items) db.ContractDocumentItems.Add(new ContractDocumentItem {
                    ContractDocumentId = document.ContractDocumentId, ContractVersionId = version.ContractVersionId, ProductId = item.ProductId,
                    ContractDocumentItemName = item.Name, ContractDocumentItemQuantity = item.Quantity, ContractDocumentItemUnitPrice = item.UnitPrice,
                    ContractDocumentItemLineSubtotal = InternalDraftPolicy.Round(item.Quantity * item.UnitPrice), ContractDocumentItemDiscountPercent = item.DiscountPercent,
                    ContractDocumentItemFixedDiscountAmount = item.DiscountAmount });
                var termMap = new Dictionary<string, ContractTerm>();
                for (var i = 0; i < content.Terms.Count; i++) { var term = content.Terms[i]; var entity = new ContractTerm {
                    ContractDocumentId = document.ContractDocumentId, ContractVersionId = version.ContractVersionId, ContractTermTermcode = term.Key,
                    ContractTermTermtitle = term.Title, ContractTermTermcontent = term.Content, ContractTermIsnegotiable = term.Editable,
                    ContractTermDisplayorder = i, CreatedAt = now }; db.ContractTerms.Add(entity); termMap[term.Key] = entity; }
                await db.SaveChangesAsync(ct);
                foreach (var comment in pendingComments) {
                    if (comment.ContractNegotiationCommentTermid is { } oldTerm && commentTerms.TryGetValue(oldTerm, out var key))
                        comment.ContractNegotiationCommentTermid = termMap.TryGetValue(key, out var mapped) ? checked((int)mapped.ContractTermTermid) : null;
                }
                for (var i = 0; i < content.Milestones.Count; i++) {
                    var m = content.Milestones[i];
                    // Incomplete rows remain in the snapshot until a payment term is selected.
                    if (!termMap.TryGetValue(m.TermKey, out var term)) continue;
                    db.ContractPaymentMilestones.Add(new() { ContractDocumentId = document.ContractDocumentId, ContractVersionId = version.ContractVersionId,
                        ContractTermId = term.ContractTermTermid, ContractPaymentMilestoneCode = m.Key, ContractPaymentMilestoneTitlevi = m.Title,
                        ContractPaymentMilestonePaymentpercent = m.Percent, ContractPaymentMilestoneAmount = m.Amount,
                        ContractPaymentMilestoneDueanchor = m.Anchor == "DATE" ? 3 : m.Anchor == "SIGNED" ? 1 : 2,
                        ContractPaymentMilestoneDaycountmode = 1, ContractPaymentMilestoneDueoffsetdays = m.OffsetDays,
                        ContractPaymentMilestoneDuedate = m.DueDate, ContractPaymentMilestoneConditionvi = m.Condition,
                        ContractPaymentMilestoneDisplayorder = i, CreatedAt = now });
                }
                var serialized = JsonSerializer.Serialize(content, Json);
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
}

public sealed class InternalTemplateStorage(string root)
{
    public string Root { get; } = Path.GetFullPath(root);
    public async Task<TemplateDefinition> Read(FileStorage file, CancellationToken ct)
    {
        if (Path.GetFileName(file.FileStorageStoragekey) != file.FileStorageStoragekey) throw new InvalidOperationException("Invalid template storage key.");
        var bytes = await File.ReadAllBytesAsync(Path.Combine(Root, file.FileStorageStoragekey), ct);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(file.FileStorageSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Template checksum mismatch.");
        return JsonSerializer.Deserialize<TemplateDefinition>(bytes, InternalRepository.Json) ?? throw new InvalidOperationException("Invalid template definition.");
    }
}
