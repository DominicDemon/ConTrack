using ContractManagement.Attributes;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.DTOs.Requests.Authentication;
using ContractManagement.API.Domains.Interfaces.Security;
using ContractManagement.API.Domains.Support;
using ContractManagement.Domains.DTOs.Requests.SystemAuth;
using ContractManagement.API.Domains.DTOs.Requests.SystemAuthentication;
using ContractManagement.API.Domains.Interfaces.SystemAuthentication;
using ContractManagement.API.Domains.Interfaces.Authentication;
using ContractManagement.Filter;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using ContractManagement.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Domains.Controllers.SystemAuth;

[ApiController]
[Route("api/system-auth")]
[AllowWithoutTenant]
public sealed class SystemAuthController : ControllerBase
{
    private readonly CentralDbContext _centralDbContext;
    private readonly IPasswordHasher<SystemAdmin> _passwordHasher;
    private readonly ICentralSecurityAuditWriter _securityAuditWriter;
    private readonly ISystemAdminAccountService? _systemAdminAccountService;
    private readonly LoginThrottle _loginThrottle;
    private readonly ISupportTicketService? _supportTicketService;

    private static readonly string DummyPasswordHash =
        new PasswordHasher<SystemAdmin>().HashPassword(new SystemAdmin(), Guid.NewGuid().ToString("N"));

    public SystemAuthController(
        CentralDbContext centralDbContext,
        IPasswordHasher<SystemAdmin> passwordHasher,
        ICentralSecurityAuditWriter securityAuditWriter,
        ISystemAdminAccountService? systemAdminAccountService = null,
        LoginThrottle? loginThrottle = null,
        ISupportTicketService? supportTicketService = null)
    {
        _centralDbContext = centralDbContext;
        _passwordHasher = passwordHasher;
        _securityAuditWriter = securityAuditWriter;
        _systemAdminAccountService = systemAdminAccountService;
        _loginThrottle = loginThrottle ?? LoginRateLimit.CreateStandaloneThrottle();
        _supportTicketService = supportTicketService;
    }

    [HttpGet("profile")]
    [SystemAdminAuthorize(AllowWhenPasswordChangeRequired = true)]
    public async Task<IActionResult> GetProfile(
        CancellationToken cancellationToken)
    {
        var profile = await AccountService.GetProfileAsync(
            GetSystemAdminId(),
            cancellationToken);
        return Ok(profile);
    }

    [HttpPut("profile")]
    [SystemAdminAuthorize(AllowWhenPasswordChangeRequired = true)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateSystemAdminProfileRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await AccountService.UpdateProfileAsync(
            GetSystemAdminId(),
            request,
            cancellationToken);
        HttpContext.Session.SetString(
            AccountSessionKeys.SystemAdminName,
            profile.FullName);
        return Ok(profile);
    }

    [HttpGet("profile/avatar")]
    [SystemAdminAuthorize(AllowWhenPasswordChangeRequired = true)]
    public Task<IActionResult> GetAvatar(CancellationToken cancellationToken) =>
        OpenProfileImageAsync(ProfileImageKind.Avatar, cancellationToken);

    [HttpPost("profile/avatar")]
    [SystemAdminAuthorize(AllowWhenPasswordChangeRequired = true)]
    public async Task<IActionResult> UploadAvatar(
        [FromForm] ProfileImageUploadRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await AccountService.UploadProfileImageAsync(
            GetSystemAdminId(),
            ProfileImageKind.Avatar,
            request,
            cancellationToken);
        return Ok(profile);
    }

    [HttpDelete("profile/avatar")]
    [SystemAdminAuthorize(AllowWhenPasswordChangeRequired = true)]
    public async Task<IActionResult> DeleteAvatar(
        [FromQuery] string rowVersion,
        CancellationToken cancellationToken)
    {
        var profile = await AccountService.DeleteProfileImageAsync(
            GetSystemAdminId(),
            ProfileImageKind.Avatar,
            rowVersion,
            cancellationToken);
        return Ok(profile);
    }

    [HttpGet("profile/cover")]
    [SystemAdminAuthorize(AllowWhenPasswordChangeRequired = true)]
    public Task<IActionResult> GetCover(CancellationToken cancellationToken) =>
        OpenProfileImageAsync(ProfileImageKind.Cover, cancellationToken);

    [HttpPost("profile/cover")]
    [SystemAdminAuthorize(AllowWhenPasswordChangeRequired = true)]
    public async Task<IActionResult> UploadCover(
        [FromForm] ProfileImageUploadRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await AccountService.UploadProfileImageAsync(
            GetSystemAdminId(),
            ProfileImageKind.Cover,
            request,
            cancellationToken);
        return Ok(profile);
    }

    [HttpDelete("profile/cover")]
    [SystemAdminAuthorize(AllowWhenPasswordChangeRequired = true)]
    public async Task<IActionResult> DeleteCover(
        [FromQuery] string rowVersion,
        CancellationToken cancellationToken)
    {
        var profile = await AccountService.DeleteProfileImageAsync(
            GetSystemAdminId(),
            ProfileImageKind.Cover,
            rowVersion,
            cancellationToken);
        return Ok(profile);
    }

    [HttpPut("password")]
    [SystemAdminAuthorize(AllowWhenPasswordChangeRequired = true)]
    public async Task<IActionResult> ChangeOwnPassword(
        [FromBody] ChangeSystemAdminPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await AccountService.ChangePasswordAsync(
            GetSystemAdminId(),
            request,
            cancellationToken);
        HttpContext.Session.Clear();
        return Ok(new
        {
            message = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại."
        });
    }

    [HttpPost("login")]
    [EnableRateLimiting(LoginRateLimit.PolicyName)]
    public async Task<IActionResult> Login(
        [FromBody] SystemAdminLoginRequest request,
        CancellationToken cancellationToken)
    {
        var throttleKey = LoginThrottle.SystemAdminKey(request.Username);
        if (_loginThrottle.GetLockoutRemaining(throttleKey) is { } remaining)
        {
            await WriteLoginAuditAsync(
                null,
                AuthorizationAuditResultTypes.Denied,
                LoginRateLimit.LockedCode,
                cancellationToken);
            return LoginRateLimit.Locked(this, remaining);
        }

        var input = request.Username?.Trim() ?? string.Empty;
        var admin = await _centralDbContext.SystemAdmins
            .FirstOrDefaultAsync(
                x => (x.Username == input || x.Email == input)
                     && x.IsActive,
                cancellationToken);

        // Tài khoản không tồn tại vẫn verify với hash giả để thời gian phản hồi như nhau.
        var result = admin is null
            ? VerifyAgainstDummyHash(request.Password)
            : _passwordHasher.VerifyHashedPassword(
                admin,
                admin.PasswordHash,
                request.Password);

        if (admin != null && result == PasswordVerificationResult.Failed && admin.PasswordHash == request.Password)
        {
            result = PasswordVerificationResult.SuccessRehashNeeded;
        }

        if (admin is null || result == PasswordVerificationResult.Failed)
        {
            var locked = _loginThrottle.RecordFailure(throttleKey);
            await WriteLoginAuditAsync(
                admin?.SystemAdminId,
                AuthorizationAuditResultTypes.Denied,
                locked ? LoginRateLimit.LockedCode : AuthorizationErrorCodes.AuthenticationRequired,
                cancellationToken);
            return Unauthorized(new AuthorizationErrorResponse(
                AuthorizationErrorCodes.AuthenticationRequired,
                "Sai tên đăng nhập hoặc mật khẩu."));
        }

        _loginThrottle.Reset(throttleKey);

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            admin.PasswordHash = _passwordHasher.HashPassword(
                admin,
                request.Password);
            admin.UpdatedAt = DateTime.UtcNow;
            await _centralDbContext.SaveChangesAsync(cancellationToken);
        }

        HttpContext.Session.SetInt32(
            AccountSessionKeys.SystemAdminId,
            admin.SystemAdminId);

        HttpContext.Session.SetString(
            AccountSessionKeys.SystemAdminName,
            admin.FullName);

        HttpContext.Session.SetInt32(
            AccountSessionKeys.SystemAdminSessionVersion,
            admin.SessionVersion);

        await WriteLoginAuditAsync(
            admin.SystemAdminId,
            AuthorizationAuditResultTypes.Success,
            null,
            cancellationToken);

        return Ok(new
        {
            message = "System admin đăng nhập thành công.",
            systemAdminId = admin.SystemAdminId,
            fullName = admin.FullName
        });
    }

    [HttpGet("me")]
    [SystemAdminAuthorize(AllowWhenPasswordChangeRequired = true)]
    public async Task<IActionResult> Me(
        CancellationToken cancellationToken)
    {
        int? systemAdminId =
            HttpContext.Session.GetInt32(AccountSessionKeys.SystemAdminId);

        if (systemAdminId is null)
        {
            return Unauthorized(new
            {
                message = "System admin is not logged in."
            });
        }

        var admin = await _centralDbContext.SystemAdmins
            .Where(x => x.SystemAdminId == systemAdminId.Value)
            .Select(x => new
            {
                x.SystemAdminId,
                x.Username,
                x.FullName,
                x.Email,
                x.IsActive,
                x.MustChangePassword,
                x.PasswordChangedAt,
                x.AvatarStorageKey,
                x.AvatarUpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (admin is null)
        {
            return Unauthorized(new { message = "System admin not found." });
        }

        var roleInfo = _supportTicketService is not null
            ? await _supportTicketService.ResolveEmployeeRoleInfoAsync(admin.SystemAdminId, cancellationToken)
            : (Role: "SYSADMIN", DepartmentId: (int?)null, DepartmentName: (string?)null);

        return Ok(new
        {
            admin.SystemAdminId,
            admin.Username,
            admin.FullName,
            admin.Email,
            admin.IsActive,
            admin.MustChangePassword,
            admin.PasswordChangedAt,
            SupportRole = roleInfo.Role,
            DepartmentId = roleInfo.DepartmentId,
            DepartmentName = roleInfo.DepartmentName,
            ImageUrl = admin.AvatarStorageKey is null
                ? null
                : $"/api/system-auth/profile/avatar?v={admin.AvatarUpdatedAt?.Ticks ?? 0}"
        });
    }

    [HttpPost("logout")]
    [SystemAdminAuthorize(AllowWhenPasswordChangeRequired = true)]
    public IActionResult Logout()
    {
        HttpContext.Session.Remove(AccountSessionKeys.SystemAdminId);
        HttpContext.Session.Remove(AccountSessionKeys.SystemAdminName);
        HttpContext.Session.Remove(AccountSessionKeys.SystemAdminSessionVersion);

        return Ok(new
        {
            message = "System admin đăng xuất thành công."
        });
    }

    private PasswordVerificationResult VerifyAgainstDummyHash(string? password)
    {
        _passwordHasher.VerifyHashedPassword(new SystemAdmin(), DummyPasswordHash, password ?? string.Empty);
        return PasswordVerificationResult.Failed;
    }

    private Task WriteLoginAuditAsync(
        int? systemAdminId,
        string result,
        string? failureCode,
        CancellationToken cancellationToken) =>
        _securityAuditWriter.TryWriteAsync(
            HttpContext,
            new CentralSecurityAuditWriteRequest(
                systemAdminId,
                null,
                null,
                AuthorizationAuditActionTypes.SystemAdminLogin,
                result,
                "SystemAdmin",
                systemAdminId?.ToString(),
                failureCode),
            cancellationToken);

    private ISystemAdminAccountService AccountService =>
        _systemAdminAccountService
        ?? throw new InvalidOperationException(
            "System Admin account service is not configured.");

    private int GetSystemAdminId() =>
        HttpContext.Session.GetInt32(AccountSessionKeys.SystemAdminId)
        ?? throw new RbacOperationException(
            StatusCodes.Status401Unauthorized,
            AuthorizationErrorCodes.AuthenticationRequired,
            "System Admin login is required.");

    private async Task<IActionResult> OpenProfileImageAsync(
        ProfileImageKind kind,
        CancellationToken cancellationToken)
    {
        var image = await AccountService.OpenProfileImageAsync(
            GetSystemAdminId(),
            kind,
            cancellationToken);
        Response.Headers.CacheControl = "private, max-age=300";
        return File(image.Content, image.ContentType, enableRangeProcessing: true);
    }
}
