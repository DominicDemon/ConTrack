using ContractManagement.Domains.DTOs.Requests.Authentication;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.DTOs.Requests.Authentication;
using ContractManagement.API.Domains.DTOs.Responses.Authentication;
using ContractManagement.API.Domains.Interfaces.Authentication;
using ContractManagement.Filter;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using ContractManagement.Infrastructure.Persistence.Central;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LoginRequest = ContractManagement.Domains.DTOs.Requests.Authentication.LoginRequest;

namespace ContractManagement.Domains.Controllers.Authentication
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly DbDtctechContext _dbDtctechContext;
        private readonly IPasswordHasher<TblEmployee> _passwordHasher;
        private readonly IEmployeeAccountService? _employeeAccountService;
        private readonly CentralDbContext? _centralDbContext;

        private readonly ICurrentTenant _currentTenant;

        public AuthController(
            DbDtctechContext dbDtctechContext,
            IPasswordHasher<TblEmployee> passwordHasher,
            ICurrentTenant currentTenant,
            IEmployeeAccountService? employeeAccountService = null,
            CentralDbContext? centralDbContext = null)
        {
            this._dbDtctechContext = dbDtctechContext;
            _passwordHasher = passwordHasher;
            _currentTenant = currentTenant;
            _employeeAccountService = employeeAccountService;
            _centralDbContext = centralDbContext;
        }

        [HttpGet("profile")]
        [SessionAuthorize(AllowWhenPasswordChangeRequired = true)]
        public async Task<IActionResult> GetProfile(
            CancellationToken cancellationToken)
        {
            var employeeId = GetAuthenticatedEmployeeId();
            var profile = await AccountService.GetProfileAsync(
                employeeId,
                cancellationToken);
            return Ok(profile);
        }

        [HttpPut("profile")]
        [SessionAuthorize(AllowWhenPasswordChangeRequired = true)]
        public async Task<IActionResult> UpdateProfile(
            [FromBody] UpdateEmployeeSelfProfileRequest request,
            CancellationToken cancellationToken)
        {
            var employeeId = GetAuthenticatedEmployeeId();
            var profile = await AccountService.UpdateProfileAsync(
                employeeId,
                request,
                cancellationToken);
            HttpContext.Session.SetString(
                AccountSessionKeys.EmployeeName,
                profile.FullName ?? string.Empty);
            return Ok(profile);
        }

        [HttpGet("profile/avatar")]
        [SessionAuthorize(AllowWhenPasswordChangeRequired = true)]
        public Task<IActionResult> GetAvatar(CancellationToken cancellationToken) =>
            OpenProfileImageAsync(ProfileImageKind.Avatar, cancellationToken);

        [HttpPost("profile/avatar")]
        [SessionAuthorize(AllowWhenPasswordChangeRequired = true)]
        public async Task<IActionResult> UploadAvatar(
            [FromForm] ProfileImageUploadRequest request,
            CancellationToken cancellationToken)
        {
            var profile = await AccountService.UploadProfileImageAsync(
                GetAuthenticatedEmployeeId(),
                ProfileImageKind.Avatar,
                request,
                cancellationToken);
            return Ok(profile);
        }

        [HttpDelete("profile/avatar")]
        [SessionAuthorize(AllowWhenPasswordChangeRequired = true)]
        public async Task<IActionResult> DeleteAvatar(
            [FromQuery] string rowVersion,
            CancellationToken cancellationToken)
        {
            var profile = await AccountService.DeleteProfileImageAsync(
                GetAuthenticatedEmployeeId(),
                ProfileImageKind.Avatar,
                rowVersion,
                cancellationToken);
            return Ok(profile);
        }

        [HttpGet("profile/cover")]
        [SessionAuthorize(AllowWhenPasswordChangeRequired = true)]
        public Task<IActionResult> GetCover(CancellationToken cancellationToken) =>
            OpenProfileImageAsync(ProfileImageKind.Cover, cancellationToken);

        [HttpPost("profile/cover")]
        [SessionAuthorize(AllowWhenPasswordChangeRequired = true)]
        public async Task<IActionResult> UploadCover(
            [FromForm] ProfileImageUploadRequest request,
            CancellationToken cancellationToken)
        {
            var profile = await AccountService.UploadProfileImageAsync(
                GetAuthenticatedEmployeeId(),
                ProfileImageKind.Cover,
                request,
                cancellationToken);
            return Ok(profile);
        }

        [HttpDelete("profile/cover")]
        [SessionAuthorize(AllowWhenPasswordChangeRequired = true)]
        public async Task<IActionResult> DeleteCover(
            [FromQuery] string rowVersion,
            CancellationToken cancellationToken)
        {
            var profile = await AccountService.DeleteProfileImageAsync(
                GetAuthenticatedEmployeeId(),
                ProfileImageKind.Cover,
                rowVersion,
                cancellationToken);
            return Ok(profile);
        }

        [HttpPut("password")]
        [SessionAuthorize(AllowWhenPasswordChangeRequired = true)]
        public async Task<IActionResult> ChangeOwnPassword(
            [FromBody] ChangeOwnPasswordRequest request,
            CancellationToken cancellationToken)
        {
            await AccountService.ChangePasswordAsync(
                GetAuthenticatedEmployeeId(),
                request,
                cancellationToken);
            HttpContext.Session.Clear();
            return Ok(new
            {
                message = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại."
            });
        }

        [HttpGet("preferences")]
        [SessionAuthorize]
        public async Task<IActionResult> GetPreferences(
            CancellationToken cancellationToken)
        {
            var preferences = await AccountService.GetPreferencesAsync(
                GetAuthenticatedEmployeeId(),
                cancellationToken);
            return Ok(preferences);
        }

        [HttpPut("preferences")]
        [SessionAuthorize]
        public async Task<IActionResult> UpdatePreferences(
            [FromBody] UpdateEmployeePreferencesRequest request,
            CancellationToken cancellationToken)
        {
            var preferences = await AccountService.UpdatePreferencesAsync(
                GetAuthenticatedEmployeeId(),
                request,
                cancellationToken);
            return Ok(preferences);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromHeader(Name = "X-Tenant-Code")] string? tenantCode, [FromBody] LoginRequest request)
        {
            // 0. Get current tenant (if needed for multi-tenancy)
            var tenant = _currentTenant.GetRequiredTenant();

            var accountInput = request.AccountName?.Trim() ?? string.Empty;
            var passwordInput = request.Password ?? string.Empty;

            if (string.IsNullOrEmpty(accountInput) || string.IsNullOrEmpty(passwordInput))
            {
                return Unauthorized(new
                {
                    message = "Sai tên đăng nhập hoặc mật khẩu."
                });
            }

            // 1. Check CUSTOMER / TENANT table first (Port 3000 is the Customer / Tenant workspace)
            var customer = await _dbDtctechContext.TblCustomers.FirstOrDefaultAsync(
                c => c.CustomerEmail == accountInput
                    || c.CustomerPhone == accountInput
                    || c.CustomerFullName == accountInput
                    || c.CustomerTaxCode == accountInput,
                HttpContext.RequestAborted);

            if (customer == null && _centralDbContext != null)
            {
                var normalizedInput = accountInput.ToLowerInvariant();
                var tenantMatch = await _centralDbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        t => t.TenantCode.ToLower() == normalizedInput
                            || t.TenantName == accountInput,
                        HttpContext.RequestAborted);

                if (tenantMatch?.OwnerCustomerId != null)
                {
                    var ownerCustId = (int)tenantMatch.OwnerCustomerId.Value;
                    customer = await _dbDtctechContext.TblCustomers.FirstOrDefaultAsync(
                        c => c.CustomerId == ownerCustId,
                        HttpContext.RequestAborted);
                }

                if (customer == null)
                {
                    var bizMatch = await _centralDbContext.CustomerBusinesses
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            b => b.BusinessEmail == accountInput
                                || b.Phone == accountInput
                                || b.CompanyTaxIdentifier == accountInput
                                || b.LegalName == accountInput,
                            HttpContext.RequestAborted);

                    if (bizMatch != null)
                    {
                        var bizCustId = (int)bizMatch.CustomerId;
                        customer = await _dbDtctechContext.TblCustomers.FirstOrDefaultAsync(
                            c => c.CustomerId == bizCustId,
                            HttpContext.RequestAborted);
                    }
                }
            }

            if (customer != null && !string.IsNullOrEmpty(customer.CustomerPassword))
            {
                var dummyEmployee = new TblEmployee();
                bool customerPasswordMatches = false;
                try
                {
                    var custVerify = _passwordHasher.VerifyHashedPassword(
                        dummyEmployee,
                        customer.CustomerPassword,
                        passwordInput);
                    customerPasswordMatches = custVerify != PasswordVerificationResult.Failed;
                }
                catch
                {
                    customerPasswordMatches = false;
                }

                if (!customerPasswordMatches && customer.CustomerPassword == passwordInput)
                {
                    customerPasswordMatches = true;
                }

                if (!customerPasswordMatches)
                {
                    return Unauthorized(new
                    {
                        message = "Sai tên đăng nhập hoặc mật khẩu."
                    });
                }

                if (customer.Status == 0)
                {
                    return Unauthorized(new AuthorizationErrorResponse(
                        AuthorizationErrorCodes.EmployeeInactive,
                        "Tài khoản khách hàng đã bị khóa."));
                }

                var customerDisplayName = !string.IsNullOrWhiteSpace(customer.CustomerFullName)
                    ? customer.CustomerFullName
                    : (customer.CustomerEmail ?? customer.CustomerPhone ?? $"Khách hàng #{customer.CustomerId}");

                HttpContext.Session.SetString("ActorKind", "Customer");
                HttpContext.Session.SetInt32(
                    AccountSessionKeys.EmployeeId,
                    customer.CustomerId);
                HttpContext.Session.SetString(
                    AccountSessionKeys.EmployeeName,
                    customerDisplayName);
                HttpContext.Session.SetInt32(
                    AccountSessionKeys.EmployeeSessionVersion,
                    1);
                HttpContext.Session.SetInt32("TenantId", tenant.TenantId);
                HttpContext.Session.SetString("TenantCode", tenant.TenantCode);

                return Ok(new
                {
                    message = "Đăng nhập thành công.",
                    employeeId = customer.CustomerId,
                    employeeName = customerDisplayName,
                    tenantId = tenant.TenantId,
                    tenantCode = tenant.TenantCode,
                    tenantName = tenant.TenantName
                });
            }

            // 1b. Check EMPLOYEE table
            var employee = await _dbDtctechContext.TblEmployees.FirstOrDefaultAsync(
                e => e.EmployeeAccount == accountInput
                    || e.EmployeeEmail == accountInput
                    || e.EmployeeCode == accountInput
                    || e.EmployeeMobile == accountInput
                    || e.EmployeeFullName == accountInput,
                HttpContext.RequestAborted);

            if (employee != null && _centralDbContext != null)
            {
                var acctLower = (employee.EmployeeAccount ?? string.Empty).ToLowerInvariant();
                var codeUpper = (employee.EmployeeCode ?? string.Empty).ToUpperInvariant();
                if (acctLower is "sysadmin" or "test.manager" or "test.technical" or "test.sale" or "test.accountant"
                    || codeUpper is "ADM-SYSADMIN" or "EMP-MANAGER" or "EMP-TECH01" or "EMP-SALE01" or "EMP-ACC01")
                {
                    return Unauthorized(new
                    {
                        message = "Tài khoản nhân viên công ty phần mềm vui lòng đăng nhập tại cổng Quản trị (Port 3001)."
                    });
                }
            }

            // 2. Check if employee exists
            if (employee == null
                || string.IsNullOrEmpty(employee.EmployeePassword))
            {
                return Unauthorized(new
                {
                    message = "Sai tên đăng nhập hoặc mật khẩu."
                });
            }

            // 3. Verify password (supports Identity PBKDF2 hash and plain-text fallback)
            PasswordVerificationResult passwordVerificationResult;
            try
            {
                passwordVerificationResult = _passwordHasher.VerifyHashedPassword(
                    employee,
                    employee.EmployeePassword,
                    passwordInput);
            }
            catch
            {
                passwordVerificationResult = PasswordVerificationResult.Failed;
            }

            if (passwordVerificationResult == PasswordVerificationResult.Failed
                && employee.EmployeePassword == passwordInput)
            {
                passwordVerificationResult = PasswordVerificationResult.SuccessRehashNeeded;
            }

            if (passwordVerificationResult == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new
                {
                    message = "Sai tên đăng nhập hoặc mật khẩu."
                });
            }

            if (employee.Status != 1)
            {
                return Unauthorized(new AuthorizationErrorResponse(
                    AuthorizationErrorCodes.EmployeeInactive,
                    "Employee account is inactive."));
            }

            if (!EmployeePermissionCatalog.TryGetPermissions(
                    employee.EmployeeType,
                    out _))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new AuthorizationErrorResponse(
                        AuthorizationErrorCodes.PermissionDenied,
                        "Employee role is not valid for RBAC v1."));
            }

            if (passwordVerificationResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                employee.EmployeePassword = _passwordHasher.HashPassword(
                    employee,
                    passwordInput);
                await _dbDtctechContext.SaveChangesAsync(HttpContext.RequestAborted);
            }

            // 4. Set up session or token (this is a placeholder, implement your own logic)
            HttpContext.Session.SetInt32(
                AccountSessionKeys.EmployeeId,
                employee.EmployeeId);

            HttpContext.Session.SetString(
                AccountSessionKeys.EmployeeName,
                employee.EmployeeFullName ?? "");

            HttpContext.Session.SetInt32(
                AccountSessionKeys.EmployeeSessionVersion,
                employee.SessionVersion);

            HttpContext.Session.SetInt32("TenantId", tenant.TenantId);

            HttpContext.Session.SetString("TenantCode", tenant.TenantCode);

            // 5. Return success response
            return Ok(new
            {
                message = "Đăng nhập thành công.",
                employeeId = employee.EmployeeId,
                employeeName = employee.EmployeeFullName,
                tenantId = tenant.TenantId,
                tenantCode = tenant.TenantCode,
                tenantName = tenant.TenantName
            });
        }

        [HttpGet("me")]
        [SessionAuthorize(AllowWhenPasswordChangeRequired = true)]
        public IActionResult GetCurrentUsers()
        {
            var employee = EmployeeAuthorizationContext.GetEmployee(HttpContext);

            if (employee is null)
            {
                return Unauthorized(new AuthorizationErrorResponse(
                    AuthorizationErrorCodes.AuthenticationRequired,
                    "Employee login is required."));
            }

            var tenant = _currentTenant.GetRequiredTenant();

            return Ok(new AuthMeResponse(
                employee.EmployeeId,
                employee.Account,
                employee.FullName,
                (byte)employee.EmployeeType,
                employee.EmployeeType.ToString(),
                tenant.TenantId,
                tenant.TenantCode,
                tenant.TenantName,
                RbacPermissions.Version,
                employee.Permissions,
                employee.MustChangePassword,
                employee.PasswordChangedAt,
                employee.ImageUrl,
                EmployeePreferenceRoutes.ResolveDefault(
                    employee.DefaultPage,
                    employee.Permissions)));
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            // Clear the session
            HttpContext.Session.Clear();

            // Return success response
            return Ok(new
            {
                message = "Đăng xuất thành công."
            });
        }

        private IEmployeeAccountService AccountService =>
            _employeeAccountService
            ?? throw new InvalidOperationException(
                "Employee account service is not configured.");

        private int GetAuthenticatedEmployeeId() =>
            EmployeeAuthorizationContext.GetEmployee(HttpContext)?.EmployeeId
            ?? throw new RbacOperationException(
                StatusCodes.Status401Unauthorized,
                AuthorizationErrorCodes.AuthenticationRequired,
                "Employee login is required.");

        private async Task<IActionResult> OpenProfileImageAsync(
            ProfileImageKind kind,
            CancellationToken cancellationToken)
        {
            var image = await AccountService.OpenProfileImageAsync(
                GetAuthenticatedEmployeeId(),
                kind,
                cancellationToken);
            Response.Headers.CacheControl = "private, max-age=300";
            return File(image.Content, image.ContentType, enableRangeProcessing: true);
        }

        // [HttpGet("generate-hash")]
        // public IActionResult GenerateHash(string password = "123456")
        // {
        //     // Khởi tạo Hasher
        //     var hasher = new PasswordHasher<TblEmployee>();
            
        //     // Tham số TblEmployee có thể để null nếu bạn chỉ muốn lấy chuỗi hash
        //     var hashedPassword = hasher.HashPassword(null!, password);

        //     return Ok(new 
        //     { 
        //         PlainText = password, 
        //         HashedPassword = hashedPassword,
        //         Instruction = "Copy chuỗi HashedPassword này và dán vào cột EmployeePassword trong SQL"
        //     });
        // }
    }
}
