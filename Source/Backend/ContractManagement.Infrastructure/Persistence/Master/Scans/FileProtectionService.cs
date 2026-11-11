using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal.Scans;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

public sealed class FileEncryptionOptions
{
    public const string SectionName = "FileEncryption";

    /// <summary>Encrypt new scans. Files stored before stay readable as they are.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>PEM file of the company recovery PUBLIC key.</summary>
    public string RecoveryPublicKeyPath { get; set; } = "";

    /// <summary>
    /// Optional PEM file of the recovery PRIVATE key. When set, reissuing an employee's key reopens their old files for
    /// the new key at once. The trade-off: whoever takes over this server can open every file. Left empty, an
    /// administrator gives the private key per request instead.
    /// </summary>
    public string RecoveryPrivateKeyPath { get; set; } = "";
}

/// <summary>The recovery public key, read once from <see cref="FileEncryptionOptions.RecoveryPublicKeyPath"/>.</summary>
public interface IRecoveryPublicKey
{
    /// <summary>SubjectPublicKeyInfo bytes.</summary>
    byte[] PublicKey { get; }

    string KeyId { get; }

    /// <summary>The matching private key when the server keeps it (RecoveryPrivateKeyPath), otherwise null. Dispose it after use.</summary>
    RSA? CreatePrivateKey();
}

public sealed record EmployeeKeyStatusDto(
    long EmployeeId,
    string Username,
    string DisplayName,
    bool HasKey,
    DateTime? IssuedAt,
    string? IssuedBy,
    int FilesToRecover);

/// <summary><see cref="RecoveredFiles"/>: old files the new key opens at once (null when the server keeps no recovery key).</summary>
public sealed record IssuedKeyDto(long EmployeeId, string DisplayName, string Key, DateTime IssuedAt, int? RecoveredFiles);

/// <summary>
/// Result of reopening an employee's old files for their current key. <see cref="NotRecoverable"/> files were
/// stored without the given recovery key (e.g. before it was made) and stay closed to the new key.
/// </summary>
public sealed record RecoveredFilesDto(long EmployeeId, int Granted, int AlreadyOpen, int NotRecoverable);

/// <summary>
/// Encrypts stored scans and opens them again (Artifacts/Database/Master/008-file-encryption.sql, FileCrypto).
/// Who may open a file is decided by the feature that owns it; this service only holds the keys.
/// </summary>
public sealed class FileProtectionService(
    MasterDbContext db,
    ICurrentEmployee currentEmployee,
    IOptions<FileEncryptionOptions> options,
    IRecoveryPublicKey recovery,
    FileKeyAttemptTracker attempts)
{
    public const string ActiveStatus = "ACTIVE";
    public const string RevokedStatus = "REVOKED";
    public const string KeyEntity = "EMPLOYEE_ENCRYPTION_KEY";
    public const string FileEntity = "FILE_STORAGE";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool Enabled => options.Value.Enabled;

    /// <summary>The server keeps the recovery private key, so old files come back without an administrator giving it.</summary>
    public bool ServerKeepsRecoveryKey => !string.IsNullOrWhiteSpace(options.Value.RecoveryPrivateKeyPath);

    // ---------------------------------------------------------------------------------------------
    // Keys of employees
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Gives the employee a new 12-character key, shown this once. An older key stops working. When the server keeps
    /// the recovery private key, the files the older key opened are granted to the new key right away; otherwise they
    /// are granted when someone who can open them does so (see OpenAsync) or by RecoverFilesAsync.
    /// </summary>
    public async Task<IssuedKeyDto> IssueKeyAsync(long employeeId, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.AsNoTracking()
            .Where(candidate => candidate.EmployeeId == employeeId)
            .Select(candidate => new { candidate.EmployeeId, candidate.EmployeeDisplayName, candidate.EmployeeStatus })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InternalAppException(404, "Không tìm thấy nhân viên.");
        if (employee.EmployeeStatus != ActiveStatus)
        {
            throw new InternalAppException(409, "Tài khoản nhân viên này đã bị khóa.");
        }

        var userKey = FileCrypto.GenerateUserKey();
        var pair = FileCrypto.CreateUserKeyPair(userKey);
        var now = DateTime.UtcNow;
        var issued = new EmployeeEncryptionKey
        {
            EmployeeId = employeeId,
            EmployeeEncryptionKeyPublickey = pair.PublicKey,
            EmployeeEncryptionKeyPrivatekey = pair.EncryptedPrivateKey,
            EmployeeEncryptionKeySalt = pair.Salt,
            EmployeeEncryptionKeyIterations = pair.Iterations,
            EmployeeEncryptionKeyStatus = ActiveStatus,
            IssuedByEmployeeId = currentEmployee.EmployeeId,
            IssuedAt = now,
        };
        await db.ExecuteInTransactionAsync(async () =>
        {
            var revoked = await db.EmployeeEncryptionKeys
                .Where(key => key.EmployeeId == employeeId && key.EmployeeEncryptionKeyStatus == ActiveStatus)
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(key => key.EmployeeEncryptionKeyStatus, RevokedStatus)
                        .SetProperty(key => key.RevokedAt, now),
                    cancellationToken);
            db.EmployeeEncryptionKeys.Add(issued);
            db.AuditLogs.Add(Audit("EncryptionKeyIssued", KeyEntity, employeeId, new { replacedOldKey = revoked > 0 }));
            await db.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        attempts.Reset(employeeId);

        int? recovered = null;
        using (var recoveryKey = recovery.CreatePrivateKey())
        {
            if (recoveryKey is not null)
            {
                recovered = (await RegrantAsync(employeeId, recoveryKey, cancellationToken)).Granted;
            }
        }

        return new IssuedKeyDto(employeeId, employee.EmployeeDisplayName, userKey, DateTime.SpecifyKind(now, DateTimeKind.Utc), recovered);
    }

    public async Task<IReadOnlyList<EmployeeKeyStatusDto>> ListKeysAsync(CancellationToken cancellationToken)
    {
        var rows = await (
                from employee in db.Employees.AsNoTracking()
                join key in db.EmployeeEncryptionKeys.Where(key => key.EmployeeEncryptionKeyStatus == ActiveStatus)
                    on employee.EmployeeId equals key.EmployeeId into keys
                from key in keys.DefaultIfEmpty()
                where employee.EmployeeStatus == ActiveStatus
                orderby employee.EmployeeDisplayName
                select new { employee.EmployeeId, employee.EmployeeUsername, employee.EmployeeDisplayName, key })
            .ToListAsync(cancellationToken);
        var issuers = rows.Where(row => row.key != null).Select(row => row.key!.IssuedByEmployeeId).Distinct().ToList();
        var names = await db.Employees.AsNoTracking()
            .Where(employee => issuers.Contains(employee.EmployeeId))
            .ToDictionaryAsync(employee => employee.EmployeeId, employee => employee.EmployeeDisplayName, cancellationToken);
        var lost = await LostFilesAsync(null, cancellationToken);
        return rows.Select(row => new EmployeeKeyStatusDto(
                row.EmployeeId,
                row.EmployeeUsername,
                row.EmployeeDisplayName,
                row.key != null,
                row.key is null ? null : DateTime.SpecifyKind(row.key.IssuedAt, DateTimeKind.Utc),
                row.key is null ? null : names.GetValueOrDefault(row.key.IssuedByEmployeeId),
                row.key is null ? 0 : lost.GetValueOrDefault(row.EmployeeId)?.Count ?? 0))
            .ToList();
    }

    /// <summary>
    /// Lets the employee's current key open again the files an older key of theirs opened. The recovery private key is
    /// the one the administrator gives with the request (never kept), or, when none is given, the one the server keeps.
    /// Only files the employee could already open are granted, so this never widens who sees what. Every grant is
    /// written to AUDIT_LOG.
    /// </summary>
    public async Task<RecoveredFilesDto> RecoverFilesAsync(long employeeId, string? recoveryPrivateKeyPem, CancellationToken cancellationToken)
    {
        if (recoveryPrivateKeyPem is null)
        {
            using var serverKey = recovery.CreatePrivateKey()
                ?? throw new ArgumentException("Hãy chọn tệp recovery-private.pem.");
            return await RegrantAsync(employeeId, serverKey, cancellationToken);
        }

        using var recoveryKey = RSA.Create();
        try
        {
            recoveryKey.ImportFromPem(recoveryPrivateKeyPem);
            // A public key also imports; it cannot unwrap anything.
            recoveryKey.ExportParameters(includePrivateParameters: true);
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException)
        {
            throw new ArgumentException("Tệp tải lên không phải khóa dự phòng riêng (recovery-private.pem).");
        }

        if (FileCrypto.KeyId(recoveryKey.ExportSubjectPublicKeyInfo()) != recovery.KeyId)
        {
            throw new ArgumentException("Khóa dự phòng này không khớp với khóa hệ thống đang dùng.");
        }

        return await RegrantAsync(employeeId, recoveryKey, cancellationToken);
    }

    private async Task<RecoveredFilesDto> RegrantAsync(long employeeId, RSA recoveryKey, CancellationToken cancellationToken)
    {
        var employeeKey = await db.EmployeeEncryptionKeys.AsNoTracking()
            .SingleOrDefaultAsync(key => key.EmployeeId == employeeId && key.EmployeeEncryptionKeyStatus == ActiveStatus, cancellationToken)
            ?? throw new InternalAppException(409, "Nhân viên này chưa có key: hãy cấp key trước.");

        var lostFiles = (await LostFilesAsync(employeeId, cancellationToken)).GetValueOrDefault(employeeId) ?? [];
        var recoveryRows = await db.FileEncryptionKeys.AsNoTracking()
            .Where(row => row.EmployeeEncryptionKeyId == null
                && row.FileEncryptionKeyRecoverykeyid == recovery.KeyId
                && lostFiles.Contains(row.FileStorageFileid))
            .Select(row => new { row.FileStorageFileid, row.FileEncryptionKeyWrappedkey })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var row in recoveryRows)
        {
            var fileKey = FileCrypto.Unwrap(row.FileEncryptionKeyWrappedkey, recoveryKey);
            db.FileEncryptionKeys.Add(new FileEncryptionKey
            {
                FileStorageFileid = row.FileStorageFileid,
                EmployeeEncryptionKeyId = employeeKey.EmployeeEncryptionKeyId,
                FileEncryptionKeyWrappedkey = FileCrypto.Wrap(fileKey, employeeKey.EmployeeEncryptionKeyPublickey),
                GrantedByEmployeeId = currentEmployee.EmployeeId,
                GrantedAt = now,
            });
            CryptographicOperations.ZeroMemory(fileKey);
            db.AuditLogs.Add(Audit("EncryptedFileGrantedWithRecoveryKey", FileEntity, row.FileStorageFileid, new { employeeId }));
        }

        await db.SaveChangesAsync(cancellationToken);
        var alreadyOpen = await (
                from held in db.FileEncryptionKeys.AsNoTracking()
                join old in db.EmployeeEncryptionKeys on held.EmployeeEncryptionKeyId equals old.EmployeeEncryptionKeyId
                where old.EmployeeId == employeeId && old.EmployeeEncryptionKeyStatus != ActiveStatus
                select held.FileStorageFileid)
            .Distinct()
            .CountAsync(cancellationToken) - lostFiles.Count;
        return new RecoveredFilesDto(employeeId, recoveryRows.Count, alreadyOpen, lostFiles.Count - recoveryRows.Count);
    }

    /// <summary>
    /// Per employee with an active key: files an older (revoked) key of theirs could open that their current key
    /// cannot yet. <paramref name="employeeId"/> null means every employee.
    /// </summary>
    private async Task<Dictionary<long, List<long>>> LostFilesAsync(long? employeeId, CancellationToken cancellationToken)
    {
        var oldGrants = await (
                from held in db.FileEncryptionKeys.AsNoTracking()
                join old in db.EmployeeEncryptionKeys on held.EmployeeEncryptionKeyId equals old.EmployeeEncryptionKeyId
                where old.EmployeeEncryptionKeyStatus != ActiveStatus && (employeeId == null || old.EmployeeId == employeeId)
                select new { old.EmployeeId, held.FileStorageFileid })
            .Distinct()
            .ToListAsync(cancellationToken);
        var currentGrants = (await (
                    from held in db.FileEncryptionKeys.AsNoTracking()
                    join current in db.EmployeeEncryptionKeys on held.EmployeeEncryptionKeyId equals current.EmployeeEncryptionKeyId
                    where current.EmployeeEncryptionKeyStatus == ActiveStatus && (employeeId == null || current.EmployeeId == employeeId)
                    select new { current.EmployeeId, held.FileStorageFileid })
                .ToListAsync(cancellationToken))
            .Select(grant => (grant.EmployeeId, grant.FileStorageFileid))
            .ToHashSet();
        return oldGrants
            .Where(grant => !currentGrants.Contains((grant.EmployeeId, grant.FileStorageFileid)))
            .GroupBy(grant => grant.EmployeeId)
            .ToDictionary(group => group.Key, group => group.Select(grant => grant.FileStorageFileid).ToList());
    }

    public async Task<bool> HasActiveKeyAsync(long employeeId, CancellationToken cancellationToken) =>
        await db.EmployeeEncryptionKeys.AnyAsync(
            key => key.EmployeeId == employeeId && key.EmployeeEncryptionKeyStatus == ActiveStatus, cancellationToken);

    // ---------------------------------------------------------------------------------------------
    // Files
    // ---------------------------------------------------------------------------------------------

    /// <summary>The bytes to store: encrypted when encryption is on. The file key is null when it is off.</summary>
    public (byte[] Stored, byte[]? FileKey) Protect(byte[] content)
    {
        if (!Enabled)
        {
            return (content, null);
        }

        // Without a recovery key the file would be lost with its readers' keys: refuse before anything is stored.
        _ = recovery.KeyId;
        var (stored, fileKey) = FileCrypto.EncryptFile(content);
        return (stored, fileKey);
    }

    /// <summary>
    /// Wraps the file key for the recovery key and for every given employee who holds a key. Adds the rows to the
    /// context; the caller saves them with the file's other rows. Employees without a key yet are granted later.
    /// </summary>
    public async Task GrantAsync(long fileId, byte[]? fileKey, IEnumerable<long> employeeIds, CancellationToken cancellationToken)
    {
        if (fileKey is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (!await db.FileEncryptionKeys.AnyAsync(row => row.FileStorageFileid == fileId && row.EmployeeEncryptionKeyId == null, cancellationToken)
            && !db.FileEncryptionKeys.Local.Any(row => row.FileStorageFileid == fileId && row.EmployeeEncryptionKeyId == null))
        {
            db.FileEncryptionKeys.Add(new FileEncryptionKey
            {
                FileStorageFileid = fileId,
                EmployeeEncryptionKeyId = null,
                FileEncryptionKeyWrappedkey = FileCrypto.Wrap(fileKey, recovery.PublicKey),
                FileEncryptionKeyRecoverykeyid = recovery.KeyId,
                GrantedAt = now,
            });
        }

        var wanted = employeeIds.Distinct().ToList();
        var keys = await db.EmployeeEncryptionKeys.AsNoTracking()
            .Where(key => wanted.Contains(key.EmployeeId) && key.EmployeeEncryptionKeyStatus == ActiveStatus)
            .Select(key => new { key.EmployeeEncryptionKeyId, key.EmployeeEncryptionKeyPublickey })
            .ToListAsync(cancellationToken);
        var keyIds = keys.Select(key => (long?)key.EmployeeEncryptionKeyId).ToList();
        var already = (await db.FileEncryptionKeys.AsNoTracking()
                .Where(row => row.FileStorageFileid == fileId && keyIds.Contains(row.EmployeeEncryptionKeyId))
                .Select(row => row.EmployeeEncryptionKeyId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        foreach (var key in keys.Where(key => !already.Contains(key.EmployeeEncryptionKeyId)))
        {
            db.FileEncryptionKeys.Add(new FileEncryptionKey
            {
                FileStorageFileid = fileId,
                EmployeeEncryptionKeyId = key.EmployeeEncryptionKeyId,
                FileEncryptionKeyWrappedkey = FileCrypto.Wrap(fileKey, key.EmployeeEncryptionKeyPublickey),
                GrantedByEmployeeId = currentEmployee.EmployeeId,
                GrantedAt = now,
            });
        }
    }

    /// <summary>
    /// The plain bytes of a stored file for the signed-in employee. A file stored before encryption is returned as
    /// it is. An encrypted one needs the employee's 12-character key; once open, it is also granted to the
    /// <paramref name="eligible"/> employees who got a key since it was stored. Every opening is logged.
    /// </summary>
    public async Task<byte[]> OpenAsync(
        long fileId,
        byte[] stored,
        string? userKey,
        IReadOnlyCollection<long> eligible,
        CancellationToken cancellationToken)
    {
        if (!FileCrypto.IsEncrypted(stored))
        {
            return stored;
        }

        var me = currentEmployee.EmployeeId;
        if (string.IsNullOrWhiteSpace(userKey))
        {
            throw new InternalAppException(400, "Tệp đã được mã hóa. Hãy nhập key 12 ký tự của bạn.", FileKeyCodes.KeyRequired);
        }

        if (attempts.IsLocked(me))
        {
            throw new InternalAppException(429, $"Nhập sai key quá {FileKeyAttemptTracker.MaxFailures} lần. Thử lại sau {FileKeyAttemptTracker.LockDuration.TotalMinutes:0} phút.", FileKeyCodes.KeyLocked);
        }

        var key = await db.EmployeeEncryptionKeys.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.EmployeeId == me && candidate.EmployeeEncryptionKeyStatus == ActiveStatus, cancellationToken)
            ?? throw new InternalAppException(403, "Bạn chưa được cấp key để mở tệp. Hãy liên hệ quản trị.", FileKeyCodes.NoKeyIssued);

        using var privateKey = FileCrypto.UnlockPrivateKey(
            key.EmployeeEncryptionKeyPrivatekey, key.EmployeeEncryptionKeySalt, key.EmployeeEncryptionKeyIterations, userKey.Trim());
        if (privateKey is null)
        {
            attempts.RecordFailure(me);
            throw new InternalAppException(400, "Key không đúng.", FileKeyCodes.KeyWrong);
        }

        attempts.Reset(me);
        var wrapped = await db.FileEncryptionKeys.AsNoTracking()
            .Where(row => row.FileStorageFileid == fileId && row.EmployeeEncryptionKeyId == key.EmployeeEncryptionKeyId)
            .Select(row => row.FileEncryptionKeyWrappedkey)
            .SingleOrDefaultAsync(cancellationToken);
        var fileKey = wrapped is not null
            ? FileCrypto.Unwrap(wrapped, privateKey)
            : await GrantWithServerKeyAsync(fileId, key, eligible, cancellationToken)
              ?? throw new InternalAppException(403, "Key của bạn chưa được cấp quyền mở tệp này. Tệp sẽ được cấp khi một người đang mở được tệp mở nó, hoặc quản trị cấp bằng khóa dự phòng.", FileKeyCodes.NotGranted);
        byte[] plain;
        try
        {
            plain = FileCrypto.DecryptFile(stored, fileKey);
        }
        catch (AuthenticationTagMismatchException)
        {
            throw new InternalAppException(409, "Tệp đã bị thay đổi sau khi lưu (không khớp mã kiểm tra). Hãy báo quản trị.");
        }

        await GrantAsync(fileId, fileKey, eligible, cancellationToken);
        db.AuditLogs.Add(Audit("EncryptedFileOpened", FileEntity, fileId, null));
        await db.SaveChangesAsync(cancellationToken);
        CryptographicOperations.ZeroMemory(fileKey);
        return plain;
    }

    /// <summary>
    /// The file key for an employee who may open the file (<paramref name="eligible"/>) but whose current key was never
    /// given it: e.g. the scan was stored before they had a key, or they joined later. Only when the server keeps the
    /// recovery private key; the grant is saved and logged at once. Null when it cannot be done this way.
    /// </summary>
    private async Task<byte[]?> GrantWithServerKeyAsync(
        long fileId,
        EmployeeEncryptionKey key,
        IReadOnlyCollection<long> eligible,
        CancellationToken cancellationToken)
    {
        if (!eligible.Contains(key.EmployeeId))
        {
            return null;
        }

        using var recoveryKey = recovery.CreatePrivateKey();
        if (recoveryKey is null)
        {
            return null;
        }

        var recoveryWrapped = await db.FileEncryptionKeys.AsNoTracking()
            .Where(row => row.FileStorageFileid == fileId
                && row.EmployeeEncryptionKeyId == null
                && row.FileEncryptionKeyRecoverykeyid == recovery.KeyId)
            .Select(row => row.FileEncryptionKeyWrappedkey)
            .FirstOrDefaultAsync(cancellationToken);
        if (recoveryWrapped is null)
        {
            return null;
        }

        var fileKey = FileCrypto.Unwrap(recoveryWrapped, recoveryKey);
        db.FileEncryptionKeys.Add(new FileEncryptionKey
        {
            FileStorageFileid = fileId,
            EmployeeEncryptionKeyId = key.EmployeeEncryptionKeyId,
            FileEncryptionKeyWrappedkey = FileCrypto.Wrap(fileKey, key.EmployeeEncryptionKeyPublickey),
            GrantedByEmployeeId = key.EmployeeId,
            GrantedAt = DateTime.UtcNow,
        });
        db.AuditLogs.Add(Audit("EncryptedFileGrantedWithRecoveryKey", FileEntity, fileId, new { employeeId = key.EmployeeId }));
        // Saved now so the grants to the other readers below do not add this employee twice.
        await db.SaveChangesAsync(cancellationToken);
        return fileKey;
    }

    // ---------------------------------------------------------------------------------------------
    // Who holds a permission
    // ---------------------------------------------------------------------------------------------

    /// <summary>Active employees who hold the permission, after the permissions it depends on are added.</summary>
    public async Task<IReadOnlyList<long>> EmployeesWithPermissionAsync(string permissionCode, CancellationToken cancellationToken)
    {
        var target = await db.SystemPermissions.AsNoTracking()
            .Where(permission => permission.SystemPermissionCode == permissionCode)
            .Select(permission => (long?)permission.SystemPermissionId)
            .SingleOrDefaultAsync(cancellationToken);
        if (target is null)
        {
            return [];
        }

        var grants = await (
                from role in db.EmployeeRoles.AsNoTracking()
                join systemRole in db.SystemRoles on role.SystemRoleId equals systemRole.SystemRoleId
                join rolePermission in db.SystemRolePermissions on role.SystemRoleId equals rolePermission.SystemRoleId
                join employee in db.Employees on role.EmployeeId equals employee.EmployeeId
                where systemRole.SystemRoleStatus == ActiveStatus && employee.EmployeeStatus == ActiveStatus
                select new { role.EmployeeId, rolePermission.SystemPermissionId })
            .ToListAsync(cancellationToken);
        var dependencies = (await db.SystemPermissionDependencies.AsNoTracking()
                .Select(dependency => new { dependency.SystemPermissionId, dependency.DependsOnSystemPermissionId })
                .ToListAsync(cancellationToken))
            .Select(dependency => (dependency.SystemPermissionId, dependency.DependsOnSystemPermissionId))
            .ToList();
        return grants
            .GroupBy(grant => grant.EmployeeId)
            .Where(group => PermissionClosure.Expand(group.Select(grant => grant.SystemPermissionId), dependencies).Contains(target.Value))
            .Select(group => group.Key)
            .ToList();
    }

    private AuditLog Audit(string action, string entity, long entityId, object? newData) => new()
    {
        ActionType = action,
        EntityName = entity,
        EntityId = entityId.ToString(),
        ActorType = "Employee",
        ActorId = currentEmployee.EmployeeId,
        NewData = newData is null ? null : JsonSerializer.Serialize(newData, Json),
        CreatedAt = DateTime.UtcNow,
    };
}

/// <summary>Locks file opening for an employee for a while after repeated wrong keys, so a key cannot be guessed.</summary>
public sealed class FileKeyAttemptTracker
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<long, (int Failures, DateTimeOffset? LockedUntil)> _counters = new();

    public bool IsLocked(long employeeId) =>
        _counters.TryGetValue(employeeId, out var counter) && counter.LockedUntil > DateTimeOffset.UtcNow;

    public void RecordFailure(long employeeId) =>
        _counters.AddOrUpdate(
            employeeId,
            _ => (1, null),
            (_, counter) =>
            {
                var failures = counter.LockedUntil <= DateTimeOffset.UtcNow ? 1 : counter.Failures + 1;
                return (failures, failures >= MaxFailures ? DateTimeOffset.UtcNow + LockDuration : counter.LockedUntil);
            });

    public void Reset(long employeeId) => _counters.TryRemove(employeeId, out _);
}
