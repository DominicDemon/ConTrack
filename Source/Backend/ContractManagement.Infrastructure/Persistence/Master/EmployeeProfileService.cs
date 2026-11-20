using ContractManagement.API.Domains.Email;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Infrastructure.Persistence.Master.Scans;
using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Persistence.Master;

/// <summary>The signed-in employee's own account, page "Hồ sơ của tôi".</summary>
public sealed record EmployeeProfileDto(
    string Code,
    string Username,
    string Name,
    string? Email,
    DateTime? EmailVerifiedAt,
    string? Phone,
    DateTime? LastLoginAt);

/// <summary>
/// Lets an employee see their account and change their email. The email is where the codes for setting a file key
/// go, so changing it needs the login password and a code sent to the NEW address (proving it is theirs); the old
/// address is told. The password itself is changed through /auth/change-password.
/// </summary>
public sealed class EmployeeProfileService(
    MasterDbContext db,
    FileKeyAttemptTracker attempts,
    EmailChangeCodes codes,
    IEmailTransport email,
    IOptions<SmtpEmailOptions> smtp,
    IHostEnvironment environment,
    ILogger<EmployeeProfileService> logger)
{
    private static readonly PasswordHasher<Employee> Passwords = new();

    public async Task<EmployeeProfileDto> GetAsync(long employeeId, CancellationToken cancellationToken)
    {
        var me = await MeAsync(employeeId, cancellationToken);
        return new EmployeeProfileDto(
            me.EmployeeCode,
            me.EmployeeUsername,
            me.EmployeeDisplayName,
            me.EmployeeEmail,
            Utc(me.EmployeeEmailVerifiedAt),
            me.EmployeePhone,
            Utc(me.EmployeeLastLoginAt));
    }

    /// <summary>Checks the password and the new address, then emails a 6-digit code to the new address.</summary>
    public async Task<string> SendEmailCodeAsync(long employeeId, string? password, string? newEmail, CancellationToken cancellationToken)
    {
        var me = await MeAsync(employeeId, cancellationToken);
        CheckPassword(me, password);
        var address = Normalise(newEmail);
        if (string.Equals(address, me.EmployeeEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Email mới trùng email hiện tại.");
        }

        if (await db.Employees.AnyAsync(other => other.EmployeeId != employeeId && other.EmployeeEmail == address, cancellationToken))
        {
            throw new InternalAppException(409, "Email đã được dùng cho nhân viên khác.");
        }

        var code = codes.Issue(employeeId, address);
        if (!smtp.Value.IsConfigured())
        {
            if (!environment.IsDevelopment())
            {
                codes.Forget(employeeId);
                throw new InternalAppException(503, "Máy chủ chưa cấu hình gửi email (Email:Smtp). Hãy báo quản trị.");
            }

            logger.LogWarning("[DEV] Email change code for employee {EmployeeId}: {Code}", employeeId, code);
            return FileKeyOwnerService.Mask(address)!;
        }

        await email.SendAsync(Message(address, "Mã xác nhận đổi email",
            $"Mã xác nhận để dùng email này cho tài khoản {me.EmployeeUsername} là {code}. Mã có hiệu lực "
            + $"{FileKeyCodeStore.Lifetime.TotalMinutes:0} phút. Nếu bạn không yêu cầu, hãy bỏ qua thư này."), cancellationToken);
        return FileKeyOwnerService.Mask(address)!;
    }

    /// <summary>Saves the address the code was sent to, marked as verified, and tells the old address.</summary>
    public async Task<EmployeeProfileDto> ConfirmEmailAsync(long employeeId, string? code, CancellationToken cancellationToken)
    {
        var me = await db.Employees.SingleOrDefaultAsync(
                employee => employee.EmployeeId == employeeId && employee.EmployeeStatus == FileProtectionService.ActiveStatus, cancellationToken)
            ?? throw new InternalAppException(401, "Phiên đăng nhập hết hạn hoặc tài khoản đã khóa.");
        var address = codes.Verify(employeeId, code);
        if (await db.Employees.AnyAsync(other => other.EmployeeId != employeeId && other.EmployeeEmail == address, cancellationToken))
        {
            throw new InternalAppException(409, "Email đã được dùng cho nhân viên khác.");
        }

        var old = me.EmployeeEmail;
        var now = DateTime.UtcNow;
        me.EmployeeEmail = address;
        me.EmployeeEmailVerifiedAt = now;
        me.UpdatedAt = now;
        me.UpdatedBy = employeeId;
        db.AuditLogs.Add(new AuditLog
        {
            ActionType = "CHANGE_EMAIL",
            EntityName = "EMPLOYEE",
            EntityId = employeeId.ToString(),
            ActorType = "EMPLOYEE",
            ActorId = employeeId,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);

        if (SmtpEmailOptions.IsEmailAddress(old) && smtp.Value.IsConfigured())
        {
            try
            {
                await email.SendAsync(Message(old!, "Email tài khoản của bạn đã được đổi",
                    $"Email của tài khoản {me.EmployeeUsername} vừa được đổi sang {FileKeyOwnerService.Mask(address)} lúc "
                    + $"{now.AddHours(7):HH:mm dd/MM/yyyy} (giờ Việt Nam). Nếu không phải bạn làm, hãy báo quản trị ngay."), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Could not tell the old address of employee {EmployeeId} about the change.", employeeId);
            }
        }

        return await GetAsync(employeeId, cancellationToken);
    }

    private async Task<Employee> MeAsync(long employeeId, CancellationToken cancellationToken) =>
        await db.Employees.AsNoTracking().SingleOrDefaultAsync(
            employee => employee.EmployeeId == employeeId && employee.EmployeeStatus == FileProtectionService.ActiveStatus, cancellationToken)
        ?? throw new InternalAppException(401, "Phiên đăng nhập hết hạn hoặc tài khoản đã khóa.");

    /// <summary>Same lock as wrong file keys: five wrong tries close both for 15 minutes.</summary>
    private void CheckPassword(Employee me, string? password)
    {
        if (attempts.RemainingLock(me.EmployeeId) is { } remaining)
        {
            throw new InternalAppException(429,
                $"Nhập sai quá {FileKeyAttemptTracker.MaxFailures} lần. Thử lại sau {Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))} phút.");
        }

        if (string.IsNullOrEmpty(password) || password.Length > 200 || string.IsNullOrEmpty(me.EmployeePasswordHash)
            || Passwords.VerifyHashedPassword(me, me.EmployeePasswordHash, password) == PasswordVerificationResult.Failed)
        {
            attempts.RecordFailure(me.EmployeeId);
            throw new InternalAppException(400, "Mật khẩu đăng nhập không đúng.");
        }
    }

    /// <summary>As the administrator's form stores it (EmployeeAdminRules): trimmed, lower case, at most 100 characters.</summary>
    internal static string Normalise(string? value)
    {
        var address = (value ?? "").Trim().ToLowerInvariant();
        if (address.Length is 0 or > 100 || !SmtpEmailOptions.IsEmailAddress(address) || !address.Contains('.', StringComparison.Ordinal))
        {
            throw new ArgumentException("Email không hợp lệ.");
        }

        return address;
    }

    private static DateTime? Utc(DateTime? value) => value is { } at ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : null;

    private static EmailMessage Message(string to, string subject, string text) =>
        new(to, subject, text, $"<p style=\"font-family:Arial,sans-serif;font-size:14px\">{WebUtility.HtmlEncode(text)}</p>");
}

/// <summary>
/// Codes for confirming a new email, with the address they were sent to: the same limits as the file key codes
/// (FileKeyCodeStore), kept apart from them. In memory only.
/// </summary>
public sealed class EmailChangeCodes
{
    private readonly FileKeyCodeStore _codes;
    private readonly ConcurrentDictionary<long, string> _addresses = new();

    public EmailChangeCodes() : this(TimeProvider.System) { }

    public EmailChangeCodes(TimeProvider clock) => _codes = new FileKeyCodeStore(clock);

    public string Issue(long employeeId, string address)
    {
        var code = _codes.Issue(employeeId);
        _addresses[employeeId] = address;
        return code;
    }

    /// <summary>The address the code was sent to; throws when the code is wrong, expired or used.</summary>
    public string Verify(long employeeId, string? code)
    {
        _codes.Verify(employeeId, code);
        return _addresses.TryRemove(employeeId, out var address)
            ? address
            : throw new InternalAppException(400, "Mã xác nhận đã hết hạn hoặc chưa được gửi. Hãy bấm gửi mã mới.");
    }

    public void Forget(long employeeId)
    {
        _codes.Forget(employeeId);
        _addresses.TryRemove(employeeId, out _);
    }
}
