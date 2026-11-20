using ContractManagement.API.Domains.Email;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using ContractManagement.Domain.Internal.Scans;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

public sealed record CustomerFileKeyStatusDto(bool HasKey, DateTime? SetAt, string? Email, bool ServerKeepsRecoveryKey);

/// <summary>A scan on the portal. <see cref="Ready"/>: the customer's key can open it now.</summary>
public sealed record CustomerScanDto(
    string FileId, string Kind, string FileName, string? ContractCode, string? ContractName, DateTime UploadedAt, string MatchedBy, bool Ready, string Status);

public sealed record CustomerScanFile(string FileName, string ContentType, byte[] Content);
public sealed record CustomerScanUploadResult(string FileId, string FileName, DateTime UploadedAt);

/// <summary>Wrong keys and key codes of customers, apart from the employees' (the ids of the two tables overlap).</summary>
public sealed class CustomerKeyGuards
{
    public FileKeyAttemptTracker Attempts { get; } = new();

    public FileKeyCodeStore Codes { get; } = new();
}

/// <summary>
/// The portal side of the scans (017-customer-file-keys.sql): a customer sets their own key, as staff do (login
/// password and a code sent to their email to create one or set a new one; the current key to change it), and opens
/// the scans that belong to them (CustomerScans). Nobody else knows the key. Every opening is logged.
/// </summary>
public sealed class CustomerScanService(
    MasterDbContext db,
    IFileStore files,
    IRecoveryPublicKey recovery,
    FileProtectionService protection,
    CustomerKeyGuards guards,
    IEmailTransport email,
    IOptions<SmtpEmailOptions> smtp,
    IHostEnvironment environment,
    ILogger<CustomerScanService> logger)
{
    private const string Active = FileProtectionService.ActiveStatus;
    private static readonly PasswordHasher<Customer> Passwords = new();

    public async Task<CustomerFileKeyStatusDto> KeyStatusAsync(long customerId, CancellationToken cancellationToken)
    {
        var me = await MeAsync(customerId, cancellationToken);
        var key = await ActiveKeyAsync(customerId, cancellationToken);
        return new CustomerFileKeyStatusDto(key is not null, key is null ? null : Utc(key.CreatedAt),
            FileKeyOwnerService.Mask(me.CustomerEmail), protection.ServerKeepsRecoveryKey);
    }

    public async Task<string> SendKeyCodeAsync(long customerId, string? password, CancellationToken cancellationToken)
    {
        var me = await MeAsync(customerId, cancellationToken);
        CheckPassword(me, password);
        if (!SmtpEmailOptions.IsEmailAddress(me.CustomerEmail))
        {
            throw new InternalAppException(409, "Tài khoản chưa có email để nhận mã. Hãy thêm email ở mục Hồ sơ của tôi.");
        }

        var code = guards.Codes.Issue(customerId);
        if (!smtp.Value.IsConfigured())
        {
            if (!environment.IsDevelopment())
            {
                guards.Codes.Forget(customerId);
                throw new InternalAppException(503, "Hệ thống chưa gửi được email lúc này. Vui lòng thử lại sau.");
            }

            logger.LogWarning("[DEV] Customer file key code for customer {CustomerId}: {Code}", customerId, code);
            return FileKeyOwnerService.Mask(me.CustomerEmail)!;
        }

        await email.SendAsync(Message(me.CustomerEmail!, "Mã xác nhận đặt key xem bản scan",
            $"Mã xác nhận để đặt key xem bản scan hợp đồng của {me.CustomerDisplayName} là {code}. Mã có hiệu lực "
            + $"{FileKeyCodeStore.Lifetime.TotalMinutes:0} phút. Nếu bạn không yêu cầu, hãy đổi mật khẩu và liên hệ nhân viên phụ trách."), cancellationToken);
        return FileKeyOwnerService.Mask(me.CustomerEmail)!;
    }

    /// <summary>Creates the first key, or a new one after forgetting the old: password and the emailed code.</summary>
    public async Task<int?> SetKeyAsync(long customerId, string? password, string? code, string? newKey, CancellationToken cancellationToken)
    {
        var me = await MeAsync(customerId, cancellationToken);
        CheckPassword(me, password);
        CheckNewKey(me, newKey);
        var current = await ActiveKeyAsync(customerId, cancellationToken);
        if (current is not null && FileCrypto.UnlockPrivateKey(current.CustomerEncryptionKeyPrivatekey, current.CustomerEncryptionKeySalt,
                current.CustomerEncryptionKeyIterations, newKey!) is { } same)
        {
            same.Dispose();
            throw new ArgumentException("Key mới trùng key hiện tại. Bạn vẫn nhớ key này nên không cần đặt lại.");
        }

        guards.Codes.Verify(customerId, code);
        var pair = FileCrypto.CreateUserKeyPair(newKey!);
        var now = DateTime.UtcNow;
        var installed = new CustomerEncryptionKey
        {
            CustomerId = customerId,
            CustomerEncryptionKeyPublickey = pair.PublicKey,
            CustomerEncryptionKeyPrivatekey = pair.EncryptedPrivateKey,
            CustomerEncryptionKeySalt = pair.Salt,
            CustomerEncryptionKeyIterations = pair.Iterations,
            CustomerEncryptionKeyStatus = Active,
            CreatedAt = now,
        };
        await db.ExecuteInTransactionAsync(async () =>
        {
            await db.CustomerEncryptionKeys
                .Where(key => key.CustomerId == customerId && key.CustomerEncryptionKeyStatus == Active)
                .ExecuteUpdateAsync(set => set.SetProperty(key => key.CustomerEncryptionKeyStatus, FileProtectionService.RevokedStatus)
                    .SetProperty(key => key.RevokedAt, now), cancellationToken);
            db.CustomerEncryptionKeys.Add(installed);
            ReviewData.Audit(db, current is null ? "CUSTOMER_FILE_KEY_CREATED" : "CUSTOMER_FILE_KEY_RESET", "CUSTOMER", customerId, "CUSTOMER", customerId, now);
            await db.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        guards.Attempts.Reset(customerId);
        await TellAsync(me, current is null ? "Bạn vừa tạo key xem bản scan hợp đồng." : "Key xem bản scan của bạn vừa được đặt lại; key cũ không còn dùng được.", cancellationToken);
        return await GrantWithServerKeyAsync(customerId, installed, cancellationToken);
    }

    /// <summary>Changes the key while it is known; every scan already given stays open (the same private key, sealed again).</summary>
    public async Task ChangeKeyAsync(long customerId, string? currentKey, string? newKey, CancellationToken cancellationToken)
    {
        var me = await MeAsync(customerId, cancellationToken);
        var key = await db.CustomerEncryptionKeys.SingleOrDefaultAsync(k => k.CustomerId == customerId && k.CustomerEncryptionKeyStatus == Active, cancellationToken)
            ?? throw new InternalAppException(409, "Bạn chưa có key. Hãy tạo key trước.", FileKeyCodes.NoKeyIssued);
        using var privateKey = Unlock(customerId, key, currentKey);
        CheckNewKey(me, newKey);
        if (string.Equals(currentKey, newKey, StringComparison.Ordinal))
        {
            throw new ArgumentException("Key mới phải khác key hiện tại.");
        }

        var resealed = FileCrypto.Reseal(privateKey, newKey!);
        var now = DateTime.UtcNow;
        key.CustomerEncryptionKeyPrivatekey = resealed.EncryptedPrivateKey;
        key.CustomerEncryptionKeySalt = resealed.Salt;
        key.CustomerEncryptionKeyIterations = resealed.Iterations;
        key.CreatedAt = now;
        ReviewData.Audit(db, "CUSTOMER_FILE_KEY_CHANGED", "CUSTOMER", customerId, "CUSTOMER", customerId, now);
        await db.SaveChangesAsync(cancellationToken);
        await TellAsync(me, "Bạn vừa đổi key xem bản scan hợp đồng.", cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerScanDto>> ListAsync(long customerId, CancellationToken cancellationToken)
    {
        var matches = await CustomerScans.ForCustomerAsync(db, customerId, cancellationToken);
        if (matches.Count == 0)
        {
            return [];
        }

        var fileIds = matches.Select(m => m.FileId).ToList();
        var stored = await db.FileStorages.AsNoTracking()
            .Where(f => fileIds.Contains(f.FileStorageFileid))
            .Select(f => new { f.FileStorageFileid, f.FileStorageFilename, f.FileStorageUploadeddate })
            .ToDictionaryAsync(f => f.FileStorageFileid, cancellationToken);
        var documents = await (
                from file in db.ContractDocumentFiles.AsNoTracking()
                join document in db.ContractDocuments on file.ContractDocumentId equals document.ContractDocumentId
                where fileIds.Contains(file.FileStorageFileid)
                select new { file.FileStorageFileid, document.ContractDocumentCode, document.ContractDocumentName })
            .ToListAsync(cancellationToken);
        var contracts = await db.Contracts.AsNoTracking()
            .Where(c => c.FileStorageFileid != null && fileIds.Contains(c.FileStorageFileid.Value))
            .Select(c => new { FileId = c.FileStorageFileid!.Value, c.ContractCode, c.ContractName })
            .ToListAsync(cancellationToken);
        var codes = await db.ContractScanKeywords.AsNoTracking()
            .Where(k => fileIds.Contains(k.FileStorageFileid) && k.ContractScanKeywordType == (int)ScanKeywordType.ContractCode)
            .Select(k => new { k.FileStorageFileid, k.ContractScanKeywordValue })
            .ToListAsync(cancellationToken);
        var key = await ActiveKeyAsync(customerId, cancellationToken);
        var granted = key is null ? [] : (await db.CustomerFileKeys.AsNoTracking()
                .Where(row => row.CustomerEncryptionKeyId == key.CustomerEncryptionKeyId && fileIds.Contains(row.FileStorageFileid))
                .Select(row => row.FileStorageFileid)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        return matches
            .Where(m => stored.ContainsKey(m.FileId))
            .Select(m =>
            {
                var file = stored[m.FileId];
                var document = documents.FirstOrDefault(d => d.FileStorageFileid == m.FileId);
                var contract = contracts.FirstOrDefault(c => c.FileId == m.FileId);
                return new CustomerScanDto(
                    m.FileId.ToString(), m.Kind, file.FileStorageFilename,
                    document?.ContractDocumentCode ?? contract?.ContractCode ?? codes.FirstOrDefault(c => c.FileStorageFileid == m.FileId)?.ContractScanKeywordValue,
                    document?.ContractDocumentName ?? contract?.ContractName,
                    DateTime.SpecifyKind(file.FileStorageUploadeddate, DateTimeKind.Utc), m.MatchedBy,
                    key is not null && (granted.Contains(m.FileId) || protection.ServerKeepsRecoveryKey), m.Status);
            })
            .OrderByDescending(scan => scan.UploadedAt)
            .ToList();
    }

    /// <summary>Stores a customer's own scan as a contract file. This deliberately creates no signed-evidence/review record.</summary>
    public async Task<CustomerScanUploadResult> UploadAsync(long customerId, long documentId, string fileName, byte[] content, CancellationToken cancellationToken)
    {
        var document = await db.ContractDocuments.AsNoTracking()
            .Where(row => row.ContractDocumentId == documentId && row.CustomerId == customerId)
            .Select(row => new { row.ContractDocumentId, row.ContractDocumentCurrentVersionId, row.EmployeeId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InternalAppException(404, "Không tìm thấy hợp đồng của bạn.");
        if (content.Length == 0 || content.Length > 20 * 1024 * 1024)
            throw new ArgumentException("Chọn tệp có nội dung, tối đa 20 MB.");
        var (extension, contentType) = ScanFileKinds.Detect(content) is { } kind
            ? kind switch { ScanFileKind.Pdf => (".pdf", "application/pdf"), ScanFileKind.Jpeg => (".jpg", "image/jpeg"), _ => (".png", "image/png") }
            : throw new ArgumentException("Chỉ hỗ trợ tệp PDF, JPG hoặc PNG.");
        var safeName = Path.GetFileName(fileName);
        var sha = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var (storedBytes, fileKey) = protection.Protect(content);
        var now = DateTime.UtcNow;
        var storageKey = $"customer-contract-scans/{customerId}/{documentId}/{Guid.NewGuid():N}{extension}{(fileKey is null ? "" : ".enc")}";
        await files.SaveAsync(storageKey, storedBytes, cancellationToken);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var file = new FileStorage
            {
                FileStorageObjectid = documentId, FileStorageObjecttype = "CONTRACT_DOCUMENT", FileStorageFiletype = extension[1..].ToUpperInvariant(),
                FileStorageFilename = safeName, FileStorageFilepath = storageKey, FileStorageStoragekey = storageKey,
                FileStorageContenttype = contentType, FileStorageFilesize = content.LongLength, FileStorageSha256 = sha,
                FileStorageUploadedbycustomerid = customerId, FileStorageUploadeddate = now,
            };
            db.FileStorages.Add(file);
            await db.SaveChangesAsync(cancellationToken);
            await protection.GrantForCustomerUploadAsync(file.FileStorageFileid, fileKey, [document.EmployeeId], cancellationToken);
            if (fileKey is not null) await CustomerScans.GrantToCustomerAsync(db, file.FileStorageFileid, customerId, fileKey, cancellationToken);
            db.ContractDocumentFiles.Add(new ContractDocumentFile
            {
                ContractDocumentId = documentId, ContractVersionId = document.ContractDocumentCurrentVersionId,
                FileStorageFileid = file.FileStorageFileid, ContractDocumentFileRole = ContractDocumentFileRoles.SignedPaper,
                IsPrimary = false, CreatedDate = now, CreatedEmployeeId = null,
            });
            ReviewData.Audit(db, "CUSTOMER_CONTRACT_SCAN_UPLOADED", "FILE_STORAGE", file.FileStorageFileid, "CUSTOMER", customerId, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new CustomerScanUploadResult(file.FileStorageFileid.ToString(), safeName, now);
        }
        catch
        {
            await files.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }
    }

    /// <summary>The plain file for the customer: theirs (CustomerScans) and opened with their key. Logged.</summary>
    public async Task<CustomerScanFile> OpenAsync(long customerId, long fileId, string? userKey, CancellationToken cancellationToken)
    {
        if (!(await CustomerScans.ForCustomerAsync(db, customerId, cancellationToken)).Any(m => m.FileId == fileId))
        {
            throw new InternalAppException(404, "Không tìm thấy bản scan.");
        }

        var file = await db.FileStorages.AsNoTracking().SingleAsync(f => f.FileStorageFileid == fileId, cancellationToken);
        var stored = await files.ReadAsync(file.FileStorageStoragekey, cancellationToken)
            ?? throw new InternalAppException(404, "Không tìm thấy tệp trên máy chủ. Liên hệ nhân viên phụ trách.");
        byte[] plain;
        if (!FileCrypto.IsEncrypted(stored))
        {
            plain = stored;
        }
        else
        {
            if (string.IsNullOrEmpty(userKey))
            {
                throw new InternalAppException(400, "Bản scan được mã hóa. Hãy nhập key của bạn.", FileKeyCodes.KeyRequired);
            }

            var key = await ActiveKeyAsync(customerId, cancellationToken)
                ?? throw new InternalAppException(403, "Bạn chưa tạo key xem bản scan. Vào mục \"Key xem bản scan\" để tạo.", FileKeyCodes.NoKeyIssued);
            using var privateKey = Unlock(customerId, key, userKey.Trim());
            var wrapped = await db.CustomerFileKeys.AsNoTracking()
                .Where(row => row.FileStorageFileid == fileId && row.CustomerEncryptionKeyId == key.CustomerEncryptionKeyId)
                .Select(row => row.CustomerFileKeyWrappedkey)
                .SingleOrDefaultAsync(cancellationToken);
            var fileKey = wrapped is not null
                ? FileCrypto.Unwrap(wrapped, privateKey)
                : await GrantOneWithServerKeyAsync(fileId, key, cancellationToken)
                  ?? throw new InternalAppException(403, "Bản scan này chưa sẵn sàng cho key của bạn. Hệ thống sẽ cấp khi nhân viên mở bản scan; vui lòng thử lại sau hoặc liên hệ nhân viên phụ trách.", FileKeyCodes.NotGranted);
            try
            {
                plain = FileCrypto.DecryptFile(stored, fileKey);
            }
            catch (AuthenticationTagMismatchException)
            {
                throw new InternalAppException(409, "Tệp đã bị thay đổi sau khi lưu. Liên hệ nhân viên phụ trách.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(fileKey);
            }
        }

        ReviewData.Audit(db, "CUSTOMER_SCAN_OPENED", "FILE_STORAGE", fileId, "CUSTOMER", customerId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return new CustomerScanFile(file.FileStorageFilename, file.FileStorageContenttype, plain);
    }

    private RSA Unlock(long customerId, CustomerEncryptionKey key, string? userKey)
    {
        if (guards.Attempts.RemainingLock(customerId) is { } remaining)
        {
            throw new InternalAppException(429,
                $"Nhập sai key quá {FileKeyAttemptTracker.MaxFailures} lần. Thử lại sau {Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))} phút.", FileKeyCodes.KeyLocked);
        }

        var privateKey = string.IsNullOrEmpty(userKey) ? null
            : FileCrypto.UnlockPrivateKey(key.CustomerEncryptionKeyPrivatekey, key.CustomerEncryptionKeySalt, key.CustomerEncryptionKeyIterations, userKey);
        if (privateKey is null)
        {
            guards.Attempts.RecordFailure(customerId);
            throw new InternalAppException(400, "Key không đúng.", FileKeyCodes.KeyWrong);
        }

        guards.Attempts.Reset(customerId);
        return privateKey;
    }

    /// <summary>After a key is set: every scan of the customer, when the server keeps the recovery key; null otherwise.</summary>
    private async Task<int?> GrantWithServerKeyAsync(long customerId, CustomerEncryptionKey key, CancellationToken cancellationToken)
    {
        if (!protection.ServerKeepsRecoveryKey)
        {
            return null;
        }

        var granted = 0;
        foreach (var match in await CustomerScans.ForCustomerAsync(db, customerId, cancellationToken))
        {
            try
            {
                var fileKey = await GrantOneWithServerKeyAsync(match.FileId, key, cancellationToken);
                if (fileKey is not null)
                {
                    CryptographicOperations.ZeroMemory(fileKey);
                    granted++;
                }
            }
            catch (InternalAppException exception) when (exception.Status == 503)
            {
                logger.LogWarning("Scans of customer {CustomerId} were not opened for their new key: {Reason}", customerId, exception.Message);
                return null;
            }
        }

        return granted;
    }

    /// <summary>The file key through the recovery key the server keeps, saved for the customer's key; null when it cannot.</summary>
    private async Task<byte[]?> GrantOneWithServerKeyAsync(long fileId, CustomerEncryptionKey key, CancellationToken cancellationToken)
    {
        using var recoveryKey = recovery.CreatePrivateKey();
        if (recoveryKey is null)
        {
            return null;
        }

        var wrapped = await db.FileEncryptionKeys.AsNoTracking()
            .Where(row => row.FileStorageFileid == fileId && row.EmployeeEncryptionKeyId == null && row.FileEncryptionKeyRecoverykeyid == recovery.KeyId)
            .Select(row => row.FileEncryptionKeyWrappedkey)
            .FirstOrDefaultAsync(cancellationToken);
        if (wrapped is null)
        {
            return null;
        }

        var fileKey = FileCrypto.Unwrap(wrapped, recoveryKey);
        if (!await db.CustomerFileKeys.AnyAsync(row => row.FileStorageFileid == fileId && row.CustomerEncryptionKeyId == key.CustomerEncryptionKeyId, cancellationToken))
        {
            db.CustomerFileKeys.Add(new CustomerFileKey
            {
                FileStorageFileid = fileId,
                CustomerEncryptionKeyId = key.CustomerEncryptionKeyId,
                CustomerFileKeyWrappedkey = FileCrypto.Wrap(fileKey, key.CustomerEncryptionKeyPublickey),
                GrantedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        return fileKey;
    }

    private async Task<Customer> MeAsync(long customerId, CancellationToken cancellationToken) =>
        await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.CustomerId == customerId && c.CustomerStatus == 1, cancellationToken)
        ?? throw new InternalAppException(401, "Phiên đăng nhập hết hạn hoặc tài khoản đã khóa.");

    private Task<CustomerEncryptionKey?> ActiveKeyAsync(long customerId, CancellationToken cancellationToken) =>
        db.CustomerEncryptionKeys.AsNoTracking().SingleOrDefaultAsync(k => k.CustomerId == customerId && k.CustomerEncryptionKeyStatus == Active, cancellationToken);

    private void CheckPassword(Customer me, string? password)
    {
        if (guards.Attempts.RemainingLock(me.CustomerId) is { } remaining)
        {
            throw new InternalAppException(429,
                $"Nhập sai quá {FileKeyAttemptTracker.MaxFailures} lần. Thử lại sau {Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))} phút.");
        }

        if (string.IsNullOrEmpty(password) || password.Length > 200
            || Passwords.VerifyHashedPassword(me, me.CustomerPasswordHash, password) == PasswordVerificationResult.Failed)
        {
            guards.Attempts.RecordFailure(me.CustomerId);
            throw new InternalAppException(400, "Mật khẩu không đúng.");
        }
    }

    private static void CheckNewKey(Customer me, string? newKey)
    {
        if (FileKeyPolicy.Problem(newKey, new FileKeyPolicy.Owner(null, me.CustomerEmail, me.CustomerPersonalPhone)) is { } problem)
        {
            throw new ArgumentException(problem);
        }

        if (Passwords.VerifyHashedPassword(me, me.CustomerPasswordHash, newKey!) != PasswordVerificationResult.Failed)
        {
            throw new ArgumentException("Key không được trùng mật khẩu đăng nhập.");
        }
    }

    private async Task TellAsync(Customer me, string what, CancellationToken cancellationToken)
    {
        if (!SmtpEmailOptions.IsEmailAddress(me.CustomerEmail) || !smtp.Value.IsConfigured())
        {
            return;
        }

        try
        {
            await email.SendAsync(Message(me.CustomerEmail!, "Key xem bản scan của bạn đã thay đổi",
                $"{what} Thời điểm: {DateTime.UtcNow.AddHours(7):HH:mm dd/MM/yyyy} (giờ Việt Nam). Nếu không phải bạn làm, hãy đổi mật khẩu và liên hệ nhân viên phụ trách."),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not tell customer {CustomerId} that their file key changed.", me.CustomerId);
        }
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static EmailMessage Message(string to, string subject, string text) =>
        new(to, subject, text, $"<p style=\"font-family:Arial,sans-serif;font-size:14px\">{WebUtility.HtmlEncode(text)}</p>");
}
