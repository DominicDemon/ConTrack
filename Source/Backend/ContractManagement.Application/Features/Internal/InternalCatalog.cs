using ContractManagement.Domain.Internal;

namespace ContractManagement.Application.Features.Internal;

public sealed record CustomerRow(long Id, string Name, string Phone, string? Email, string? Address, string? TaxIdentifier, bool Active,
    int BusinessCount, DateTime CreatedAt, DateTime? LastLoginAt);
public sealed record CustomerUpdateInput(string? Name, string? Phone, string? Email, string? Address, string? TaxIdentifier);
public sealed record ActiveInput(bool Active);
public sealed record ProductRow(long Id, string? Code, string Name, string? Unit, int? CategoryId, string? CategoryName, decimal? Price, string? Description);
public sealed record ProductInput(string? Code, string? Name, string? Unit, int? CategoryId, decimal? Price, string? Description);
public sealed record CategoryOption(int Id, string Name);
public sealed record ServiceRow(long Id, string Name, string? Unit, decimal? Price, string? TypeName, bool Active);
public sealed record DepartmentRow(long Id, string Code, string Name, string? Description, string Status, string? ManagerName, int MemberCount,
    string? StaffRoleName, string ManagerRoleName);
public sealed record DepartmentInput(string? Name, string? Description);

/// <summary>Khách hàng, Sản phẩm và dịch vụ, Phòng ban on MASTER. Each endpoint group checks its own permission.</summary>
public interface ICatalogAdmin
{
    Task<List<CustomerRow>> Customers(string? search, CancellationToken ct);
    Task<CustomerRow> UpdateCustomer(long id, CustomerUpdateInput input, InternalUser actor, CancellationToken ct);
    Task<CustomerRow> SetCustomerStatus(long id, ActiveInput input, InternalUser actor, CancellationToken ct);
    Task<CustomerRow> CustomerRow(long id, CancellationToken ct);
    Task<List<ProductRow>> Products(string? search, CancellationToken ct);
    Task<List<CategoryOption>> Categories(CancellationToken ct);
    Task<List<ServiceRow>> Services(CancellationToken ct);
    Task<ProductRow> SaveProduct(long? id, ProductInput input, InternalUser actor, CancellationToken ct);
    Task<List<DepartmentRow>> Departments(CancellationToken ct);
    Task<DepartmentRow> UpdateDepartment(long id, DepartmentInput input, InternalUser actor, CancellationToken ct);
}

public static class CatalogRules
{
    public const string CustomerManage = "customer.manage", ProductRead = "product.read", ProductManage = "product.manage", DepartmentManage = "department.manage";

    /// <returns>Trimmed text, null when blank; throws when longer than <paramref name="max"/>.</returns>
    public static string? Optional(string? value, int max, string label)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        return text.Length <= max ? text : throw new ArgumentException($"{label} tối đa {max} ký tự.");
    }

    public static string Required(string? value, int max, string label) =>
        Optional(value, max, label) ?? throw new ArgumentException($"{label} bắt buộc.");

    public static CustomerUpdateInput Customer(CustomerUpdateInput input)
    {
        var phone = PhoneNumber.Normalize(input.Phone) ?? throw new ArgumentException("Số điện thoại Việt Nam không hợp lệ.");
        var email = Optional(input.Email, 255, "Email")?.ToLowerInvariant();
        if (email is not null && !System.Net.Mail.MailAddress.TryCreate(email, out _)) throw new ArgumentException("Email không hợp lệ.");
        var tax = Optional(input.TaxIdentifier, 20, "Mã số thuế cá nhân");
        if (tax is not null && !tax.All(char.IsLetterOrDigit)) throw new ArgumentException("Mã số thuế chỉ gồm chữ và số.");
        return new(Required(input.Name, 200, "Tên khách hàng"), phone, email, Optional(input.Address, 1000, "Địa chỉ"), tax);
    }

    public static ProductInput Product(ProductInput input)
    {
        if (input.Price is < 0 or > 1_000_000_000_000m) throw new ArgumentException("Giá phải từ 0 đến 1.000 tỷ.");
        var code = Optional(input.Code, 20, "Mã sản phẩm")?.ToUpperInvariant();
        return new(code, Required(input.Name, 500, "Tên sản phẩm"), Optional(input.Unit, 100, "Đơn vị tính"), input.CategoryId, input.Price,
            Optional(input.Description, 2000, "Mô tả"));
    }

    public static DepartmentInput Department(DepartmentInput input) =>
        new(Required(input.Name, 500, "Tên phòng ban"), Optional(input.Description, 2000, "Mô tả"));
}
