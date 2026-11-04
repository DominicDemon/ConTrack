using ContractManagement.Domain.Internal;

namespace ContractManagement.Application.Features.Internal;

public sealed record InternalUser(long Id, string Name, string Username, string[] Permissions)
{
    public bool CanManageAll => Permissions.Contains("draft.manage.all");
    public bool CanWrite => Permissions.Contains("draft.write");
}
public sealed record CustomerOption(long Id, string Name, string Phone, Dictionary<string, string> Fields);
public sealed record CustomerInput(string Name, string Phone, string? Email, string? Company, string? TaxCode, string? Address);
public sealed record ProductOption(long Id, string Name, decimal Price);
public sealed record EmployeeOption(long Id, string Name);
public sealed record DraftSummary(long Id, string Code, string Name, string CustomerName, long EmployeeId, string EmployeeName, DateTime UpdatedAt, int MissingCount,
    int Status, string? LatestReviewStatus);
public sealed record DraftRecord(long Id, string Code, long EmployeeId, string EmployeeName, string RowVersion, string VersionRowVersion, DateTime UpdatedAt, DraftContent Content,
    int Status, long VersionNo, bool Locked, bool CanEdit, string CustomerName, string CustomerPhoneMasked, string? LatestReviewStatus, string? SendBlockedReason);
public sealed record SaveDraftRequest(DraftContent Content, string? RowVersion, string? VersionRowVersion);
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
    Task<List<ProductOption>> Products(string? search, CancellationToken ct);
    Task<bool> ProductsExist(IEnumerable<long> ids, CancellationToken ct);
    Task<List<EmployeeOption>> Employees(CancellationToken ct);
    Task<List<DraftSummary>> Drafts(long? owner, string? search, int? status, CancellationToken ct);
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
        InternalDraftPolicy.Prepare(content, template);
        if (!await repository.ProductsExist(content.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value), ct))
            throw new ArgumentException("Sản phẩm được chọn không còn tồn tại.");
        return await repository.Save(id, request, user, ct);
    }
}
