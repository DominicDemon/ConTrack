using ContractManagement.Domain.Internal;

namespace ContractManagement.Application.Features.Internal;

public sealed record InternalUser(long Id, string Name, string Username, string[] Permissions, bool MustChangePassword = false,
    string? Department = null, bool IsManager = false)
{
    public bool CanManageLegal => Permissions.Contains("legal.manage");
    public bool CanReviewLegal => CanCheck || CanManageLegal;
    public bool CanManageContractWorkflow => CanManageAll || CanManageLegal;
    public bool CanAccessCompanySigning => CanManageContractWorkflow || CanSignCompany;
    public bool CanSignCompany => CanApprove;
    public bool CanCheck => Permissions.Contains("contract.check");
    public bool CanApprove => Permissions.Contains("contract.approve");
    public bool CanManageTemplates => Permissions.Contains("template.manage");
    public bool CanManageAll => Permissions.Contains("draft.manage.all") || CanCheck || CanApprove;
    public bool CanWrite => Permissions.Contains("draft.write");
    public bool CanEditDraftTerms(long ownerId) => CanCheck || CanWrite && Id == ownerId;
    public bool CanManageEmployees => Permissions.Contains(EmployeeAdminRules.ManagePermission);
}
public sealed record EmployeeAdminRow(long Id, string Code, string Username, string Name, string? Email, string? Phone, string Status,
    bool MustChangePassword, DateTime? LastLoginAt, long? DepartmentId, bool IsManager);
/// <summary>A department and the screens its staff / its manager get. StaffPermissions is null when the department has managers only (Ban Giám Đốc).</summary>
public sealed record DepartmentOption(long Id, string Code, string Name, string[]? StaffPermissions, string[] ManagerPermissions, long? ManagerId, string? ManagerName);
public sealed record CreateEmployeeInput(string? Code, string? Username, string? Name, string? Email, string? Phone, long? DepartmentId, bool Manager);
public sealed record EmployeePositionInput(long? DepartmentId, bool Manager);
public sealed record EmployeeStatusInput(bool Active);
public sealed record EmployeeChangePasswordInput(string? CurrentPassword, string? NewPassword);
/// <summary>The temporary password is returned once and never stored in clear text.</summary>
public sealed record TemporaryPasswordResult(EmployeeAdminRow Employee, string TemporaryPassword);

public interface IEmployeeAdmin
{
    Task<List<EmployeeAdminRow>> Employees(CancellationToken ct);
    Task<List<DepartmentOption>> Departments(CancellationToken ct);
    Task<TemporaryPasswordResult> Create(CreateEmployeeInput input, InternalUser actor, CancellationToken ct);
    Task<EmployeeAdminRow> SetPosition(long id, EmployeePositionInput input, InternalUser actor, CancellationToken ct);
    Task<EmployeeAdminRow> SetStatus(long id, EmployeeStatusInput input, InternalUser actor, CancellationToken ct);
    Task<TemporaryPasswordResult> ResetPassword(long id, InternalUser actor, CancellationToken ct);
    Task ChangePassword(long employeeId, EmployeeChangePasswordInput input, CancellationToken ct);
}

public static class EmployeeAdminRules
{
    public const string ManagePermission = "employee.manage";
    public const string Active = "ACTIVE", Locked = "LOCKED";
    public const string PasswordMessage = "Mật khẩu mới cần tối thiểu 8 ký tự, có cả chữ và số.";
    private static readonly System.Text.RegularExpressions.Regex UsernamePattern = new("^[a-z0-9._-]{3,100}$");
    private static readonly System.Text.RegularExpressions.Regex CodePattern = new("^[A-Z0-9_-]{1,30}$");

    public static bool IsValidPassword(string? password) =>
        password is { Length: >= 8 and <= 100 } && password.Any(char.IsLetter) && password.Any(char.IsDigit);

    /// <summary>Normalizes and validates the form; throws ArgumentException with a message the admin can act on.</summary>
    public static CreateEmployeeInput Normalize(CreateEmployeeInput input)
    {
        var code = input.Code?.Trim().ToUpperInvariant() ?? "";
        var username = input.Username?.Trim().ToLowerInvariant() ?? "";
        var name = input.Name?.Trim() ?? "";
        var email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim().ToLowerInvariant();
        var phone = string.IsNullOrWhiteSpace(input.Phone) ? null : PhoneNumber.Normalize(input.Phone) ?? throw new ArgumentException("Số điện thoại không hợp lệ.");
        if (!CodePattern.IsMatch(code)) throw new ArgumentException("Mã nhân viên gồm 1–30 ký tự chữ in hoa, số, gạch dưới hoặc gạch ngang.");
        if (!UsernamePattern.IsMatch(username)) throw new ArgumentException("Tên đăng nhập gồm 3–100 ký tự chữ thường, số, dấu chấm, gạch dưới hoặc gạch ngang.");
        if (name.Length is 0 or > 200) throw new ArgumentException("Họ tên bắt buộc, tối đa 200 ký tự.");
        if (email is not null && (email.Length > 100 || !System.Net.Mail.MailAddress.TryCreate(email, out _))) throw new ArgumentException("Email không hợp lệ.");
        return new(code, username, name, email, phone, DepartmentId(input.DepartmentId), input.Manager);
    }

    public static long DepartmentId(long? id) => id is > 0 ? id.Value : throw new ArgumentException("Chọn phòng ban.");

    /// <summary>Admins act on other accounts only, so the acting admin always keeps access and at least one admin remains.</summary>
    public static void RequireOther(InternalUser actor, long target)
    {
        if (actor.Id == target) throw new InternalAppException(409, "Không thể tự đổi phòng ban, khóa hoặc đặt lại mật khẩu cho chính mình.");
    }

    public static string TemporaryPassword() =>
        System.Security.Cryptography.RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz", 8)
        + System.Security.Cryptography.RandomNumberGenerator.GetString("23456789", 4);
}
public sealed record CustomerLookupResult(CustomerOption Customer, long? BusinessId, string? BusinessName);
public sealed record CustomerOption(long Id, string Name, string Phone, Dictionary<string, string> Fields, long? BusinessId = null);
public sealed record CustomerInput(string Name, string Phone, string? Email, string? Company, string? TaxCode, string? Address, string? PartyType = null);
public sealed record BusinessInput(string Name, string TaxCode, string Address, string Representative);
public sealed record BusinessOption(long Id, string Name, string TaxCode, string Address, string Representative);
public sealed record ProductOption(long Id, string Name, decimal Price);
public sealed record EmployeeOption(long Id, string Name);
public sealed record DraftSummary(long Id, string Code, string Name, string CustomerName, long EmployeeId, string EmployeeName, DateTime UpdatedAt, int MissingCount,
    int Status, string? LatestReviewStatus, long TemplateId = 0, int DocumentType = 1);
public sealed record ContractArchiveEntry(long Id, string Code, string Name, string CustomerName, long EmployeeId, string EmployeeName,
    DateTime CreatedAt, DateTime UpdatedAt, int Status, long VersionNo, bool VersionLocked, int SignatureCount, DateTime? LastSignedAt,
    decimal TotalAmount, string CurrencyCode, DateTime? EffectiveDate, DateTime? ExpireDate);
public sealed record SigningMonitorEntry(long Id, string Code, string Name, string CustomerName, long EmployeeId, string EmployeeName,
    DateTime UpdatedAt, int Status, long VersionNo, int SignatureCount, int SignedCount, bool CompanySigned, bool CustomerSigned,
    string? LatestReviewStatus, DateTime? LastSignedAt, bool Paper = false);
public sealed record SigningTimelineEvent(DateTime At, string Category, string Title, string? Detail, string State);
public sealed record SigningMonitorDetail(long Id, string Code, string Name, string CustomerName, string EmployeeName,
    DateTime CreatedAt, DateTime UpdatedAt, int Status, long VersionNo, bool VersionLocked, string? SnapshotHash,
    IReadOnlyList<SigningTimelineEvent> Timeline, IReadOnlyList<SigningSignerProgress> Signers,
    IReadOnlyList<SigningArtifactInfo> Artifacts);
public sealed record SigningSignerProgress(long Id, long VersionNo, string Party, string Role, string Name, string? Title, string? Email,
    int Status, DateTime? SignedAt, string? DocumentHash, string? RejectReason);
public sealed record SigningArtifactInfo(string FileName, string ContentType, long Size, string Sha256, int Role, bool Primary, DateTime AddedAt);
public sealed record DraftRecord(long Id, string Code, long EmployeeId, string EmployeeName, string RowVersion, string VersionRowVersion, DateTime UpdatedAt, DraftContent Content,
    int Status, long VersionNo, bool Locked, bool CanEdit, string CustomerName, string CustomerPhoneMasked, string? LatestReviewStatus, string? SendBlockedReason,
    List<DraftIssue>? Issues = null, List<CarriedDraftComment>? CarriedComments = null, bool CustomerHasAccount = false, bool PaperEvidencePending = false);
/// <summary>One row of the template library (Tạo mẫu văn bản): every type, hidden ones included.</summary>
public sealed record TemplateSummary(long Id, string Name, int DocumentType, long? ParentTemplateId, string? ParentName, bool Active,
    bool Word, int ChildCount, int DocumentCount, DateTime CreatedAt, DateTime? UpdatedAt, List<TemplateFieldOption>? Fields = null);
/// <summary>A field a document made from the template fills in; Required = it must be filled before sending.</summary>
public sealed record TemplateFieldOption(string Key, string Label, bool Required);
/// <param name="RequiredFields">Keys of the template's fields that must be filled before sending; null leaves them as they are.</param>
public sealed record TemplateInfoInput(string? Name, int DocumentType, long? ParentTemplateId, List<string>? RequiredFields = null);
public sealed record CarriedDraftComment(long Id, string? TermKey, string Content, long? SourceCommentId, long? SourceVersionId, bool Resolved);
/// <param name="ParentDocumentId">On create: attach the new document (appendix, handover record…) to this contract.</param>
public sealed record SaveDraftRequest(DraftContent Content, string? RowVersion, string? VersionRowVersion, string? WordPreviewToken = null, long? ParentDocumentId = null);
public sealed record RelatedDocument(long Id, string? Code, string Name, int DocumentType, int Status, long TemplateId, DateTime CreatedAt);
public sealed record ChildTemplateOption(long Id, string Name, int DocumentType);
/// <summary>A contract and the documents attached to it: its parent (for a child), its children, and the child templates of its template.</summary>
public sealed record DraftRelations(RelatedDocument? Parent, List<RelatedDocument> Children, List<ChildTemplateOption> ChildTemplates);
public sealed class InternalAppException(int status, string message, string? code = null) : Exception(message)
{
    public int Status { get; } = status;
    public string? Code { get; } = code;
}

public interface IInternalRepository
{
    Task<InternalUser?> Authenticate(string username, string password, CancellationToken ct);
    Task<InternalUser?> GetUser(long id, CancellationToken ct);
    Task<List<TemplateDefinition>> Templates(CancellationToken ct);
    Task<List<CustomerOption>> Customers(string? search, CancellationToken ct, long? owner = null);
    Task<CustomerOption?> Customer(long id, CancellationToken ct);
    Task<CustomerOption> CreateCustomer(CustomerInput input, long actor, CancellationToken ct);
    Task<List<BusinessOption>> Businesses(long customerId, CancellationToken ct);
    Task<BusinessOption> CreateBusiness(long customerId, BusinessInput input, long actor, CancellationToken ct);
    Task<List<ProductOption>> Products(string? search, CancellationToken ct);
    Task<bool> ProductsExist(IEnumerable<long> ids, CancellationToken ct);
    Task<List<EmployeeOption>> Employees(CancellationToken ct);
    /// <param name="partyType">PERSON or BUSINESS (the contract's party in its current version); null for both.</param>
    Task<List<DraftSummary>> Drafts(long? owner, string? search, int? status, CancellationToken ct, string? partyType = null);
    Task<List<ContractArchiveEntry>> ContractArchive(string? search, int? status, CancellationToken ct, string? partyType = null,
        DateTime? createdFrom = null, DateTime? createdTo = null, int limit = 200);
    Task<List<SigningMonitorEntry>> SigningMonitor(string? search, int? status, CancellationToken ct, string? partyType = null);
    Task<SigningMonitorDetail?> SigningMonitorDetail(long id, CancellationToken ct);
    Task<DraftRecord?> Draft(long id, CancellationToken ct);
    Task<DraftRecord> Save(long? id, SaveDraftRequest request, InternalUser actor, CancellationToken ct);
    /// <summary>The contract a document is attached to, if any.</summary>
    Task<long?> ParentDocument(long id, CancellationToken ct) => Task.FromResult<long?>(null);
}

public sealed class InternalDraftService(IInternalRepository repository)
{
    public static void RequireAccess(InternalUser user, long owner)
    {
        if ((!user.CanWrite && !user.CanCheck && !user.CanApprove && !user.CanManageLegal) || (!user.CanManageContractWorkflow && user.Id != owner))
            throw new InternalAppException(403, "Bạn không có quyền thao tác bản nháp này.");
    }

    public async Task<DraftRecord> Get(long id, InternalUser user, CancellationToken ct)
    {
        var draft = await repository.Draft(id, ct) ?? throw new InternalAppException(404, "Không tìm thấy bản nháp.");
        RequireAccess(user, draft.EmployeeId);
        // A contract Hành chính created goes to the customer from that Hành chính employee; any other from Sale.
        var blocked = draft.Content.OfficeOrigin == true
            ? user.CanCheck && user.Id == draft.EmployeeId ? draft.SendBlockedReason : "Chỉ Hành chính phụ trách hợp đồng này được gửi khách."
            : user.CanWrite ? draft.SendBlockedReason : "Chỉ kinh doanh được gửi khách.";
        return draft with { CanEdit = draft.CanEdit && (user.CanWrite || user.CanCheck), SendBlockedReason = blocked };
    }

    public async Task<DraftRecord> Save(long? id, SaveDraftRequest request, InternalUser user, CancellationToken ct)
    {
        if (!user.CanWrite && !user.CanCheck) throw new InternalAppException(403, "Bạn không có quyền sửa hợp đồng.");
        RequireAccess(user, user.Id);
        var content = request.Content ?? throw new ArgumentException("Thiếu nội dung bản nháp.");
        if (content.Fields is null || content.Terms is null || content.Items is null)
            throw new ArgumentException("Dữ liệu trường, điều khoản hoặc hạng mục không hợp lệ.");
        TemplateDefinition template;
        if (id.HasValue)
        {
            var old = await Get(id.Value, user, ct);
            if (old.Content.TemplateId != content.TemplateId || old.Content.CustomerId != content.CustomerId)
                throw new ArgumentException("Không đổi mẫu hoặc khách hàng sau khi tạo nháp; hãy tạo nháp mới.");
            template = old.Content.TemplateSnapshot ?? throw new InternalAppException(409, "Nháp thiếu bản chụp mẫu.");
            if (request.WordPreviewToken != null && !user.CanEditDraftTerms(old.EmployeeId)) throw new InternalAppException(403, "Bạn không có quyền sửa Word.");
            if ((!user.CanEditDraftTerms(old.EmployeeId) || template.WordStorageKey is not null) && System.Text.Json.JsonSerializer.Serialize(old.Content.Terms) != System.Text.Json.JsonSerializer.Serialize(content.Terms))
                throw new InternalAppException(403, "Bạn không thể sửa điều khoản qua thao tác này. Với mẫu Word, hãy mở trình sửa Word của nháp.");
        }
        else
        {
            template = (await repository.Templates(ct)).SingleOrDefault(t => t.Id == content.TemplateId)
                ?? throw new ArgumentException("Mẫu không tồn tại hoặc đã ngừng sử dụng.");
            var customer = await repository.Customer(content.CustomerId, ct) ?? throw new ArgumentException("Khách hàng không tồn tại.");
            if (template.WordStorageKey is not null) content.Terms = template.Terms.Select(t => t with { }).ToList();
            // Initial snapshot can be edited by the author; later profile changes never refresh it.
            foreach (var pair in customer.Fields)
                if (!content.Fields.ContainsKey(pair.Key)) content.Fields[pair.Key] = pair.Value;
        }
        template = DraftCompletionPolicy.UpgradeTemplate(template);
        // Keys the template does not have (company defaults an editor filled in, an older client) are dropped, not refused:
        // they are never printed, and refusing them blocked a plain re-save.
        var known = template.Fields.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var key in content.Fields.Keys.Where(k => !known.Contains(k)).ToList()) content.Fields.Remove(key);
        content.FieldsEn = content.FieldsEn?.Where(p => known.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);
        if (template.WordStorageKey is null)
            template = template with { Terms = content.Terms.Select(t => t with { Editable = true }).ToList() };
        DraftValidation.RequireContractDates(content, template);
        if (DocumentRules.For(template.DocumentType).ParentMilestone)
        {
            // A payment request names one instalment of its contract; the flag is the server's, never the client's.
            var parentId = id.HasValue ? await repository.ParentDocument(id.Value, ct) : request.ParentDocumentId;
            var milestones = parentId is long p ? (await repository.Draft(p, ct))?.Content.Milestones ?? [] : [];
            content.ParentMilestoneRequired = milestones.Count > 0 ? true : null;
            if (milestones.Count == 0) content.ParentMilestoneKey = null;
            else if (!string.IsNullOrWhiteSpace(content.ParentMilestoneKey) && milestones.All(m => m.Key != content.ParentMilestoneKey))
                throw new ArgumentException("Đợt thanh toán không thuộc hợp đồng này.");
        }
        DraftValidation.Prepare(content, template);
        if (content.CustomerBusinessId.HasValue && !(await repository.Businesses(content.CustomerId, ct)).Any(b => b.Id == content.CustomerBusinessId))
            throw new ArgumentException("Pháp nhân không thuộc khách hàng đã chọn.");
        if (!await repository.ProductsExist(content.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value), ct))
            throw new ArgumentException("Sản phẩm được chọn không còn tồn tại.");
        return await repository.Save(id, request, user, ct);
    }
}
