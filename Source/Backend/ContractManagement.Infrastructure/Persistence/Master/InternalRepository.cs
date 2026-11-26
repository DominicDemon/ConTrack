using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractDocumentFileRoles = ContractManagement.Domain.Internal.Scans.ContractDocumentFileRoles;
using SignedEvidenceStatus = ContractManagement.Domain.Internal.Scans.SignedEvidenceStatus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed class InternalRepository(MasterDbContext db, InternalTemplateStorage storage, InternalWordTemplates? word = null,
    IConfiguration? configuration = null, IHostEnvironment? environment = null) : IInternalRepository
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
        var isSalesEmployee = await (from assignment in db.EmployeeDepartments
            join department in db.Departments on assignment.DepartmentId equals department.DepartmentId
            where assignment.EmployeeId == id && department.DepartmentStatus == "ACTIVE"
                && (department.DepartmentCode == "KD" || department.DepartmentName.Contains("Kinh Doanh"))
            select assignment.EmployeeId).AnyAsync(ct);
        var departmentRow = await (from ed in db.EmployeeDepartments join d in db.Departments on ed.DepartmentId equals d.DepartmentId
            where ed.EmployeeId == id select new { d.DepartmentId, d.DepartmentName, d.DepartmentManagerEmployeeId }).FirstOrDefaultAsync(ct);
        var isManager = departmentRow?.DepartmentManagerEmployeeId == id;
        var isSysAdmin = employee.EmployeeCode == "ADM-SYSADMIN" || employee.EmployeeUsername == "sysadmin";

        // Chỉ nhân viên/trưởng phòng thuộc 3 phòng ban hỗ trợ (2: Kỹ thuật, 4: Kế toán, 5: Hành chính) mới có màn hỗ trợ khách hàng
        var isSupportDepartment = departmentRow != null && (departmentRow.DepartmentId == 2 || departmentRow.DepartmentId == 4 || departmentRow.DepartmentId == 5);

        if (isSalesEmployee)
            permissions = permissions.Append("draft.write").Distinct().ToArray();
        if (isSysAdmin)
            permissions = permissions.Concat(["draft.write", "draft.manage.all", "customer.manage", "product.read", "product.manage"]).Distinct().ToArray();
        if (isSupportDepartment)
        {
            if (isManager)
                permissions = permissions.Concat(["ticket.manage.all", "ticket.handle"]).Distinct().ToArray();
            else
                permissions = permissions.Append("ticket.handle").Distinct().ToArray();
        }
        else if (!CanUseLocalSupport(employee.EmployeeUsername))
        {
            permissions = permissions.Where(p => p != "ticket.handle" && p != "ticket.manage.all").Distinct().ToArray();
        }

        return new(id, employee.EmployeeDisplayName, employee.EmployeeUsername, permissions, employee.EmployeeMustChangePassword,
            departmentRow?.DepartmentName, isManager || isSysAdmin);
    }
    private bool CanUseLocalSupport(string username) =>
        environment?.IsDevelopment() == true &&
        (configuration?["LocalTesting:SupportUsers"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(username, StringComparer.OrdinalIgnoreCase);

    public Task<List<TemplateDefinition>> Templates(CancellationToken ct) => TemplateLibrary(false, ct);
    public Task<List<TemplateDefinition>> TemplateLibrary(bool allTypes, CancellationToken ct) => TemplateLibrary(allTypes, ct, null);

    internal async Task<List<TemplateDefinition>> TemplateLibrary(bool allTypes, CancellationToken ct, long? editorTemplateId)
    {
        // Every document type (contract, appendix, handover record, receipt, payment request): drafts are made from any of them.
        var templates = await db.ContractTemplates.AsNoTracking()
            .Where(t => t.IsActive && (!editorTemplateId.HasValue || t.ContractTemplateId == editorTemplateId.Value))
            .ToListAsync(ct);
        _ = allTypes;
        var result = new List<TemplateDefinition>();
        foreach (var t in templates)
        {
            var id = t.ContractTemplateId;
            var file = await db.FileStorages.AsNoTracking().Where(f => f.FileStorageObjecttype == "INTERNAL_TEMPLATE" && f.FileStorageObjectid == id)
                .OrderByDescending(f => f.FileStorageFileid).FirstOrDefaultAsync(ct);
            // The table's type wins over the stored definition, which older files wrote without one (read back as 1).
            if (file is not null)
            {
                var stored = await storage.Read(file, ct);
                // Hành chính changes which fields are required on the template page (CONTRACT_TEMPLATE_FIELDS); the row wins over the stored file.
                var flags = (await db.ContractTemplateFields.AsNoTracking().Where(f => f.ContractTemplateId == id && f.ContractTemplateFieldDataType != "TERM")
                    .Select(f => new { f.ContractTemplateFieldKey, f.IsRequired }).ToListAsync(ct))
                    .GroupBy(f => f.ContractTemplateFieldKey).ToDictionary(g => g.Key, g => g.Last().IsRequired);
                var fields = stored.Fields.Select(f => flags.TryGetValue(f.Key, out var required) ? f with { Required = required } : f).ToList();
                fields.AddRange(DraftCompletionPolicy.PartyFields.Where(f => flags.ContainsKey(f.Key) && fields.All(x => x.Key != f.Key)).Select(f => f with { Required = flags[f.Key] }));
                result.Add(DraftCompletionPolicy.UpgradeTemplate(stored with { Name = t.ContractTemplateName, DocumentType = t.ContractTemplateDocumentType,
                    PrintFormat = stored.WordStorageKey is null ? ContractFormatSettings.Parse(t.PrintFormatJson) : ContractFormatSettings.ForWord(t.PrintFormatJson), ParentTemplateId = t.ParentTemplateId, Fields = fields }));
                continue;
            }
            // An uploaded template whose definition file is missing is skipped, as before; only SQL-defined templates have no stored files.
            if (await db.FileStorages.AnyAsync(f => f.FileStorageObjecttype.StartsWith("INTERNAL_TEMPLATE") && f.FileStorageObjectid == id, ct)) continue;
            if (await FromDatabase(t, ct, allowEmpty: editorTemplateId.HasValue) is { } defined) result.Add(DraftCompletionPolicy.UpgradeTemplate(defined with { PrintFormat = ContractFormatSettings.Parse(t.PrintFormatJson), DocumentType = t.ContractTemplateDocumentType, ParentTemplateId = t.ParentTemplateId }));
        }
        // Each template carries its type's sending rule; a draft made from it keeps that rule in its snapshot.
        var rules = await DocumentCategories.Rules(db, ct);
        return result.Select(t => t with { Rule = rules.GetValueOrDefault(t.DocumentType) }).ToList();
    }
    /// <summary>Database first: a template written by SQL (Artifacts/Database/Master/010-demo-template.sql) is CONTRACT_TEMPLATE plus
    /// CONTRACT_TEMPLATE_FIELDS; rows with data type TERM are its terms in id order (label = title, extra_options = default content).</summary>
    private async Task<TemplateDefinition?> FromDatabase(ContractTemplate t, CancellationToken ct, bool allowEmpty)
    {
        var rows = await db.ContractTemplateFields.AsNoTracking().Where(f => f.ContractTemplateId == t.ContractTemplateId)
            .OrderBy(f => f.ContractTemplateFieldId).ToListAsync(ct);
        var terms = rows.Where(f => f.ContractTemplateFieldDataType == "TERM").Select(f => new DraftTerm(f.ContractTemplateFieldKey, f.ContractTemplateFieldLabel, f.ExtraOptions ?? "")).ToList();
        if (terms.Count == 0 && !allowEmpty) return null;
        var fields = rows.Where(f => f.ContractTemplateFieldDataType != "TERM")
            .Select(f => new DraftField(f.ContractTemplateFieldKey, f.ContractTemplateFieldLabel, f.IsRequired, f.ContractTemplateFieldDataType)).ToList();
        return new(t.ContractTemplateId, t.ContractTemplateName, $"db-{(t.UpdatedDate ?? t.CreatedDate).Ticks}", true, fields, terms);
    }
    public async Task<string> RenameTemplate(long id, string? name, InternalUser actor, CancellationToken ct)
    {
        if (!actor.CanManageTemplates)
            throw new InternalAppException(403, "Bạn không có quyền quản lý mẫu hợp đồng.");
        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 500)
            throw new ArgumentException("Tên mẫu phải có từ 1 đến 500 ký tự.");
        var template = await db.ContractTemplates.SingleOrDefaultAsync(
            t => t.ContractTemplateId == id && t.IsActive, ct)
            ?? throw new InternalAppException(404, "Không tìm thấy mẫu văn bản đang hoạt động.");
        if (template.ContractTemplateName == name) return name;

        var now = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            ActionType = "RENAME_TEMPLATE",
            EntityName = "CONTRACT_TEMPLATE",
            EntityId = id.ToString(),
            ActorType = "EMPLOYEE",
            ActorId = actor.Id,
            CreatedAt = now,
            OldData = JsonSerializer.Serialize(new { name = template.ContractTemplateName }, Json),
            NewData = JsonSerializer.Serialize(new { name }, Json)
        });
        template.ContractTemplateName = name;
        template.UpdatedDate = now;
        await db.SaveChangesAsync(ct);
        return name;
    }

    /// <summary>
    /// Takes a template out of use. Drafts and signed documents keep their own snapshot of it, so the row and its files stay
    /// (they are the history of what was used); the template only leaves the template list and the draft editor.
    /// </summary>
    public async Task DeleteTemplate(long id, InternalUser actor, CancellationToken ct)
    {
        if (!actor.CanManageTemplates)
            throw new InternalAppException(403, "Bạn không có quyền quản lý mẫu văn bản.");
        var template = await db.ContractTemplates.SingleOrDefaultAsync(t => t.ContractTemplateId == id && t.IsActive, ct)
            ?? throw new InternalAppException(404, "Không tìm thấy mẫu văn bản đang hoạt động.");
        var now = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            ActionType = "DELETE_TEMPLATE",
            EntityName = "CONTRACT_TEMPLATE",
            EntityId = id.ToString(),
            ActorType = "EMPLOYEE",
            ActorId = actor.Id,
            CreatedAt = now,
            OldData = JsonSerializer.Serialize(new { name = template.ContractTemplateName, documentType = template.ContractTemplateDocumentType, isActive = true }, Json),
            NewData = JsonSerializer.Serialize(new { isActive = false }, Json)
        });
        template.IsActive = false;
        template.UpdatedDate = now;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The template library: every template of every type, hidden ones included, with its parent and how often it is used.</summary>
    public async Task<List<TemplateSummary>> TemplateLibrary(CancellationToken ct)
    {
        var rows = await db.ContractTemplates.AsNoTracking().OrderByDescending(t => t.ContractTemplateId).ToListAsync(ct);
        var words = await db.FileStorages.AsNoTracking().Where(f => f.FileStorageObjecttype == "INTERNAL_TEMPLATE_WORD")
            .Select(f => f.FileStorageObjectid).Distinct().ToListAsync(ct);
        var used = await db.ContractDocuments.AsNoTracking().GroupBy(d => d.ContractTemplateId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var names = rows.ToDictionary(t => t.ContractTemplateId, t => t.ContractTemplateName);
        var fieldRows = (await db.ContractTemplateFields.AsNoTracking().Where(f => f.ContractTemplateFieldDataType != "TERM")
            .OrderBy(f => f.ContractTemplateFieldId).ToListAsync(ct)).ToLookup(f => f.ContractTemplateId);
        return rows.Select(t => new TemplateSummary(t.ContractTemplateId, t.ContractTemplateName, t.ContractTemplateDocumentType,
            t.ParentTemplateId, t.ParentTemplateId is long p ? names.GetValueOrDefault(p) : null, t.IsActive, words.Contains(t.ContractTemplateId),
            rows.Count(c => c.ParentTemplateId == t.ContractTemplateId), used.GetValueOrDefault(t.ContractTemplateId), t.CreatedDate, t.UpdatedDate,
            TemplateFields(fieldRows[t.ContractTemplateId]))).ToList();
    }

    /// <summary>The template's own field rows, plus the party fields every document has (UpgradeTemplate) when it has no row for them.</summary>
    private static List<TemplateFieldOption> TemplateFields(IEnumerable<ContractTemplateField> rows)
    {
        var own = rows.Select(f => new TemplateFieldOption(f.ContractTemplateFieldKey, f.ContractTemplateFieldLabel, f.IsRequired)).ToList();
        return own.Concat(DraftCompletionPolicy.PartyFields.Where(f => own.All(x => x.Key != f.Key)).Select(f => new TemplateFieldOption(f.Key, f.Label, f.Required))).ToList();
    }

    /// <summary>Name, document type and parent of a template; its Word content is replaced through the Word preview and activation.</summary>
    public async Task<TemplateSummary> UpdateTemplate(long id, TemplateInfoInput input, InternalUser actor, CancellationToken ct)
    {
        if (!actor.CanManageTemplates) throw new InternalAppException(403, "Bạn không có quyền quản lý mẫu văn bản.");
        var name = input.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 500) throw new ArgumentException("Tên mẫu phải có từ 1 đến 500 ký tự.");
        var template = await db.ContractTemplates.SingleOrDefaultAsync(t => t.ContractTemplateId == id && t.IsActive, ct)
            ?? throw new InternalAppException(404, "Không tìm thấy mẫu văn bản đang dùng.");
        await TemplateHierarchy.Validate(db, id, input.DocumentType, input.ParentTemplateId, ct);
        var now = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            ActionType = "UPDATE_TEMPLATE", EntityName = "CONTRACT_TEMPLATE", EntityId = id.ToString(), ActorType = "EMPLOYEE", ActorId = actor.Id, CreatedAt = now,
            OldData = JsonSerializer.Serialize(new { name = template.ContractTemplateName, documentType = template.ContractTemplateDocumentType, parentTemplateId = template.ParentTemplateId }, Json),
            NewData = JsonSerializer.Serialize(new { name, documentType = input.DocumentType, parentTemplateId = input.ParentTemplateId }, Json)
        });
        template.ContractTemplateName = name;
        template.ContractTemplateDocumentType = input.DocumentType;
        template.ParentTemplateId = input.ParentTemplateId;
        if (input.RequiredFields is { } required)
        {
            // Documents already made keep the flags of their own snapshot; new ones take these.
            var keys = required.ToHashSet(StringComparer.Ordinal);
            var rows = await db.ContractTemplateFields.Where(f => f.ContractTemplateId == id && f.ContractTemplateFieldDataType != "TERM").ToListAsync(ct);
            if (keys.Any(k => rows.All(r => r.ContractTemplateFieldKey != k) && DraftCompletionPolicy.PartyFields.All(f => f.Key != k)))
                throw new ArgumentException("Có trường không thuộc mẫu này.");
            foreach (var row in rows) row.IsRequired = keys.Contains(row.ContractTemplateFieldKey);
            // A party field the template has no row for is stored once it is given its own flag.
            foreach (var party in DraftCompletionPolicy.PartyFields.Where(f => rows.All(r => r.ContractTemplateFieldKey != f.Key) && keys.Contains(f.Key) != f.Required))
                db.ContractTemplateFields.Add(new() { ContractTemplateId = id, ContractTemplateFieldKey = party.Key, ContractTemplateFieldLabel = party.Label,
                    ContractTemplateFieldDataType = party.DataType, IsRequired = keys.Contains(party.Key) });
        }
        template.UpdatedDate = now;
        template.ModifiedEmployeeId = checked((int)actor.Id);
        await db.SaveChangesAsync(ct);
        return (await TemplateLibrary(ct)).Single(t => t.Id == id);
    }

    /// <summary>Puts a hidden template back in the template list and the draft editor.</summary>
    public async Task<TemplateSummary> RestoreTemplate(long id, InternalUser actor, CancellationToken ct)
    {
        if (!actor.CanManageTemplates) throw new InternalAppException(403, "Bạn không có quyền quản lý mẫu văn bản.");
        var template = await db.ContractTemplates.SingleOrDefaultAsync(t => t.ContractTemplateId == id && !t.IsActive, ct)
            ?? throw new InternalAppException(404, "Không tìm thấy mẫu văn bản đã ẩn.");
        // A child whose parent is hidden comes back attached: the parent check would refuse it, so only the type is checked.
        TemplateHierarchy.ValidateType(template.ContractTemplateDocumentType);
        var now = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog { ActionType = "RESTORE_TEMPLATE", EntityName = "CONTRACT_TEMPLATE", EntityId = id.ToString(),
            ActorType = "EMPLOYEE", ActorId = actor.Id, CreatedAt = now,
            OldData = JsonSerializer.Serialize(new { isActive = false }, Json), NewData = JsonSerializer.Serialize(new { isActive = true }, Json) });
        template.IsActive = true;
        template.UpdatedDate = now;
        await db.SaveChangesAsync(ct);
        return (await TemplateLibrary(ct)).Single(t => t.Id == id);
    }

    public const string ChildRelation = "ATTACHMENT";

    /// <summary>A child document goes with a contract the actor works on, is made from a child template of that contract's template, for the same customer.</summary>
    internal async Task CheckChildDocument(long parentId, DraftContent content, InternalUser actor, CancellationToken ct)
    {
        var parent = await db.ContractDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.ContractDocumentId == parentId && d.TenantId == null, ct)
            ?? throw new InternalAppException(404, "Không tìm thấy hợp đồng cha.");
        InternalDraftService.RequireAccess(actor, parent.EmployeeId);
        if (parent.ContractDocumentType != TemplateHierarchy.Contract)
            throw new ArgumentException("Chỉ gắn văn bản đi kèm vào một hợp đồng.");
        if (!await db.ContractTemplates.AnyAsync(t => t.ContractTemplateId == content.TemplateId && t.IsActive && t.ParentTemplateId == parent.ContractTemplateId, ct))
            throw new ArgumentException("Chọn một mẫu con của mẫu hợp đồng này.");
        if (content.CustomerId != parent.CustomerId)
            throw new ArgumentException("Văn bản đi kèm phải cùng khách hàng với hợp đồng.");
    }

    public Task<long?> ParentDocument(long id, CancellationToken ct) => db.ContractDocumentRelations.AsNoTracking()
        .Where(r => r.ChildDocumentId == id && r.ContractDocumentRelationType == ChildRelation).Select(r => (long?)r.ParentDocumentId).FirstOrDefaultAsync(ct);
    public async Task<DraftRelations> Related(long id, InternalUser actor, CancellationToken ct)
    {
        var document = await db.ContractDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.ContractDocumentId == id && d.TenantId == null, ct)
            ?? throw new InternalAppException(404, "Không tìm thấy hồ sơ.");
        InternalDraftService.RequireAccess(actor, document.EmployeeId);
        var parentId = await db.ContractDocumentRelations.AsNoTracking().Where(r => r.ChildDocumentId == id && r.ContractDocumentRelationType == ChildRelation)
            .Select(r => (long?)r.ParentDocumentId).FirstOrDefaultAsync(ct);
        var childIds = await db.ContractDocumentRelations.AsNoTracking().Where(r => r.ParentDocumentId == id && r.ContractDocumentRelationType == ChildRelation)
            .Select(r => r.ChildDocumentId).ToListAsync(ct);
        var ids = childIds.Append(parentId ?? 0).ToList();
        var related = await db.ContractDocuments.AsNoTracking().Where(d => ids.Contains(d.ContractDocumentId))
            .Select(d => new RelatedDocument(d.ContractDocumentId, d.ContractDocumentCode, d.ContractDocumentName, d.ContractDocumentType,
                d.ContractDocumentStatus, d.ContractTemplateId, d.CreatedDate)).ToListAsync(ct);
        var childTemplates = document.ContractDocumentType != TemplateHierarchy.Contract ? [] : await db.ContractTemplates.AsNoTracking()
            .Where(t => t.IsActive && t.ParentTemplateId == document.ContractTemplateId).OrderBy(t => t.ContractTemplateDocumentType).ThenBy(t => t.ContractTemplateName)
            .Select(t => new ChildTemplateOption(t.ContractTemplateId, t.ContractTemplateName, t.ContractTemplateDocumentType)).ToListAsync(ct);
        return new(related.SingleOrDefault(d => d.Id == parentId), related.Where(d => childIds.Contains(d.Id)).OrderBy(d => d.CreatedAt).ToList(), childTemplates);
    }

    public async Task<List<CustomerOption>> Customers(string? search, CancellationToken ct, long? owner = null)
    {
        var query = db.Customers.AsNoTracking().Where(c => c.CustomerStatus == 1 && (owner == null || c.CustomerOwnerEmployeeId == owner));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(c => c.CustomerDisplayName.Contains(search) || c.CustomerPersonalPhone.Contains(search));
        var ids = await query.OrderBy(c => c.CustomerDisplayName).Select(c => c.CustomerId).Take(100).ToListAsync(ct);
        var results = new List<CustomerOption>();
        foreach (var id in ids) { var customer = await Customer(id, ct); if (customer is not null) results.Add(customer); }
        return results;
    }
    public async Task<List<CustomerLookupResult>> LookupCustomers(string? search, CancellationToken ct)
    {
        var term = search?.Trim() ?? "";
        if (term.Length == 0 || term.Length > 255) return [];
        var email = term.ToLowerInvariant();
        var phone = PhoneNumber.Normalize(term);
        var internationalPhone = phone is null ? null : "+84" + phone[1..];
        var tax = term.Replace("-", "").Replace(" ", "");

        var active = db.Customers.AsNoTracking().Where(c => c.CustomerStatus == 1);
        var people = active.Where(c => (c.CustomerEmail != null && c.CustomerEmail.ToLower() == email)
            || (phone != null && (c.CustomerPersonalPhone == phone || c.CustomerPersonalPhone == internationalPhone)));
        var individuals = await people.OrderBy(c => c.CustomerDisplayName).ThenBy(c => c.CustomerId).Take(50).ToListAsync(ct);
        var companies = await (from business in db.CustomerBusinesses.AsNoTracking()
            join customer in active on business.CustomerId equals customer.CustomerId
            where people.Any(c => c.CustomerId == customer.CustomerId)
                || (business.CustomerBusinessEmail != null && business.CustomerBusinessEmail.ToLower() == email)
                || (phone != null && (business.CustomerBusinessPhone == phone || business.CustomerBusinessPhone == internationalPhone))
                || (tax != "" && business.CustomerBusinessCompanyTaxIdentifier.Replace("-", "").Replace(" ", "") == tax)
            orderby business.CustomerBusinessLegalName, business.CustomerBusinessId
            select new { business, customer }).Take(50).ToListAsync(ct);

        var results = individuals.Select(c => new CustomerLookupResult(CustomerProfile(c), null, null)).ToList();
        results.AddRange(companies.Select(row => BusinessProfile(row.customer, row.business)));
        return results;
    }

    /// <summary>Bên A as the customer's legal entity: the entity's name, address, representative and tax code over the person's details.</summary>
    private static CustomerLookupResult BusinessProfile(Customer customer, CustomerBusiness b)
    {
        var contact = CustomerProfile(customer);
        contact.Fields["CUSTOMER_COMPANY"] = b.CustomerBusinessLegalName;
        contact.Fields["CUSTOMER_ADDRESS"] = !string.IsNullOrWhiteSpace(b.CustomerBusinessOfficeAddress)
            ? b.CustomerBusinessOfficeAddress
            : !string.IsNullOrWhiteSpace(b.CustomerBusinessHeadOfficeAddress)
                ? b.CustomerBusinessHeadOfficeAddress
                : (customer.CustomerAddress ?? "");
        contact.Fields["CUSTOMER_CONTACT_NAME"] = !string.IsNullOrWhiteSpace(b.CustomerBusinessRepresentativeName)
            ? b.CustomerBusinessRepresentativeName
            : customer.CustomerDisplayName;
        contact.Fields["CUSTOMER_CONTACT_PHONE"] = b.CustomerBusinessPhone ?? customer.CustomerPersonalPhone;
        contact.Fields["CUSTOMER_EMAIL"] = b.CustomerBusinessEmail ?? customer.CustomerEmail ?? "";
        contact.Fields["CUSTOMER_TAX_CODE"] = b.CustomerBusinessCompanyTaxIdentifier;
        return new CustomerLookupResult(contact, b.CustomerBusinessId, b.CustomerBusinessLegalName);
    }

    /// <summary>
    /// The customer's current details for Bên A of a contract (the person, or the chosen legal entity of that customer),
    /// so an editable version can be refilled from the customer record. Null when the customer is gone or locked.
    /// </summary>
    public async Task<CustomerLookupResult?> CustomerProfileFor(long customerId, long? businessId, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.CustomerId == customerId && c.CustomerStatus == 1, ct);
        if (customer is null) return null;
        if (businessId is null) return new CustomerLookupResult(CustomerProfile(customer), null, null);
        var business = await db.CustomerBusinesses.AsNoTracking().SingleOrDefaultAsync(b => b.CustomerBusinessId == businessId && b.CustomerId == customerId, ct);
        return business is null ? new CustomerLookupResult(CustomerProfile(customer), null, null) : BusinessProfile(customer, business);
    }

    public async Task<CustomerOption?> Customer(long id, CancellationToken ct)
    {
        var c = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.CustomerId == id && x.CustomerStatus == 1, ct);
        return c is null ? null : CustomerProfile(c);
    }

    private static CustomerOption CustomerProfile(Customer c) =>
        new(c.CustomerId, c.CustomerDisplayName, c.CustomerPersonalPhone, new()
        {
            ["CUSTOMER_CONTACT_NAME"] = c.CustomerDisplayName, ["CUSTOMER_CONTACT_PHONE"] = c.CustomerPersonalPhone,
            ["CUSTOMER_COMPANY"] = c.CustomerDisplayName,
            ["CUSTOMER_ADDRESS"] = c.CustomerAddress ?? "",
            ["CUSTOMER_EMAIL"] = c.CustomerEmail ?? "", ["CUSTOMER_TAX_CODE"] = ""
        });

    public async Task<CustomerOption> CreateCustomer(CustomerInput input, long actor, CancellationToken ct)
    {
        var name = InternalDraftPolicy.Text(input.Name, 200);
        var phone = PhoneNumber.Normalize(input.Phone);
        if (name.Length == 0 || phone is null) throw new ArgumentException("Nhập tên và số điện thoại Việt Nam hợp lệ.");
        var email = InternalDraftPolicy.Text(input.Email, 255);
        if (email.Length > 0 && !System.Net.Mail.MailAddress.TryCreate(email, out _)) throw new ArgumentException("Email không hợp lệ.");
        var company = InternalDraftPolicy.Text(input.Company, 1000);
        var tax = InternalDraftPolicy.Text(input.TaxCode, 20);
        if (input.PartyType is not (null or InternalDraftPolicy.Person or InternalDraftPolicy.Business))
            throw new ArgumentException("Chọn loại khách hàng: Cá nhân hoặc Doanh nghiệp.");
        if (input.PartyType == InternalDraftPolicy.Person && (company.Length > 0 || tax.Length > 0))
            throw new ArgumentException("Hồ sơ cá nhân không nhận thông tin pháp nhân khi tạo.");
        if (input.PartyType == InternalDraftPolicy.Business && (company.Length == 0 || tax.Length == 0))
            throw new ArgumentException("Nhập tên pháp lý và mã số thuế doanh nghiệp.");
        if (company.Length > 0 && tax.Length == 0) throw new ArgumentException("Thông tin doanh nghiệp cần mã số thuế.");
        if (await db.Customers.AnyAsync(c => c.CustomerPersonalPhone == phone || c.CustomerDisplayName == name || (email != "" && c.CustomerEmail == email), ct))
            throw new InternalAppException(409, "Khách hàng đã có trong hệ thống (trùng tên, SĐT hoặc email). Không thấy trong danh sách của bạn thì liên hệ Phòng Hành Chính - Pháp Chế để được chuyển khách.");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var customer = new Customer { CustomerDisplayName = name, CustomerPersonalPhone = phone, CustomerEmail = email.Length == 0 ? null : email,
                CustomerAddress = InternalDraftPolicy.Text(input.Address, 1000), CustomerStatus = 1, CreatedAt = DateTime.UtcNow, CreatedBy = actor,
                CustomerOwnerEmployeeId = actor };
            customer.CustomerPasswordHash = new PasswordHasher<Customer>().HashPassword(customer, Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            db.Customers.Add(customer);
            await db.SaveChangesAsync(ct);
            CustomerBusiness? business = null;
            if (company.Length > 0)
            {
                business = new CustomerBusiness { CustomerId = customer.CustomerId,
                    CustomerBusinessLegalName = company, CustomerBusinessCompanyTaxIdentifier = tax, CustomerBusinessOfficeAddress = customer.CustomerAddress,
                    CustomerBusinessRepresentativeName = input.PartyType == InternalDraftPolicy.Business ? name : null,
                    CreatedAt = DateTime.UtcNow, CreatedBy = actor };
                db.CustomerBusinesses.Add(business);
            }
            db.AuditLogs.Add(new AuditLog { ActionType = "CREATE", EntityName = "CUSTOMER", EntityId = customer.CustomerId.ToString(), ActorType = "EMPLOYEE", ActorId = actor, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return input.PartyType == InternalDraftPolicy.Business && business is not null
                ? BusinessProfile(customer, business).Customer with { BusinessId = business.CustomerBusinessId }
                : CustomerProfile(customer);
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
    public async Task<List<DraftSummary>> Drafts(long? owner, string? search, int? status, CancellationToken ct, string? partyType = null)
    {
        var party = InternalDraftPolicy.PartyTypeMarker(partyType);
        var query = from d in db.ContractDocuments.AsNoTracking() join e in db.Employees on d.EmployeeId equals e.EmployeeId
            join c in db.Customers on d.CustomerId equals c.CustomerId
            join v in db.ContractDynamicValues on d.ContractDocumentId equals v.ContractDocumentId
            where (!status.HasValue || d.ContractDocumentStatus == status) && d.TenantId == null && v.FieldKey == SnapshotKey && v.ContractVersionId == d.ContractDocumentCurrentVersionId
                && (!owner.HasValue || d.EmployeeId == owner) && (party == null || v.FieldValue.Contains(party))
                && (search == null || d.ContractDocumentName.Contains(search) || d.ContractDocumentCode!.Contains(search) || c.CustomerDisplayName.Contains(search))
            orderby d.UpdatedDate ?? d.CreatedDate descending
            select new { d, e.EmployeeDisplayName, c.CustomerDisplayName, v.FieldValue, Latest = db.ContractReviewRequests
                .Where(r => r.ContractDocumentId == d.ContractDocumentId).OrderByDescending(r => r.ContractReviewRequestId).Select(r => r.ContractReviewRequestStatus).FirstOrDefault() };
        return (await query.Take(200).ToListAsync(ct)).Select(x => { var content = JsonSerializer.Deserialize<DraftContent>(x.FieldValue, Json)!;
            if (x.d.ContractDocumentStatus == ContractDocumentStatus.Draft || (x.d.ContractDocumentStatus == ContractDocumentStatus.Negotiating && x.Latest is ContractReviewStatus.ChangesRequested or ContractReviewStatus.Rejected))
                DraftValidation.Recheck(content);
            return new DraftSummary(x.d.ContractDocumentId, x.d.ContractDocumentCode!, x.d.ContractDocumentName,
            x.CustomerDisplayName, x.d.EmployeeId, x.EmployeeDisplayName, x.d.UpdatedDate ?? x.d.CreatedDate,
            content.MissingFields.Count, x.d.ContractDocumentStatus, x.Latest, x.d.ContractTemplateId, x.d.ContractDocumentType); }).ToList();
    }
    /// <summary>Documents whose current version's snapshot is of the given party type (all when party is null).</summary>
    private IQueryable<ContractDocument> OfParty(string? party) => party == null ? db.ContractDocuments.AsNoTracking()
        : db.ContractDocuments.AsNoTracking().Where(d => db.ContractDynamicValues.Any(v => v.ContractDocumentId == d.ContractDocumentId
            && v.ContractVersionId == d.ContractDocumentCurrentVersionId && v.FieldKey == SnapshotKey && v.FieldValue.Contains(party)));
    public async Task<List<ContractArchiveEntry>> ContractArchive(string? search, int? status, CancellationToken ct, string? partyType = null,
        DateTime? createdFrom = null, DateTime? createdTo = null, int limit = 200)
    {
        var query = from d in OfParty(InternalDraftPolicy.PartyTypeMarker(partyType))
            join e in db.Employees.AsNoTracking() on d.EmployeeId equals e.EmployeeId
            join c in db.Customers.AsNoTracking() on d.CustomerId equals c.CustomerId
            join v in db.ContractVersions.AsNoTracking() on d.ContractDocumentCurrentVersionId equals v.ContractVersionId
            where d.TenantId == null && (!status.HasValue || d.ContractDocumentStatus == status)
                && (!createdFrom.HasValue || d.CreatedDate >= createdFrom.Value)
                && (!createdTo.HasValue || d.CreatedDate <= createdTo.Value)
                && (search == null || d.ContractDocumentName.Contains(search)
                    || (d.ContractDocumentCode != null && d.ContractDocumentCode.Contains(search))
                    || c.CustomerDisplayName.Contains(search))
            orderby d.UpdatedDate ?? d.CreatedDate descending
            select new { d, e.EmployeeDisplayName, c.CustomerDisplayName, v.ContractVersionId, v.ContractVersionVersionNo,
                v.ContractVersionIslocked, v.ContractVersionTotalamount, v.ContractVersionCurrencycode,
                v.ContractVersionEffectiveDate, v.ContractVersionExpireDate };
        var rows = await query.Take(Math.Clamp(limit, 1, 5000)).ToListAsync(ct);
        var ids = rows.Select(x => x.d.ContractDocumentId).ToArray();
        var signatures = await db.ContractElectronicSignatures.AsNoTracking()
            .Where(s => ids.Contains(s.ContractDocumentId))
            .GroupBy(s => s.ContractDocumentId)
            .Select(g => new { Id = g.Key, Count = g.Count(), LastSignedAt = g.Max(x => x.ContractElectronicSignedat) })
            .ToDictionaryAsync(x => x.Id, ct);
        // The schedule of each contract's current version: amounts, which were received (status 1) and when they fall due.
        var versionIds = rows.Select(x => x.ContractVersionId).ToArray();
        var schedule = (await db.ContractPaymentMilestones.AsNoTracking().Where(m => versionIds.Contains(m.ContractVersionId))
            .Select(m => new { m.ContractVersionId, Amount = m.ContractPaymentMilestoneAmount, Paid = m.ContractPaymentMilestonePaymentstatus == 1,
                PaidAt = m.ContractPaymentMilestonePaidat, Due = m.ContractPaymentMilestoneDuedate }).ToListAsync(ct)).ToLookup(m => m.ContractVersionId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        return rows.Select(x =>
        {
            signatures.TryGetValue(x.d.ContractDocumentId, out var signing);
            var items = schedule[x.ContractVersionId].ToList();
            var payments = items.Count == 0 ? ArchivePayments.None : new ArchivePayments(items.Count, items.Count(m => m.Paid),
                items.Sum(m => m.Amount), items.Where(m => m.Paid).Sum(m => m.Amount), items.Where(m => !m.Paid && m.Due < today).Sum(m => m.Amount),
                items.Where(m => !m.Paid && m.Due != null).Min(m => m.Due), items.Max(m => m.PaidAt),
                items.Where(m => m.Paid && m.PaidAt != null).Select(m => new ArchiveReceipt(m.Amount, m.PaidAt!.Value)).ToList());
            return new ContractArchiveEntry(x.d.ContractDocumentId, x.d.ContractDocumentCode ?? "",
                x.d.ContractDocumentName, x.CustomerDisplayName, x.d.EmployeeId, x.EmployeeDisplayName,
                x.d.CreatedDate, x.d.UpdatedDate ?? x.d.CreatedDate, x.d.ContractDocumentStatus, x.ContractVersionVersionNo,
                x.ContractVersionIslocked, signing?.Count ?? 0, signing?.LastSignedAt,
                x.ContractVersionTotalamount, x.ContractVersionCurrencycode,
                x.ContractVersionEffectiveDate, x.ContractVersionExpireDate) { Payments = payments };
        }).ToList();
    }
    public async Task<List<SigningMonitorEntry>> SigningMonitor(string? search, int? status, CancellationToken ct, string? partyType = null)
    {
        var docs = await (from d in OfParty(InternalDraftPolicy.PartyTypeMarker(partyType))
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
        var business = InternalDraftPolicy.PartyTypeMarker(InternalDraftPolicy.Business)!;
        var paper = (await db.ContractDynamicValues.AsNoTracking().Where(v => ids.Contains(v.ContractDocumentId) && versionIds.Contains(v.ContractVersionId)
                && v.FieldKey == SnapshotKey && v.FieldValue.Contains(business)).Select(v => new { v.ContractDocumentId, v.ContractVersionId }).ToListAsync(ct))
            .Where(v => currentVersions.GetValueOrDefault(v.ContractDocumentId) == v.ContractVersionId).Select(v => v.ContractDocumentId).ToHashSet();
        return docs.Select(x =>
        {
            ContractVersion? version = null;
            var hasVersion = x.d.ContractDocumentCurrentVersionId is long versionId && versions.TryGetValue(versionId, out version);
            currentSignatures.TryGetValue(x.d.ContractDocumentId, out var signing);
            reviews.TryGetValue(x.d.ContractDocumentId, out var review);
            return new SigningMonitorEntry(x.d.ContractDocumentId, x.d.ContractDocumentCode ?? "", x.d.ContractDocumentName,
                x.CustomerDisplayName, x.d.EmployeeId, x.EmployeeDisplayName, x.d.UpdatedDate ?? x.d.CreatedDate,
                x.d.ContractDocumentStatus, hasVersion ? version!.ContractVersionVersionNo : 0,
                signing?.Count ?? 0, signing?.Signed ?? 0, signing?.CompanySigned ?? false, signing?.CustomerSigned ?? false, review?.Status, signing?.Last, paper.Contains(x.d.ContractDocumentId));
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
    internal static bool ResolveOfficeOrigin(bool? snapshotOrigin, string? creationAudit)
    {
        if (snapshotOrigin is { } origin) return origin;
        if (string.IsNullOrWhiteSpace(creationAudit)) return false;
        try
        {
            using var json = JsonDocument.Parse(creationAudit);
            return json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("officeOrigin", out var source)
                && source.ValueKind == JsonValueKind.True;
        }
        catch (JsonException) { return false; }
    }

    internal static async Task<bool> IsOfficeOrigin(DraftContent content, long documentId, MasterDbContext db, CancellationToken ct)
    {
        if (content.OfficeOrigin is { } origin) return origin;
        var creationAudit = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == "CONTRACT_DOCUMENTS" && a.ActionType == "CREATE" && a.EntityId == documentId.ToString())
            .OrderBy(a => a.AuditLogId).Select(a => a.NewData).FirstOrDefaultAsync(ct);
        return ResolveOfficeOrigin(null, creationAudit);
    }

    internal static string? OfficeSendBlockedReason(DraftContent content, int status, string? latestReview, bool locked, string? phone, string? email)
    {
        if (ContractReviewPolicy.CanSend(status, latestReview, locked, 0) is { } stateReason) return stateReason;
        if (content.ExpireDate is null || content.EffectiveDate > content.ExpireDate) return "Kiểm tra thời hạn hợp đồng.";
        if (content.FormatVersion < 2 || content.TemplateSnapshot is null) return "Mở và lưu lại nháp trước khi gửi khách.";
        if (DraftValidation.SendIssues(content).Count > 0) return "Còn thiếu thông tin bắt buộc.";
        if (ReviewLink.ContactPhone(content.Fields, phone) is null) return "Khách hàng chưa có số điện thoại hợp lệ.";
        return ReviewLink.ContactEmail(content.Fields, email) is null ? "Nhập email khách hàng trong hợp đồng để gửi link xem." : null;
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
        if (content.TemplateSnapshot is { WordStorageKey: null, PrintFormat: null } legacyTemplate)
            content.TemplateSnapshot = legacyTemplate with { PrintFormat = await ContractFormatSettings.Read(db, content.TemplateId, ct) };
        if (ContractReviewPolicy.CanEdit(d.ContractDocumentStatus, v.ContractVersionIslocked)
            && content.TemplateSnapshot is { WordStorageKey: not null, PrintFormat: null } legacyWord)
        {
            var json = await db.ContractTemplates.Where(t => t.ContractTemplateId == content.TemplateId)
                .Select(t => t.PrintFormatJson).FirstOrDefaultAsync(ct);
            content.TemplateSnapshot = legacyWord with { PrintFormat = ContractFormatSettings.ForWord(json) };
        }
        content.OfficeOrigin = await IsOfficeOrigin(content, id, db, ct);
        if (!v.ContractVersionIslocked) DraftValidation.Recheck(content);
        var phone = ReviewLink.ContactPhone(content.Fields, customer.CustomerPersonalPhone);
        var internallyApproved = await db.ContractApprovalRequests.AnyAsync(a => a.ContractDocumentId == id && a.ContractVersionId == v.ContractVersionId && (a.InternalStage == "APPROVED" || a.InternalStage == "DIRECTOR"), ct);
        var blocked = content.OfficeOrigin == true
            ? OfficeSendBlockedReason(content, d.ContractDocumentStatus, latest, v.ContractVersionIslocked, customer.CustomerPersonalPhone, customer.CustomerEmail)
            : !internallyApproved ? "Cần Hành chính duyệt trước khi gửi khách." : ContractReviewPolicy.CanSend(d.ContractDocumentStatus == ContractDocumentStatus.PendingApproval ? ContractDocumentStatus.Draft : d.ContractDocumentStatus, latest, d.ContractDocumentStatus == ContractDocumentStatus.PendingApproval ? false : v.ContractVersionIslocked, content.MissingFields.Count)
            ?? (phone is null ? "Khách hàng chưa có số điện thoại hợp lệ." : null)
            ?? (ReviewLink.ContactEmail(content.Fields, customer.CustomerEmail) is null ? "Nhập email khách hàng trong hợp đồng để gửi link xem." : null);
        var carried = await (from comment in db.ContractNegotiationComments.AsNoTracking()
            join term in db.ContractTerms on (long?)comment.ContractNegotiationCommentTermid equals term.ContractTermTermid into terms
            from term in terms.DefaultIfEmpty()
            where comment.ContractDocumentId == id && comment.ContractVersionId == v.ContractVersionId && comment.ContractNegotiationCommentCarriedforwardfromcommentid != null
            select new CarriedDraftComment(comment.ContractNegotiationCommentId, term == null ? null : term.ContractTermTermcode, comment.ContractNegotiationCommentContent,
                comment.ContractNegotiationCommentCarriedforwardfromcommentid, comment.ContractNegotiationCommentCarriedforwardfromversionid, comment.ContractNegotiationCommentState == 1)).ToListAsync(ct);
        var paperEvidencePending = content.PartyType == "BUSINESS" && d.ContractDocumentStatus == ContractDocumentStatus.PendingSignature
            && await (from file in db.ContractDocumentFiles
                      join evidence in db.ContractSignedEvidences on file.ContractDocumentFileId equals evidence.ContractDocumentFileId
                      where file.ContractDocumentId == id && file.ContractVersionId == v.ContractVersionId
                          && file.ContractDocumentFileRole == ContractDocumentFileRoles.SignedPaper
                          && evidence.ContractSignedEvidenceStatus == (int)SignedEvidenceStatus.PendingReview
                      select evidence.ContractSignedEvidenceId).AnyAsync(ct);
        return new(id, d.ContractDocumentCode!, d.EmployeeId, name, Convert.ToBase64String(d.RowVersion), Convert.ToBase64String(v.ContractVersionRowversion),
            d.UpdatedDate ?? d.CreatedDate, content, d.ContractDocumentStatus, v.ContractVersionVersionNo, v.ContractVersionIslocked,
            ContractReviewPolicy.CanEdit(d.ContractDocumentStatus, v.ContractVersionIslocked), customer.CustomerDisplayName,
            PhoneNumber.Mask(phone ?? customer.CustomerPersonalPhone), latest, blocked, DraftValidation.Missing(content), carried,
            await ReviewData.HasAccount(db, d.CustomerId, ct), paperEvidencePending);
    }
    public async Task<DraftRecord> Save(long? id, SaveDraftRequest request, InternalUser actor, CancellationToken ct)
    {
        request.Content.UseTimesNewRoman = true;
        string? wordKey = null;
        byte[]? edited = null;
        try
        {
            if (request.WordPreviewToken is { } token)
            {
                var source = request.Content.TemplateSnapshot ?? throw new ArgumentException("Thiếu mẫu Word.");
                edited = word!.ReviewedDocument(token, actor.Id, source, id, request.Content);
                wordKey = Guid.NewGuid().ToString("N") + ".docx";
                var terms = InternalWordTemplates.Sections(InternalWordDocument.Blocks(edited));
                if (terms.Count > 100) throw new ArgumentException("Nội dung vượt quá giới hạn 100 điều khoản.");
                request.Content.Terms = terms;
                request.Content.TemplateSnapshot = source with { WordStorageKey = wordKey, WordSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(edited)), Terms = terms };
                DraftValidation.Recheck(request.Content);
                await storage.WriteAsync(Path.Combine(storage.Root, wordKey), edited, ct);
            }
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var now = DateTime.UtcNow;
                var content = request.Content;
                content.OfficeOrigin = actor.CanCheck && !actor.CanWrite;
                ContractDocument document;
                ContractVersion version;
                string? oldData = null;
                if (content.TemplateSnapshot is { WordStorageKey: null } template)
                    content.TemplateSnapshot = template with { PrintFormat = await ContractFormatSettings.Read(db, content.TemplateId, ct) };
                var serialized = JsonSerializer.Serialize(content, Json);
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
                    var savedContent = oldData == null ? null : JsonSerializer.Deserialize<DraftContent>(oldData, Json);
                    content.OfficeOrigin = savedContent is not null && await IsOfficeOrigin(savedContent, document.ContractDocumentId, db, ct);
                    if (savedContent?.TemplateSnapshot?.PrintFormat is { } frozen && savedContent.TemplateId == content.TemplateId && content.TemplateSnapshot is { } currentTemplate)
                        content.TemplateSnapshot = currentTemplate with { PrintFormat = frozen };
                    content.NumberingBusinessId = savedContent?.NumberingBusinessId;
                    content.ContractNumber = savedContent?.ContractNumber ?? (document.ContractDocumentCode?.StartsWith("HD-", StringComparison.Ordinal) == true
                        ? document.ContractDocumentCode : null);
                    serialized = JsonSerializer.Serialize(content, Json);
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
                        db.ContractPaymentMilestones.RemoveRange(await db.ContractPaymentMilestones.Where(x => x.ContractDocumentId == id && x.ContractVersionId == version.ContractVersionId).ToListAsync(ct));
                        await db.SaveChangesAsync(ct);
                        var previousTerms = await db.ContractTerms.Where(x => x.ContractDocumentId == id && x.ContractVersionId == version.ContractVersionId).ToListAsync(ct);
                        commentTerms = previousTerms.ToDictionary(t => t.ContractTermTermid, t => t.ContractTermTermcode);
                        pendingComments = await db.ContractNegotiationComments.Where(x => x.ContractDocumentId == id && x.ContractVersionId == version.ContractVersionId).ToListAsync(ct);
                        db.ContractTerms.RemoveRange(previousTerms);
                        if (contentChanged && ContractVersionNotes.IsWorking(version.ContractVersionChangenote))
                            version.ContractVersionChangenote = ContractVersionNotes.Display(version.ContractVersionChangenote);
                    }
                }
                else
                {
                    if (request.ParentDocumentId is long parentId) await CheckChildDocument(parentId, content, actor, ct);
                    // The contract register follows Vietnam's calendar, including the New Year boundary.
                    var date = now.AddHours(7);
                    var owner = content.NumberingBusinessId ?? 0;
                    if (content.NumberingBusinessId.HasValue)
                    {
                        if (owner <= 0 || owner != content.CustomerBusinessId || content.PartyType != "BUSINESS")
                            throw new InternalAppException(400, "Quy tắc không khớp pháp nhân đang chọn.");
                        var rules = new ContractNumberRules(db);
                        await rules.CheckOwner(owner, content.CustomerId, ct);
                        if (await rules.Read(owner, ct) == null) throw new InternalAppException(400, "Pháp nhân chưa thiết lập quy tắc.");
                    }
                    var numbers = await db.Database.SqlQuery<string>($"EXEC dbo.AllocateFormattedContractNumber @owner={owner}, @day={date.Date}").ToListAsync(ct);
                    var code = numbers.Single();
                    document = new ContractDocument { CustomerId = content.CustomerId, EmployeeId = actor.Id, ContractTemplateId = content.TemplateId,
                        ContractDocumentName = content.Name, ContractDocumentCode = code, ContractDocumentType = content.TemplateSnapshot?.DocumentType ?? 1,
                        ContractDocumentStatus = 0, CreatedDate = now };
                    db.ContractDocuments.Add(document);
                    await db.SaveChangesAsync(ct);
                    if (request.ParentDocumentId is long attachedTo)
                        db.ContractDocumentRelations.Add(new ContractDocumentRelation { ParentDocumentId = attachedTo, ChildDocumentId = document.ContractDocumentId,
                            ContractDocumentRelationType = ChildRelation, CreatedDate = now });
                    version = new ContractVersion { ContractDocumentId = document.ContractDocumentId, ContractTemplateId = content.TemplateId,
                        ContractVersionVersionNo = 1, ContractVersionCurrencycode = "VND", ContractVersionCreatedDate = now };
                    db.ContractVersions.Add(version);
                    await db.SaveChangesAsync(ct);
                    document.ContractDocumentCurrentVersionId = version.ContractVersionId;
                }
                if (wordKey != null) db.FileStorages.Add(new() { FileStorageObjecttype = "CONTRACT_VERSION", FileStorageObjectid = version.ContractVersionId,
                    FileStorageStoragekey = wordKey, FileStorageSha256 = content.TemplateSnapshot!.WordSha256!, FileStorageContenttype = InternalWordDocument.Mime,
                    FileStorageFilesize = edited!.Length, FileStorageFilename = "hop-dong.docx", FileStorageFilepath = wordKey, FileStorageFiletype = "DOCX",
                    FileStorageUploadedbyemployeeid = actor.Id, FileStorageUploadeddate = now });
                document.ContractDocumentName = content.Name;
                if (!id.HasValue) content.ContractNumber = document.ContractDocumentCode;
                serialized = JsonSerializer.Serialize(content, Json);
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
                var termMap = new Dictionary<string, ContractTerm>();
                for (var i = 0; i < content.Terms.Count; i++) { var term = content.Terms[i]; var entity = new ContractTerm {
                    ContractDocumentId = document.ContractDocumentId, ContractVersionId = version.ContractVersionId, ContractTermTermcode = term.Key,
                    ContractTermTermtitle = term.Title, ContractTermTermcontent = term.Content, ContractTermIsnegotiable = term.Editable,
                    ContractTermTermtitleen = term.TitleEn, ContractTermTermcontenten = term.ContentEn,
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
        finally
        {
            if (wordKey != null && !await db.FileStorages.AsNoTracking().AnyAsync(f => f.FileStorageStoragekey == wordKey, CancellationToken.None))
                File.Delete(Path.Combine(storage.Root, wordKey));
        }
    }

    private static bool SameDraftSnapshot(string current, string incoming)
    {
        try { return JsonNode.DeepEquals(JsonNode.Parse(current), JsonNode.Parse(incoming)); }
        catch (JsonException) { return string.Equals(current, incoming, StringComparison.Ordinal); }
    }
}

/// <summary>
/// Folder of templates and Word copies of drafts; the signing PDFs live in its sibling "signing" folder. Every file is
/// written and read through <see cref="WriteAsync"/> and <see cref="ReadAsync"/>, which encrypt at rest when a
/// <see cref="ContractFileCipher"/> is given (internal host) and pass bytes through otherwise (tests, legacy host).
/// </summary>
public sealed class InternalTemplateStorage(string root, ContractFileCipher? cipher = null)
{
    public string Root { get; } = Path.GetFullPath(root);

    /// <summary>Folder of the contract PDFs of each signing step.</summary>
    public string SigningRoot => Path.GetFullPath(Path.Combine(Root, "..", "signing"));

    public async Task WriteAsync(string path, byte[] plain, CancellationToken ct)
    {
        if (cipher is not null) { await cipher.WriteAsync(path, plain, ct); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, plain, ct);
    }

    public async Task<byte[]> ReadAsync(string path, CancellationToken ct) =>
        cipher is not null ? await cipher.ReadAsync(path, ct) : await File.ReadAllBytesAsync(path, ct);
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
        var bytes = await ReadAsync(Path.Combine(Root, file.FileStorageStoragekey), ct);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(file.FileStorageSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Template checksum mismatch.");
        return JsonSerializer.Deserialize<TemplateDefinition>(bytes, InternalRepository.Json) ?? throw new InvalidOperationException("Invalid template definition.");
    }
}
