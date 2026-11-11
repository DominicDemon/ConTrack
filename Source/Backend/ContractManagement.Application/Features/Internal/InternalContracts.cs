using ContractManagement.Domain.Internal;

namespace ContractManagement.Application.Features.Internal;

public sealed record InternalUser(long Id, string Name, string Username, string[] Permissions, bool MustChangePassword = false,
    string? Department = null, bool IsManager = false)
{
    public bool CanManageAll => Permissions.Contains("draft.manage.all");
    public bool CanWrite => Permissions.Contains("draft.write");
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
public sealed record CustomerOption(long Id, string Name, string Phone, Dictionary<string, string> Fields);
public sealed record CustomerInput(string Name, string Phone, string? Email, string? Company, string? TaxCode, string? Address);
public sealed record BusinessInput(string Name, string TaxCode, string Address, string Representative);
public sealed record BusinessOption(long Id, string Name, string TaxCode, string Address, string Representative);
public sealed record ProductOption(long Id, string Name, decimal Price);
public sealed record EmployeeOption(long Id, string Name);
public sealed record DraftSummary(long Id, string Code, string Name, string CustomerName, long EmployeeId, string EmployeeName, DateTime UpdatedAt, int MissingCount,
    int Status, string? LatestReviewStatus);
public sealed record ContractArchiveEntry(long Id, string Code, string Name, string CustomerName, long EmployeeId, string EmployeeName,
    DateTime UpdatedAt, int Status, long VersionNo, bool VersionLocked, int SignatureCount, DateTime? LastSignedAt);
public sealed record SigningMonitorEntry(long Id, string Code, string Name, string CustomerName, long EmployeeId, string EmployeeName,
    DateTime UpdatedAt, int Status, long VersionNo, int SignatureCount, int SignedCount, bool CompanySigned, bool CustomerSigned,
    string? LatestReviewStatus, DateTime? LastSignedAt);
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
    List<DraftIssue>? Issues = null, List<CarriedDraftComment>? CarriedComments = null);
public sealed record CarriedDraftComment(long Id, string? TermKey, string Content, long? SourceCommentId, long? SourceVersionId, bool Resolved);
public sealed record SaveDraftRequest(DraftContent Content, string? RowVersion, string? VersionRowVersion);
public sealed record SigningDemoDraftResult(DraftRecord Draft, string CustomerPortalPhone, string CustomerPortalPassword);
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
    Task<List<CustomerOption>> Customers(string? search, CancellationToken ct);
    Task<CustomerOption?> Customer(long id, CancellationToken ct);
    Task<CustomerOption> CreateCustomer(CustomerInput input, long actor, CancellationToken ct);
    Task<List<BusinessOption>> Businesses(long customerId, CancellationToken ct);
    Task<BusinessOption> CreateBusiness(long customerId, BusinessInput input, long actor, CancellationToken ct);
    Task<List<ProductOption>> Products(string? search, CancellationToken ct);
    Task<bool> ProductsExist(IEnumerable<long> ids, CancellationToken ct);
    Task<List<EmployeeOption>> Employees(CancellationToken ct);
    Task<List<DraftSummary>> Drafts(long? owner, string? search, int? status, CancellationToken ct);
    Task<List<ContractArchiveEntry>> ContractArchive(string? search, int? status, CancellationToken ct);
    Task<List<SigningMonitorEntry>> SigningMonitor(string? search, int? status, CancellationToken ct);
    Task<SigningMonitorDetail?> SigningMonitorDetail(long id, CancellationToken ct);
    Task<DraftRecord?> Draft(long id, CancellationToken ct);
    Task<DraftRecord> Save(long? id, SaveDraftRequest request, InternalUser actor, CancellationToken ct);
}

public sealed class InternalDraftService(IInternalRepository repository)
{
    public static void RequireAccess(InternalUser user, long owner)
    {
        if (!user.CanWrite || (!user.CanManageAll && user.Id != owner))
            throw new InternalAppException(403, "Bạn không có quyền thao tác bản nháp này.");
    }

    public async Task<DraftRecord> Get(long id, InternalUser user, CancellationToken ct)
    {
        var draft = await repository.Draft(id, ct) ?? throw new InternalAppException(404, "Không tìm thấy bản nháp.");
        RequireAccess(user, draft.EmployeeId);
        return draft;
    }

    public async Task<DraftRecord> Save(long? id, SaveDraftRequest request, InternalUser user, CancellationToken ct)
    {
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
        }
        else
        {
            template = (await repository.Templates(ct)).SingleOrDefault(t => t.Id == content.TemplateId)
                ?? throw new ArgumentException("Mẫu không tồn tại hoặc đã ngừng sử dụng.");
            var customer = await repository.Customer(content.CustomerId, ct) ?? throw new ArgumentException("Khách hàng không tồn tại.");
            // Initial snapshot can be edited by the author; later profile changes never refresh it.
            foreach (var pair in customer.Fields)
                if (!content.Fields.ContainsKey(pair.Key)) content.Fields[pair.Key] = pair.Value;
        }
        template = DraftCompletionPolicy.UpgradeTemplate(template);
        DraftValidation.Prepare(content, template);
        if (content.CustomerBusinessId.HasValue && !(await repository.Businesses(content.CustomerId, ct)).Any(b => b.Id == content.CustomerBusinessId))
            throw new ArgumentException("Pháp nhân không thuộc khách hàng đã chọn.");
        if (!await repository.ProductsExist(content.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value), ct))
            throw new ArgumentException("Sản phẩm được chọn không còn tồn tại.");
        return await repository.Save(id, request, user, ct);
    }
}
