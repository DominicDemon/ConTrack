using ContractManagement.API.Domains.Email;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

/// <summary>What the "Key của tôi" page shows. <see cref="Email"/> is masked: where the code will be sent.</summary>
public sealed record MyFileKeyDto(
    bool MayHoldKey,
    bool HasKey,
    DateTime? SetAt,
    bool EncryptionEnabled,
    bool ServerKeepsRecoveryKey,
    string? Email);

/// <summary><see cref="RecoveredFiles"/>: older files the new key opens at once; null when they come back later.</summary>
public sealed record SetFileKeyResultDto(DateTime SetAt, bool Replaced, int? RecoveredFiles);

/// <summary>
/// Each employee who can see scans sets and looks after their own key; the administrator no longer issues keys and
/// never knows one. Creating a key and setting a new one after forgetting the old need the login password and a code
/// sent to the employee's email, so someone at an unlocked computer cannot do it. Changing a key needs the current
/// key and keeps every file open, as the same private key is only sealed again. Each step is written to AUDIT_LOG
/// and the employee is told by email.
/// </summary>
public sealed class FileKeyOwnerService(
    MasterDbContext db,
    ICurrentEmployee currentEmployee,
    FileProtectionService protection,
    FileKeyAttemptTracker attempts,
    FileKeyCodeStore codes,
    IEmailTransport email,
    IOptions<SmtpEmailOptions> smtp,
    IHostEnvironment environment,
    ILogger<FileKeyOwnerService> logger)
{
    /// <summary>New keys set after forgetting one, per employee and day: more points to someone else trying.</summary>
    public const int MaxResetsPerDay = 3;

    private static readonly PasswordHasher<Employee> Passwords = new();

    /// <summary>Whoever may see scans may hold a key to open them.</summary>
    public bool MayHoldKey =>
        currentEmployee.HasPermission(SigningPermissions.View) || currentEmployee.HasPermission(ScannedContractPermissions.View);

    public async Task<MyFileKeyDto> MineAsync(CancellationToken cancellationToken)
    {
        var me = await MeAsync(cancellationToken);
        var key = await ActiveKeyAsync(cancellationToken);
        return new MyFileKeyDto(
            MayHoldKey,
            key is not null,
            key is null ? null : DateTime.SpecifyKind(key.IssuedAt, DateTimeKind.Utc),
            protection.Enabled,
            protection.ServerKeepsRecoveryKey,
            Mask(me.EmployeeEmail));
    }

    /// <summary>Checks the login password, then emails a 6-digit code for creating or setting a new key.</summary>
    public async Task<string> SendCodeAsync(string? password, CancellationToken cancellationToken)
    {
        RequireMayHold();
        var me = await CheckPasswordAsync(password, cancellationToken);
        if (!SmtpEmailOptions.IsEmailAddress(me.EmployeeEmail))
        {
            throw new InternalAppException(409, "Tài khoản của bạn chưa có email hợp lệ để nhận mã. Hãy thêm email ở mục \"Hồ sơ của tôi\".");
        }

        var code = codes.Issue(me.EmployeeId);
        if (!smtp.Value.IsConfigured())
        {
            if (!environment.IsDevelopment())
            {
                codes.Forget(me.EmployeeId);
                throw new InternalAppException(503, "Máy chủ chưa cấu hình gửi email (Email:Smtp). Hãy báo quản trị.");
            }

            // A developer machine without email: the code is printed in the API terminal instead.
            logger.LogWarning("[DEV] File key code for employee {EmployeeId}: {Code}", me.EmployeeId, code);
            return Mask(me.EmployeeEmail)!;
        }

        await email.SendAsync(Message(me.EmployeeEmail!, "Mã xác nhận đặt key mở tệp",
            $"Mã xác nhận để đặt key mở tệp scan của bạn là {code}. Mã có hiệu lực {FileKeyCodeStore.Lifetime.TotalMinutes:0} phút. "
            + "Nếu bạn không yêu cầu, hãy đổi mật khẩu đăng nhập và báo quản trị ngay."), cancellationToken);
        return Mask(me.EmployeeEmail)!;
    }

    /// <summary>
    /// Creates the employee's first key, or a new one after forgetting the old (the old one stops working). Needs the
    /// login password and the emailed code.
    /// </summary>
    public async Task<SetFileKeyResultDto> SetAsync(string? password, string? code, string? newKey, CancellationToken cancellationToken)
    {
        RequireMayHold();
        var me = await CheckPasswordAsync(password, cancellationToken);
        var current = await ActiveKeyAsync(cancellationToken);
        CheckNewKey(me, newKey);
        if (current is not null && FileCrypto.UnlockPrivateKey(current.EmployeeEncryptionKeyPrivatekey, current.EmployeeEncryptionKeySalt,
                current.EmployeeEncryptionKeyIterations, newKey!) is { } same)
        {
            same.Dispose();
            throw new ArgumentException("Key mới trùng key hiện tại. Bạn vẫn nhớ key này nên không cần đặt lại.");
        }

        if (current is not null)
        {
            var since = DateTime.UtcNow.AddDays(-1);
            var resets = await db.AuditLogs.CountAsync(log => log.ActionType == "EncryptionKeyResetByOwner"
                && log.EntityName == FileProtectionService.KeyEntity && log.EntityId == me.EmployeeId.ToString()
                && log.CreatedAt > since, cancellationToken);
            if (resets >= MaxResetsPerDay)
            {
                throw new InternalAppException(429, $"Bạn đã đặt lại key {MaxResetsPerDay} lần trong 24 giờ. Hãy thử lại sau hoặc báo quản trị.");
            }
        }

        // Last before saving: a wrong or expired code is counted, and a good one is used up.
        codes.Verify(me.EmployeeId, code);
        var pair = FileCrypto.CreateUserKeyPair(newKey!);
        var recovered = await protection.InstallKeyAsync(me.EmployeeId, pair, cancellationToken);
        await TellAsync(me, current is null
            ? "Bạn vừa tạo key mở tệp scan."
            : "Key mở tệp scan của bạn vừa được đặt lại; key cũ không còn dùng được.", cancellationToken);
        return new SetFileKeyResultDto(DateTime.UtcNow, current is not null, recovered);
    }

    /// <summary>Changes the key while the employee still knows it. Every file stays open with the new key.</summary>
    public async Task<SetFileKeyResultDto> ChangeAsync(string? currentKey, string? newKey, CancellationToken cancellationToken)
    {
        RequireMayHold();
        var me = await MeAsync(cancellationToken);
        var key = await db.EmployeeEncryptionKeys.SingleOrDefaultAsync(
                candidate => candidate.EmployeeId == me.EmployeeId && candidate.EmployeeEncryptionKeyStatus == FileProtectionService.ActiveStatus,
                cancellationToken)
            ?? throw new InternalAppException(409, "Bạn chưa có key. Hãy tạo key trước.", FileKeyCodes.NoKeyIssued);
        if (attempts.RemainingLock(me.EmployeeId) is { } remaining)
        {
            throw Locked(remaining);
        }

        using var privateKey = string.IsNullOrEmpty(currentKey)
            ? null
            : FileCrypto.UnlockPrivateKey(key.EmployeeEncryptionKeyPrivatekey, key.EmployeeEncryptionKeySalt, key.EmployeeEncryptionKeyIterations, currentKey);
        if (privateKey is null)
        {
            attempts.RecordFailure(me.EmployeeId);
            throw new InternalAppException(400, "Key hiện tại không đúng.", FileKeyCodes.KeyWrong);
        }

        attempts.Reset(me.EmployeeId);
        CheckNewKey(me, newKey);
        if (string.Equals(currentKey, newKey, StringComparison.Ordinal))
        {
            throw new ArgumentException("Key mới phải khác key hiện tại.");
        }

        var resealed = FileCrypto.Reseal(privateKey, newKey!);
        var now = DateTime.UtcNow;
        key.EmployeeEncryptionKeyPrivatekey = resealed.EncryptedPrivateKey;
        key.EmployeeEncryptionKeySalt = resealed.Salt;
        key.EmployeeEncryptionKeyIterations = resealed.Iterations;
        key.IssuedByEmployeeId = me.EmployeeId;
        key.IssuedAt = now;
        db.AuditLogs.Add(new AuditLog
        {
            ActionType = "EncryptionKeyChangedByOwner",
            EntityName = FileProtectionService.KeyEntity,
            EntityId = me.EmployeeId.ToString(),
            ActorType = "Employee",
            ActorId = me.EmployeeId,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        await TellAsync(me, "Bạn vừa đổi key mở tệp scan.", cancellationToken);
        return new SetFileKeyResultDto(DateTime.SpecifyKind(now, DateTimeKind.Utc), false, null);
    }

    private void RequireMayHold()
    {
        if (!MayHoldKey)
        {
            throw new InternalAppException(403, "Bạn không có quyền xem bản scan nên không cần key mở tệp.");
        }
    }

    private async Task<Employee> MeAsync(CancellationToken cancellationToken) =>
        await db.Employees.AsNoTracking()
            .SingleOrDefaultAsync(employee => employee.EmployeeId == currentEmployee.EmployeeId
                && employee.EmployeeStatus == FileProtectionService.ActiveStatus, cancellationToken)
        ?? throw new InternalAppException(401, "Phiên đăng nhập hết hạn hoặc tài khoản đã khóa.");

    private Task<EmployeeEncryptionKey?> ActiveKeyAsync(CancellationToken cancellationToken) =>
        db.EmployeeEncryptionKeys.AsNoTracking().SingleOrDefaultAsync(
            key => key.EmployeeId == currentEmployee.EmployeeId && key.EmployeeEncryptionKeyStatus == FileProtectionService.ActiveStatus,
            cancellationToken);

    /// <summary>The login password, with the same lock as wrong keys so neither can be guessed.</summary>
    private async Task<Employee> CheckPasswordAsync(string? password, CancellationToken cancellationToken)
    {
        var me = await MeAsync(cancellationToken);
        if (attempts.RemainingLock(me.EmployeeId) is { } remaining)
        {
            throw Locked(remaining);
        }

        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(me.EmployeePasswordHash)
            || Passwords.VerifyHashedPassword(me, me.EmployeePasswordHash, password) == PasswordVerificationResult.Failed)
        {
            attempts.RecordFailure(me.EmployeeId);
            throw new InternalAppException(400, "Mật khẩu đăng nhập không đúng.");
        }

        return me;
    }

    private static void CheckNewKey(Employee me, string? newKey)
    {
        if (FileKeyPolicy.Problem(newKey, new FileKeyPolicy.Owner(me.EmployeeUsername, me.EmployeeEmail, me.EmployeePhone)) is { } problem)
        {
            throw new ArgumentException(problem);
        }

        if (!string.IsNullOrEmpty(me.EmployeePasswordHash)
            && Passwords.VerifyHashedPassword(me, me.EmployeePasswordHash, newKey!) != PasswordVerificationResult.Failed)
        {
            throw new ArgumentException("Key không được trùng mật khẩu đăng nhập.");
        }
    }

    private static InternalAppException Locked(TimeSpan remaining) => new(429,
        $"Nhập sai quá {FileKeyAttemptTracker.MaxFailures} lần. Thử lại sau {Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))} phút.",
        FileKeyCodes.KeyLocked);

    /// <summary>Best effort: the key is already set, so a mail that fails only goes to the log.</summary>
    private async Task TellAsync(Employee me, string what, CancellationToken cancellationToken)
    {
        if (!SmtpEmailOptions.IsEmailAddress(me.EmployeeEmail) || !smtp.Value.IsConfigured())
        {
            return;
        }

        try
        {
            await email.SendAsync(Message(me.EmployeeEmail!, "Key mở tệp scan của bạn đã thay đổi",
                $"{what} Thời điểm: {DateTime.UtcNow.AddHours(7):HH:mm dd/MM/yyyy} (giờ Việt Nam). "
                + "Nếu không phải bạn làm, hãy đổi mật khẩu đăng nhập và báo quản trị ngay để thu hồi key."), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not tell employee {EmployeeId} that their file key changed.", me.EmployeeId);
        }
    }

    private static EmailMessage Message(string to, string subject, string text) =>
        new(to, subject, text, $"<p style=\"font-family:Arial,sans-serif;font-size:14px\">{WebUtility.HtmlEncode(text)}</p>");

    /// <summary>a***@congty.vn: enough to recognise the mailbox, not to learn it.</summary>
    internal static string? Mask(string? address)
    {
        if (!SmtpEmailOptions.IsEmailAddress(address))
        {
            return null;
        }

        var at = address!.IndexOf('@');
        return at <= 1 ? "*" + address[at..] : address[0] + new string('*', Math.Min(at - 1, 5)) + address[at..];
    }
}

/// <summary>
/// The emailed codes for setting a key, in memory only (a restart means asking for a new code). A code lasts five
/// minutes and is used once; five wrong tries end it. A new code needs a minute's wait, and at most five are sent an hour.
/// </summary>
public sealed class FileKeyCodeStore
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ResendAfter = TimeSpan.FromSeconds(60);
    public const int MaxPerHour = 5;
    public const int MaxWrongTries = 5;

    private sealed record Entry(byte[] Hash, DateTimeOffset IssuedAt, int WrongTries, List<DateTimeOffset> Sent);

    private readonly ConcurrentDictionary<long, Entry> _entries = new();
    private readonly TimeProvider _clock;

    public FileKeyCodeStore() : this(TimeProvider.System) { }

    public FileKeyCodeStore(TimeProvider clock) => _clock = clock;

    public string Issue(long employeeId)
    {
        var now = _clock.GetUtcNow();
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        lock (_entries)
        {
            var sent = _entries.TryGetValue(employeeId, out var old) ? old.Sent.Where(at => now - at < TimeSpan.FromHours(1)).ToList() : [];
            if (old is not null && now - old.IssuedAt < ResendAfter)
            {
                var wait = (int)Math.Ceiling((ResendAfter - (now - old.IssuedAt)).TotalSeconds);
                throw new InternalAppException(429, $"Mã vừa được gửi. Chờ {wait} giây rồi gửi lại.");
            }

            if (sent.Count >= MaxPerHour)
            {
                throw new InternalAppException(429, $"Đã gửi mã {MaxPerHour} lần trong 1 giờ. Hãy thử lại sau.");
            }

            sent.Add(now);
            _entries[employeeId] = new Entry(Hash(code), now, 0, sent);
        }

        return code;
    }

    /// <summary>Uses the code up when it is right; throws when it is wrong, expired or was never sent.</summary>
    public void Verify(long employeeId, string? code)
    {
        lock (_entries)
        {
            if (!_entries.TryGetValue(employeeId, out var entry) || entry.WrongTries >= MaxWrongTries
                || _clock.GetUtcNow() - entry.IssuedAt > Lifetime)
            {
                throw new InternalAppException(400, "Mã xác nhận đã hết hạn hoặc chưa được gửi. Hãy bấm gửi mã mới.");
            }

            if (code is null || !CryptographicOperations.FixedTimeEquals(entry.Hash, Hash(code.Trim())))
            {
                _entries[employeeId] = entry with { WrongTries = entry.WrongTries + 1 };
                var left = MaxWrongTries - entry.WrongTries - 1;
                throw new InternalAppException(400, left > 0
                    ? $"Mã xác nhận không đúng. Còn {left} lần thử."
                    : "Mã xác nhận không đúng. Mã này đã hết lượt, hãy gửi mã mới.");
            }

            // Used: the employee keeps the send history, so the hourly limit still holds.
            _entries[employeeId] = entry with { Hash = [], WrongTries = MaxWrongTries };
        }
    }

    public void Forget(long employeeId)
    {
        lock (_entries)
        {
            if (_entries.TryGetValue(employeeId, out var entry))
            {
                _entries[employeeId] = entry with { WrongTries = MaxWrongTries, IssuedAt = DateTimeOffset.MinValue };
            }
        }
    }

    private static byte[] Hash(string code) => SHA256.HashData(Encoding.UTF8.GetBytes(code));
}
