using ContractManagement.API.Domains.Email;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master.Scans;
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed record CustomerBusinessProfileDto(string LegalName, string TaxCode, string? Address, string? Representative);

/// <summary>The signed-in customer's own account, page "Hồ sơ của tôi" of the portal.</summary>
public sealed record CustomerProfileDto(
    string Name,
    string Phone,
    string? Email,
    DateTime? EmailVerifiedAt,
    string? Address,
    string? TaxCode,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    IReadOnlyList<CustomerBusinessProfileDto> Businesses);

/// <summary>
/// Wrong passwords and email codes of customers, kept apart from employees' (the ids of the two tables overlap).
/// </summary>
public sealed class CustomerProfileGuards
{
    public FileKeyAttemptTracker Attempts { get; } = new();

    public EmailChangeCodes Codes { get; } = new();

    /// <summary>Codes for a forgotten password, sent to the email of the account.</summary>
    public FileKeyCodeStore ResetCodes { get; } = new();
}

/// <summary>
/// Lets a customer see their account and change their email. Contract review links are sent to that email, so a new
/// address needs the portal password and a code sent to it; the old address is told. Name, phone (the login) and tax
/// code stay with the company's staff.
/// </summary>
public sealed class CustomerProfileService(
    MasterDbContext db,
    CustomerProfileGuards guards,
    IEmailTransport email,
    IOptions<SmtpEmailOptions> smtp,
    IHostEnvironment environment,
    ILogger<CustomerProfileService> logger)
{
    private const int Active = 1;
    private static readonly PasswordHasher<Customer> Passwords = new();

    public async Task<CustomerProfileDto> GetAsync(long customerId, CancellationToken cancellationToken)
    {
        var me = await MeAsync(customerId, cancellationToken);
        var businesses = await db.CustomerBusinesses.AsNoTracking()
            .Where(business => business.CustomerId == customerId)
            .OrderBy(business => business.CustomerBusinessLegalName)
            .Select(business => new CustomerBusinessProfileDto(
                business.CustomerBusinessLegalName,
                business.CustomerBusinessCompanyTaxIdentifier,
                business.CustomerBusinessOfficeAddress ?? business.CustomerBusinessHeadOfficeAddress,
                business.CustomerBusinessRepresentativeName))
            .ToListAsync(cancellationToken);
        return new CustomerProfileDto(
            me.CustomerDisplayName,
            me.CustomerPersonalPhone,
            me.CustomerEmail,
            Utc(me.CustomerEmailVerifiedAt),
            me.CustomerAddress,
            me.CustomerPersonalTaxIdentifier,
            DateTime.SpecifyKind(me.CreatedAt, DateTimeKind.Utc),
            Utc(me.CustomerLastLoginAt),
            businesses);
    }

    public async Task<string> SendEmailCodeAsync(long customerId, string? password, string? newEmail, CancellationToken cancellationToken)
    {
        var me = await MeAsync(customerId, cancellationToken);
        CheckPassword(me, password);
        var address = EmployeeProfileService.Normalise(newEmail);
        if (string.Equals(address, me.CustomerEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Email mới trùng email hiện tại.");
        }

        if (await db.Customers.AnyAsync(other => other.CustomerId != customerId && other.CustomerEmail == address, cancellationToken))
        {
            throw new InternalAppException(409, "Email này đã được dùng cho tài khoản khác. Liên hệ nhân viên phụ trách nếu cần hỗ trợ.");
        }

        var code = guards.Codes.Issue(customerId, address);
        if (!smtp.Value.IsConfigured())
        {
            if (!environment.IsDevelopment())
            {
                guards.Codes.Forget(customerId);
                throw new InternalAppException(503, "Hệ thống chưa gửi được email lúc này. Vui lòng thử lại sau.");
            }

            logger.LogWarning("[DEV] Customer email change code for customer {CustomerId}: {Code}", customerId, code);
            return FileKeyOwnerService.Mask(address)!;
        }

        await email.SendAsync(Message(address, "Mã xác nhận đổi email",
            $"Mã xác nhận để dùng email này cho tài khoản khách hàng {me.CustomerDisplayName} là {code}. Mã có hiệu lực "
            + $"{FileKeyCodeStore.Lifetime.TotalMinutes:0} phút. Nếu bạn không yêu cầu, hãy bỏ qua thư này."), cancellationToken);
        return FileKeyOwnerService.Mask(address)!;
    }

    public async Task<CustomerProfileDto> ConfirmEmailAsync(long customerId, string? code, CancellationToken cancellationToken)
    {
        var me = await db.Customers.SingleOrDefaultAsync(customer => customer.CustomerId == customerId && customer.CustomerStatus == Active, cancellationToken)
            ?? throw new InternalAppException(401, "Phiên đăng nhập hết hạn hoặc tài khoản đã khóa.");
        var address = guards.Codes.Verify(customerId, code);
        if (await db.Customers.AnyAsync(other => other.CustomerId != customerId && other.CustomerEmail == address, cancellationToken))
        {
            throw new InternalAppException(409, "Email này đã được dùng cho tài khoản khác. Liên hệ nhân viên phụ trách nếu cần hỗ trợ.");
        }

        var old = me.CustomerEmail;
        var now = DateTime.UtcNow;
        me.CustomerEmail = address;
        me.CustomerEmailVerifiedAt = now;
        me.UpdatedAt = now;
        ReviewData.Audit(db, "CHANGE_EMAIL", "CUSTOMER", customerId, "CUSTOMER", customerId, now);
        await db.SaveChangesAsync(cancellationToken);

        if (SmtpEmailOptions.IsEmailAddress(old) && smtp.Value.IsConfigured())
        {
            try
            {
                await email.SendAsync(Message(old!, "Email tài khoản của bạn đã được đổi",
                    $"Email nhận hợp đồng của tài khoản {me.CustomerDisplayName} vừa được đổi sang {FileKeyOwnerService.Mask(address)} lúc "
                    + $"{now.AddHours(7):HH:mm dd/MM/yyyy} (giờ Việt Nam). Nếu không phải bạn làm, hãy liên hệ nhân viên phụ trách ngay."),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Could not tell the old address of customer {CustomerId} about the change.", customerId);
            }
        }

        return await GetAsync(customerId, cancellationToken);
    }

    /// <summary>The same answer whether or not the phone has an account, so nobody learns who is a customer.</summary>
    public const string ForgotAnswer = "Nếu số điện thoại có tài khoản và email, mã đặt lại mật khẩu đã được gửi tới email đó.";

    /// <summary>
    /// Forgotten password: a 6-digit code to the email of the account (where contract links already go). Says nothing
    /// about whether the phone has an account, has an email, or was sent a code a moment ago.
    /// </summary>
    public async Task ForgotPasswordAsync(string? phone, CancellationToken cancellationToken)
    {
        var customer = await ByPhoneAsync(phone, cancellationToken);
        if (customer is null || !SmtpEmailOptions.IsEmailAddress(customer.CustomerEmail))
        {
            return;
        }

        string code;
        try
        {
            code = guards.ResetCodes.Issue(customer.CustomerId);
        }
        catch (InternalAppException exception) when (exception.Status == 429)
        {
            return;
        }

        if (!smtp.Value.IsConfigured())
        {
            if (!environment.IsDevelopment())
            {
                guards.ResetCodes.Forget(customer.CustomerId);
                throw new InternalAppException(503, "Hệ thống chưa gửi được email lúc này. Vui lòng thử lại sau.");
            }

            logger.LogWarning("[DEV] Customer password reset code for customer {CustomerId}: {Code}", customer.CustomerId, code);
            return;
        }

        await email.SendAsync(Message(customer.CustomerEmail!, "Mã đặt lại mật khẩu",
            $"Mã đặt lại mật khẩu cổng khách hàng của {customer.CustomerDisplayName} là {code}. Mã có hiệu lực "
            + $"{FileKeyCodeStore.Lifetime.TotalMinutes:0} phút. Nếu bạn không yêu cầu, hãy bỏ qua thư này; mật khẩu vẫn giữ nguyên."), cancellationToken);
    }

    /// <summary>Sets the new password when the emailed code is right; returns the customer to sign in.</summary>
    public async Task<long> ResetPasswordAsync(string? phone, string? code, string? newPassword, CancellationToken cancellationToken)
    {
        if (!CustomerPasswordRules.IsValid(newPassword))
        {
            throw new ArgumentException(CustomerPasswordRules.Message);
        }

        var found = await ByPhoneAsync(phone, cancellationToken)
            ?? throw new InternalAppException(400, "Mã xác nhận đã hết hạn hoặc chưa được gửi. Hãy bấm gửi mã mới.");
        guards.ResetCodes.Verify(found.CustomerId, code);
        var customer = await db.Customers.SingleAsync(x => x.CustomerId == found.CustomerId, cancellationToken);
        var now = DateTime.UtcNow;
        customer.CustomerPasswordHash = Passwords.HashPassword(customer, newPassword!);
        // A password of their own, not a temporary one: the portal does not ask to change it again.
        customer.CustomerPersonalPhoneVerifiedAt ??= now;
        customer.UpdatedAt = now;
        ReviewData.Audit(db, "RESET_PASSWORD_SELF", "CUSTOMER", customer.CustomerId, "CUSTOMER", customer.CustomerId, now);
        await db.SaveChangesAsync(cancellationToken);
        guards.Attempts.Reset(customer.CustomerId);

        if (smtp.Value.IsConfigured())
        {
            try
            {
                await email.SendAsync(Message(customer.CustomerEmail!, "Mật khẩu của bạn đã được đổi",
                    $"Mật khẩu cổng khách hàng của {customer.CustomerDisplayName} vừa được đặt lại lúc {now.AddHours(7):HH:mm dd/MM/yyyy} "
                    + "(giờ Việt Nam). Nếu không phải bạn làm, hãy liên hệ nhân viên phụ trách ngay."), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Could not tell customer {CustomerId} that their password was reset.", customer.CustomerId);
            }
        }

        return customer.CustomerId;
    }

    private async Task<Customer?> ByPhoneAsync(string? phone, CancellationToken cancellationToken) =>
        PhoneNumber.Normalize(phone) is { } normalized
            ? await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerPersonalPhone == normalized && x.CustomerStatus == Active, cancellationToken)
            : null;

    private async Task<Customer> MeAsync(long customerId, CancellationToken cancellationToken) =>
        await db.Customers.AsNoTracking().SingleOrDefaultAsync(customer => customer.CustomerId == customerId && customer.CustomerStatus == Active, cancellationToken)
        ?? throw new InternalAppException(401, "Phiên đăng nhập hết hạn hoặc tài khoản đã khóa.");

    private void CheckPassword(Customer me, string? password)
    {
        if (guards.Attempts.RemainingLock(me.CustomerId) is { } remaining)
        {
            throw new InternalAppException(429,
                $"Nhập sai mật khẩu quá {FileKeyAttemptTracker.MaxFailures} lần. Thử lại sau {Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))} phút.");
        }

        if (string.IsNullOrEmpty(password) || password.Length > 200 || string.IsNullOrEmpty(me.CustomerPasswordHash)
            || Passwords.VerifyHashedPassword(me, me.CustomerPasswordHash, password) == PasswordVerificationResult.Failed)
        {
            guards.Attempts.RecordFailure(me.CustomerId);
            throw new InternalAppException(400, "Mật khẩu không đúng.");
        }

        guards.Attempts.Reset(me.CustomerId);
    }

    private static DateTime? Utc(DateTime? value) => value is { } at ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : null;

    private static EmailMessage Message(string to, string subject, string text) =>
        new(to, subject, text, $"<p style=\"font-family:Arial,sans-serif;font-size:14px\">{WebUtility.HtmlEncode(text)}</p>");
}
