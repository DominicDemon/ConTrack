using System.Diagnostics.CodeAnalysis;
using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.Services.Security;
using ContractManagement.Domains.Controllers.Authentication;
using ContractManagement.Domains.Controllers.SystemAuth;
using ContractManagement.Domains.DTOs.Requests.Authentication;
using ContractManagement.Domains.DTOs.Requests.SystemAuth;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Models;
using ContractManagement.Infrastructure.MultiTenancy.Services;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using ContractManagement.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace ContractManagement.Tests.Domains.Security;

public sealed class LoginProtectionTests
{
    private const string CorrectPassword = "Password123!";

    [Fact]
    public void Throttle_LocksAfterMaxFailures_ResetClears_AndLockExpires()
    {
        var clock = new ManualClock();
        var throttle = new LoginThrottle(new MemoryCache(new MemoryCacheOptions()), clock);
        var key = LoginThrottle.EmployeeKey(1, " Alice ");
        Assert.Equal(key, LoginThrottle.EmployeeKey(1, "alice"));
        Assert.NotEqual(key, LoginThrottle.EmployeeKey(2, "alice")); // tách theo tenant

        for (var i = 1; i < LoginThrottle.MaxFailedAttempts; i++) Assert.False(throttle.RecordFailure(key));
        Assert.Null(throttle.GetLockoutRemaining(key));
        throttle.Reset(key); // đăng nhập đúng xóa bộ đếm
        for (var i = 1; i < LoginThrottle.MaxFailedAttempts; i++) throttle.RecordFailure(key);
        Assert.True(throttle.RecordFailure(key));
        Assert.Equal(LoginThrottle.LockoutDuration, throttle.GetLockoutRemaining(key));

        clock.Advance(LoginThrottle.LockoutDuration);
        Assert.Null(throttle.GetLockoutRemaining(key));
        Assert.False(throttle.RecordFailure(key)); // hết khóa thì đếm lại từ đầu
    }

    [Fact]
    public async Task EmployeeLogin_LocksAfterFiveWrongPasswords_EvenForCorrectPassword_AndAuditsFailures()
    {
        await using var db = CreateTenantDb();
        var hasher = new PasswordHasher<TblEmployee>();
        var employee = new TblEmployee
        {
            EmployeeId = 1, EmployeeAccount = "manager", EmployeeFullName = "Manager",
            EmployeeType = (byte)EmployeeType.Manager, Status = 1, SessionVersion = 1
        };
        employee.EmployeePassword = hasher.HashPassword(employee, CorrectPassword);
        db.TblEmployees.Add(employee);
        await db.SaveChangesAsync();
        var throttle = new LoginThrottle(new MemoryCache(new MemoryCacheOptions()), TimeProvider.System);
        var controller = EmployeeController(db, hasher, throttle);

        for (var i = 0; i < LoginThrottle.MaxFailedAttempts; i++)
            Assert.IsType<UnauthorizedObjectResult>(await controller.Login("tenant-a", Employee("manager", "wrong")));

        var locked = Assert.IsType<ObjectResult>(await controller.Login("tenant-a", Employee("manager", CorrectPassword)));
        Assert.Equal(429, locked.StatusCode);
        Assert.Equal(LoginRateLimit.LockedCode, Assert.IsType<AuthorizationErrorResponse>(locked.Value).Code);
        Assert.False(string.IsNullOrEmpty(controller.Response.Headers.RetryAfter));

        var audits = await db.TblAuthorizationAudits.ToListAsync();
        Assert.Equal(LoginThrottle.MaxFailedAttempts + 1, audits.Count);
        Assert.All(audits, a => Assert.Equal((AuthorizationAuditActionTypes.EmployeeLogin, AuthorizationAuditResultTypes.Denied, "manager"),
            (a.Action, a.Result, a.TargetId)));
        Assert.Equal(2, audits.Count(a => a.FailureCode == LoginRateLimit.LockedCode)); // lần thứ 5 (bị khóa) + lần bị chặn
        Assert.DoesNotContain(audits, a => (a.TargetId + a.FailureCode + a.ChangedFields).Contains("wrong"));
    }

    [Fact]
    public async Task EmployeeLogin_UnknownAccount_IsLockedTheSameWay_AndSuccessResetsCounter()
    {
        await using var db = CreateTenantDb();
        var hasher = new PasswordHasher<TblEmployee>();
        var employee = new TblEmployee
        {
            EmployeeId = 1, EmployeeAccount = "manager", EmployeeFullName = "Manager",
            EmployeeType = (byte)EmployeeType.Manager, Status = 1, SessionVersion = 1
        };
        employee.EmployeePassword = hasher.HashPassword(employee, CorrectPassword);
        db.TblEmployees.Add(employee);
        await db.SaveChangesAsync();
        var controller = EmployeeController(db, hasher, new LoginThrottle(new MemoryCache(new MemoryCacheOptions()), TimeProvider.System));

        // Tài khoản không tồn tại cũng bị khóa → không dùng việc khóa để dò tài khoản.
        for (var i = 0; i < LoginThrottle.MaxFailedAttempts; i++)
            Assert.IsType<UnauthorizedObjectResult>(await controller.Login("tenant-a", Employee("ghost", "wrong")));
        Assert.Equal(429, Assert.IsType<ObjectResult>(await controller.Login("tenant-a", Employee("ghost", "wrong"))).StatusCode);

        // Sai 4 lần rồi đúng → bộ đếm về 0, sai tiếp 4 lần vẫn chưa bị khóa.
        for (var i = 1; i < LoginThrottle.MaxFailedAttempts; i++) await controller.Login("tenant-a", Employee("manager", "wrong"));
        Assert.IsType<OkObjectResult>(await controller.Login("tenant-a", Employee("manager", CorrectPassword)));
        for (var i = 1; i < LoginThrottle.MaxFailedAttempts; i++) await controller.Login("tenant-a", Employee("manager", "wrong"));
        Assert.IsType<OkObjectResult>(await controller.Login("tenant-a", Employee("manager", CorrectPassword)));
    }

    [Fact]
    public async Task SystemAdminLogin_LocksAfterFiveWrongPasswords()
    {
        await using var central = new CentralDbContext(new DbContextOptionsBuilder<CentralDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var hasher = new PasswordHasher<SystemAdmin>();
        var admin = new SystemAdmin { SystemAdminId = 1, Username = "root", FullName = "Root", IsActive = true, CreatedAt = DateTime.UtcNow };
        admin.PasswordHash = hasher.HashPassword(admin, CorrectPassword);
        central.SystemAdmins.Add(admin);
        await central.SaveChangesAsync();
        var controller = new SystemAuthController(central, hasher,
            new CentralSecurityAuditWriter(central, NullLogger<CentralSecurityAuditWriter>.Instance),
            loginThrottle: new LoginThrottle(new MemoryCache(new MemoryCacheOptions()), TimeProvider.System))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Session = new TestSession() } }
        };

        for (var i = 0; i < LoginThrottle.MaxFailedAttempts; i++)
            Assert.IsType<UnauthorizedObjectResult>(await controller.Login(new SystemAdminLoginRequest { Username = "root", Password = "wrong" }, default));
        var locked = Assert.IsType<ObjectResult>(await controller.Login(
            new SystemAdminLoginRequest { Username = "ROOT", Password = CorrectPassword }, default));

        Assert.Equal(429, locked.StatusCode);
        Assert.Contains(await central.SecurityAudits.ToListAsync(), a => a.FailureCode == LoginRateLimit.LockedCode);
    }

    [Fact]
    public void LoginEndpoints_UseIpRateLimitPolicy()
    {
        foreach (var method in new[] { typeof(AuthController).GetMethod(nameof(AuthController.Login))!, typeof(SystemAuthController).GetMethod(nameof(SystemAuthController.Login))! })
        {
            var attribute = Assert.Single(method.GetCustomAttributes(typeof(EnableRateLimitingAttribute), false));
            Assert.Equal(LoginRateLimit.PolicyName, ((EnableRateLimitingAttribute)attribute).PolicyName);
        }
    }

    private static LoginRequest Employee(string account, string password) => new() { AccountName = account, Password = password };

    private static DbDtctechContext CreateTenantDb() => new(new DbContextOptionsBuilder<DbDtctechContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AuthController EmployeeController(DbDtctechContext db, IPasswordHasher<TblEmployee> hasher, LoginThrottle throttle)
    {
        var currentTenant = new CurrentTenant();
        currentTenant.Set(new ResolvedTenant(101, "tenant-a", "Tenant A", TenantDatabaseMode.Dedicated, "unused"));
        return new AuthController(db, hasher, currentTenant, loginThrottle: throttle)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Session = new TestSession() } }
        };
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 28, 8, 0, 0, TimeSpan.Zero);
        public void Advance(TimeSpan by) => _now += by;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = [];
        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => _values.TryGetValue(key, out value);
    }
}
