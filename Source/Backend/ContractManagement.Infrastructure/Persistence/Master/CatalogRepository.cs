using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed class CatalogRepository(MasterDbContext db) : ICatalogAdmin
{
    private const int CustomerActive = 1, CustomerLocked = 0;

    public async Task<List<CustomerRow>> Customers(string? search, CancellationToken ct)
    {
        var term = search?.Trim();
        var query = db.Customers.AsNoTracking();
        if (!string.IsNullOrEmpty(term)) query = query.Where(c => c.CustomerDisplayName.Contains(term) || c.CustomerPersonalPhone.Contains(term) || (c.CustomerEmail != null && c.CustomerEmail.Contains(term)));
        // shortcut: first 500 by name, no paging; add server paging when customer lists grow past that.
        return await query.OrderBy(c => c.CustomerDisplayName).Take(500)
            .Select(c => new CustomerRow(c.CustomerId, c.CustomerDisplayName, c.CustomerPersonalPhone, c.CustomerEmail, c.CustomerAddress, c.CustomerPersonalTaxIdentifier,
                c.CustomerStatus == CustomerActive, db.CustomerBusinesses.Count(b => b.CustomerId == c.CustomerId), c.CreatedAt, c.CustomerLastLoginAt))
            .ToListAsync(ct);
    }

    public async Task<CustomerRow> CustomerRow(long id, CancellationToken ct) =>
        await db.Customers.AsNoTracking().Where(c => c.CustomerId == id)
            .Select(c => new CustomerRow(c.CustomerId, c.CustomerDisplayName, c.CustomerPersonalPhone, c.CustomerEmail, c.CustomerAddress, c.CustomerPersonalTaxIdentifier,
                c.CustomerStatus == CustomerActive, db.CustomerBusinesses.Count(b => b.CustomerId == c.CustomerId), c.CreatedAt, c.CustomerLastLoginAt))
            .SingleOrDefaultAsync(ct) ?? throw new InternalAppException(404, "Không tìm thấy khách hàng.");

    public async Task<CustomerRow> UpdateCustomer(long id, CustomerUpdateInput input, InternalUser actor, CancellationToken ct)
    {
        var form = CatalogRules.Customer(input);
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.CustomerId == id, ct) ?? throw new InternalAppException(404, "Không tìm thấy khách hàng.");
        if (await db.Customers.AnyAsync(c => c.CustomerId != id && c.CustomerPersonalPhone == form.Phone, ct)) throw new InternalAppException(409, "Số điện thoại đã thuộc khách hàng khác.");
        if (await db.Customers.AnyAsync(c => c.CustomerId != id && c.CustomerDisplayName == form.Name, ct)) throw new InternalAppException(409, "Tên khách hàng đã tồn tại.");
        if (form.Email is not null && await db.Customers.AnyAsync(c => c.CustomerId != id && c.CustomerEmail == form.Email, ct)) throw new InternalAppException(409, "Email đã thuộc khách hàng khác.");
        var now = DateTime.UtcNow;
        // A new phone is not verified yet: the portal asks for it again before showing contracts.
        if (customer.CustomerPersonalPhone != form.Phone) customer.CustomerPersonalPhoneVerifiedAt = null;
        if (customer.CustomerEmail != form.Email) customer.CustomerEmailVerifiedAt = null;
        customer.CustomerDisplayName = form.Name!; customer.CustomerPersonalPhone = form.Phone!; customer.CustomerEmail = form.Email;
        customer.CustomerAddress = form.Address; customer.CustomerPersonalTaxIdentifier = form.TaxIdentifier;
        customer.UpdatedAt = now; customer.UpdatedBy = actor.Id;
        ReviewData.Audit(db, "UPDATE", "CUSTOMER", id, "EMPLOYEE", actor.Id, now);
        await db.SaveChangesAsync(ct);
        return await CustomerRow(id, ct);
    }

    public async Task<CustomerRow> SetCustomerStatus(long id, ActiveInput input, InternalUser actor, CancellationToken ct)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.CustomerId == id, ct) ?? throw new InternalAppException(404, "Không tìm thấy khách hàng.");
        var now = DateTime.UtcNow;
        customer.CustomerStatus = input.Active ? CustomerActive : CustomerLocked;
        customer.UpdatedAt = now; customer.UpdatedBy = actor.Id;
        ReviewData.Audit(db, input.Active ? "UNLOCK" : "LOCK", "CUSTOMER", id, "EMPLOYEE", actor.Id, now);
        await db.SaveChangesAsync(ct);
        return await CustomerRow(id, ct);
    }

    public async Task<List<ProductRow>> Products(string? search, CancellationToken ct)
    {
        var term = search?.Trim();
        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrEmpty(term)) query = query.Where(p => p.ProductName.Contains(term) || (p.ProductCode != null && p.ProductCode.Contains(term)));
        return await query.OrderBy(p => p.ProductSortorder ?? int.MaxValue).ThenBy(p => p.ProductName).Take(500)
            .Select(p => new ProductRow(p.ProductId, p.ProductCode, p.ProductName, p.ProductUnitname, p.CategoryId,
                db.Categories.Where(c => c.CategoryId == p.CategoryId).Select(c => c.CategoryName).FirstOrDefault(), p.ProductSuggestedPrice, p.ProductShortdesc))
            .ToListAsync(ct);
    }

    public Task<List<CategoryOption>> Categories(CancellationToken ct) =>
        db.Categories.AsNoTracking().OrderBy(c => c.CategorySortorder ?? int.MaxValue).ThenBy(c => c.CategoryName)
            .Select(c => new CategoryOption(c.CategoryId, c.CategoryName)).ToListAsync(ct);

    public Task<List<ServiceRow>> Services(CancellationToken ct) =>
        db.Services.AsNoTracking().OrderBy(s => s.ServiceName)
            .Select(s => new ServiceRow(s.ServiceId, s.ServiceName, s.ServiceUnitname, s.ServicePrice,
                db.ServiceTypes.Where(t => t.ServicetypeId == s.ServicetypeId).Select(t => t.ServicetypeName).FirstOrDefault(), s.ServiceStatus == 1))
            .ToListAsync(ct);

    public async Task<ProductRow> SaveProduct(long? id, ProductInput input, InternalUser actor, CancellationToken ct)
    {
        var form = CatalogRules.Product(input);
        if (form.CategoryId is { } category && !await db.Categories.AnyAsync(c => c.CategoryId == category, ct)) throw new ArgumentException("Danh mục không tồn tại.");
        if (form.Code is not null && await db.Products.AnyAsync(p => p.ProductCode == form.Code && p.ProductId != (id ?? 0), ct)) throw new InternalAppException(409, "Mã sản phẩm đã được dùng.");
        var product = id is null ? new Product { ProductCreatedAt = DateTime.UtcNow }
            : await db.Products.SingleOrDefaultAsync(p => p.ProductId == id, ct) ?? throw new InternalAppException(404, "Không tìm thấy sản phẩm.");
        product.ProductCode = form.Code; product.ProductName = form.Name!; product.ProductUnitname = form.Unit;
        product.CategoryId = form.CategoryId; product.ProductSuggestedPrice = form.Price; product.ProductShortdesc = form.Description;
        if (id is null) db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        ReviewData.Audit(db, id is null ? "CREATE" : "UPDATE", "PRODUCT", product.ProductId, "EMPLOYEE", actor.Id, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        return (await Products(null, ct)).Single(p => p.Id == product.ProductId);
    }

    public async Task<List<DepartmentRow>> Departments(CancellationToken ct)
    {
        var rows = await (from d in db.Departments.AsNoTracking()
            select new { d, Manager = db.Employees.Where(e => e.EmployeeId == d.DepartmentManagerEmployeeId).Select(e => e.EmployeeDisplayName).FirstOrDefault(),
                Members = db.EmployeeDepartments.Count(m => m.DepartmentId == d.DepartmentId),
                Staff = db.SystemRoles.Where(r => r.SystemRoleId == d.DepartmentStaffRoleId).Select(r => r.SystemRoleName).FirstOrDefault(),
                ManagerRole = db.SystemRoles.Where(r => r.SystemRoleId == d.DepartmentManagerRoleId).Select(r => r.SystemRoleName).FirstOrDefault() })
            .OrderBy(x => x.d.DepartmentId).ToListAsync(ct);
        return rows.Select(x => new DepartmentRow(x.d.DepartmentId, x.d.DepartmentCode, x.d.DepartmentName, x.d.DepartmentDescription, x.d.DepartmentStatus,
            x.Manager, x.Members, x.Staff, x.ManagerRole ?? "Chưa cấu hình")).ToList();
    }

    public async Task<DepartmentRow> UpdateDepartment(long id, DepartmentInput input, InternalUser actor, CancellationToken ct)
    {
        var form = CatalogRules.Department(input);
        var department = await db.Departments.SingleOrDefaultAsync(d => d.DepartmentId == id, ct) ?? throw new InternalAppException(404, "Không tìm thấy phòng ban.");
        if (await db.Departments.AnyAsync(d => d.DepartmentId != id && d.DepartmentName == form.Name, ct)) throw new InternalAppException(409, "Tên phòng ban đã tồn tại.");
        var now = DateTime.UtcNow;
        department.DepartmentName = form.Name!; department.DepartmentDescription = form.Description;
        department.UpdatedAt = now; department.UpdatedBy = actor.Id;
        ReviewData.Audit(db, "UPDATE", "DEPARTMENT", id, "EMPLOYEE", actor.Id, now);
        await db.SaveChangesAsync(ct);
        return (await Departments(ct)).Single(d => d.Id == id);
    }
}
