using ContractManagement.API.Domains.Email;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal.Scans;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

/// <summary><see cref="Manual"/>: typed by the customer; it stays when OCR reads the document again.</summary>
public sealed record CustomerVaultKeywordDto(string Id, string Type, string Value, bool Manual);

/// <summary><see cref="DaysLeft"/>: days until <see cref="ExpiresOn"/> (yyyy-mm-dd), negative once it passed.</summary>
public sealed record CustomerVaultItemDto(
    string Id, string Title, string? Note, string? Category, string FileName, string ContentType, long SizeBytes, DateTime UploadedAt,
    string OcrStatus, int? PageCount, IReadOnlyList<CustomerVaultKeywordDto> Keywords, IReadOnlyList<string> SameContractCode,
    string? ExpiresOn, int? RemindDays, int? DaysLeft, DateTime? TrashedAt);

public sealed record CustomerVaultUsageDto(long UsedBytes, long LimitBytes, int Files, int InTrash);

public sealed record CustomerVaultContent(string FileName, string ContentType, byte[] Content);

public sealed record CustomerVaultEdit(string? Title, string? Note, string? Category, string? ExpiresOn, int? RemindDays);

/// <summary>
/// A customer's own document store on the portal (022, 023): the customer uses the company's service to scan and keep
/// their contracts and papers, also ones with other parties. Each file is encrypted for the customer's key (and the
/// company recovery key); no employee key gets it, so staff cannot open the vault, and its keywords are kept apart from
/// the staff scan keywords. Deleting moves a document to the bin for <see cref="TrashDays"/> days. Every customer has
/// <see cref="LimitBytes"/>, the bin included.
/// </summary>
public sealed class CustomerVaultService(
    MasterDbContext db,
    IFileStore files,
    FileProtectionService protection,
    CustomerScanService scans,
    CustomerVaultOcrQueue ocr,
    CustomerVaultEmailLimit emailLimit,
    IEmailTransport email,
    IOptions<SmtpEmailOptions> smtp)
{
    public const long LimitBytes = 1L * 1024 * 1024 * 1024;
    public const long MaxFileBytes = 20L * 1024 * 1024;
    public const int TrashDays = 30;
    public const int DefaultRemindDays = 30;
    public const int Manual = 2;

    /// <summary>Folders offered to every customer; their own ones are added.</summary>
    public static readonly string[] DefaultCategories =
        ["Hợp đồng mua bán", "Hợp đồng dịch vụ", "Hợp đồng thuê", "Hợp đồng lao động", "Giấy phép, chứng từ", "Khác"];

    public async Task<CustomerVaultUsageDto> UsageAsync(long customerId, CancellationToken cancellationToken)
    {
        var rows = await Stored(customerId)
            .Join(db.FileStorages, v => v.FileStorageFileid, f => f.FileStorageFileid, (v, f) => new { f.FileStorageFilesize, v.TrashedAt })
            .ToListAsync(cancellationToken);
        return new CustomerVaultUsageDto(rows.Sum(r => r.FileStorageFilesize), LimitBytes, rows.Count(r => r.TrashedAt == null), rows.Count(r => r.TrashedAt != null));
    }

    public async Task<IReadOnlyList<string>> CategoriesAsync(long customerId, CancellationToken cancellationToken)
    {
        var own = await Stored(customerId).Where(v => v.CustomerVaultFileCategory != null)
            .Select(v => v.CustomerVaultFileCategory!).Distinct().ToListAsync(cancellationToken);
        return DefaultCategories.Concat(own.Order()).Distinct().ToList();
    }

    /// <summary>
    /// Documents in use (or in the bin), newest first. <paramref name="query"/> matches the title, note, file name or any
    /// keyword; <paramref name="from"/> and <paramref name="to"/> are days (yyyy-mm-dd) of upload.
    /// </summary>
    public async Task<IReadOnlyList<CustomerVaultItemDto>> ListAsync(
        long customerId, string? query, string? from, string? to, string? category, bool trash, CancellationToken cancellationToken)
    {
        var rows = Stored(customerId).Where(v => trash ? v.TrashedAt != null : v.TrashedAt == null)
            .Join(db.FileStorages, v => v.FileStorageFileid, f => f.FileStorageFileid, (v, f) => new { v, f });
        if (Day(from) is { } start) rows = rows.Where(r => r.v.CreatedAt >= start.AddHours(-7));
        if (Day(to) is { } end) rows = rows.Where(r => r.v.CreatedAt < end.AddDays(1).AddHours(-7));
        if (!string.IsNullOrWhiteSpace(category)) rows = rows.Where(r => r.v.CustomerVaultFileCategory == category.Trim());
        var term = query?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var digits = new string(term.Where(char.IsAsciiDigit).ToArray());
            var lower = term.ToLowerInvariant();
            var upper = VietnameseText.StripDiacritics(term).ToUpperInvariant();
            rows = rows.Where(r => r.v.CustomerVaultFileTitle.Contains(term) || (r.v.CustomerVaultFileNote != null && r.v.CustomerVaultFileNote.Contains(term))
                || r.f.FileStorageFilename.Contains(term)
                || db.CustomerVaultKeywords.Any(k => k.CustomerVaultFileId == r.v.CustomerVaultFileId
                    && (k.CustomerVaultKeywordValue.Contains(lower) || k.CustomerVaultKeywordValue.Contains(upper)
                        || (digits.Length >= 4 && k.CustomerVaultKeywordValue.Contains(digits)))));
        }

        var list = await rows.OrderByDescending(r => r.v.CustomerVaultFileId).Take(500).ToListAsync(cancellationToken);
        var ids = list.Select(r => r.v.CustomerVaultFileId).ToList();
        var keywords = await db.CustomerVaultKeywords.AsNoTracking().Where(k => ids.Contains(k.CustomerVaultFileId)).ToListAsync(cancellationToken);
        // Same contract number on another document in use: a document kept twice, or two contracts sharing a number.
        var codes = await db.CustomerVaultKeywords.AsNoTracking()
            .Where(k => k.CustomerVaultKeywordType == (int)ScanKeywordType.ContractCode
                && db.CustomerVaultFiles.Any(v => v.CustomerVaultFileId == k.CustomerVaultFileId && v.CustomerId == customerId && v.DeletedAt == null && v.TrashedAt == null))
            .Select(k => new { k.CustomerVaultFileId, k.CustomerVaultKeywordValue })
            .ToListAsync(cancellationToken);
        var today = Today();
        return list.Select(r =>
        {
            var own = keywords.Where(k => k.CustomerVaultFileId == r.v.CustomerVaultFileId).ToList();
            var mine = own.Where(k => k.CustomerVaultKeywordType == (int)ScanKeywordType.ContractCode).Select(k => k.CustomerVaultKeywordValue).ToHashSet();
            var twins = codes.Where(c => c.CustomerVaultFileId != r.v.CustomerVaultFileId && mine.Contains(c.CustomerVaultKeywordValue))
                .Select(c => c.CustomerVaultFileId.ToString()).Distinct().ToList();
            var expires = r.v.CustomerVaultFileExpiresOn;
            return new CustomerVaultItemDto(
                r.v.CustomerVaultFileId.ToString(), r.v.CustomerVaultFileTitle, r.v.CustomerVaultFileNote, r.v.CustomerVaultFileCategory,
                r.f.FileStorageFilename, r.f.FileStorageContenttype, r.f.FileStorageFilesize, DateTime.SpecifyKind(r.v.CreatedAt, DateTimeKind.Utc),
                r.v.CustomerVaultFileOcrStatus, r.v.CustomerVaultFilePageCount,
                own.OrderBy(k => k.CustomerVaultKeywordType).Select(k => new CustomerVaultKeywordDto(
                    k.CustomerVaultKeywordId.ToString(), ((ScanKeywordType)k.CustomerVaultKeywordType).ToString(), k.CustomerVaultKeywordValue, k.CustomerVaultKeywordSource == Manual)).ToList(),
                twins, expires?.ToString("yyyy-MM-dd"), r.v.CustomerVaultFileRemindDays,
                expires is { } e ? e.DayNumber - today.DayNumber : null,
                r.v.TrashedAt is { } t ? DateTime.SpecifyKind(t, DateTimeKind.Utc) : null);
        }).ToList();
    }

    /// <summary>Encrypts and keeps a document, then reads its keywords in the background from the plain bytes.</summary>
    public async Task<CustomerVaultItemDto> UploadAsync(
        long customerId, string fileName, byte[] content, string? title, string? note, string? category, string? expiresOn, CancellationToken cancellationToken)
    {
        if (content.Length == 0 || content.Length > MaxFileBytes)
            throw new ArgumentException("Chọn tệp có nội dung, tối đa 20 MB.");
        var (extension, contentType) = ScanFileKinds.Detect(content) is { } kind
            ? kind switch { ScanFileKind.Pdf => (".pdf", "application/pdf"), ScanFileKind.Jpeg => (".jpg", "image/jpeg"), _ => (".png", "image/png") }
            : throw new ArgumentException($"“{Path.GetFileName(fileName)}”: chỉ hỗ trợ tệp PDF, JPG hoặc PNG.");
        _ = await db.CustomerEncryptionKeys.AsNoTracking()
            .SingleOrDefaultAsync(k => k.CustomerId == customerId && k.CustomerEncryptionKeyStatus == FileProtectionService.ActiveStatus, cancellationToken)
            ?? throw new InternalAppException(409, "Hãy tạo key xem bản scan trước: tài liệu trong kho được mã hóa bằng key của bạn.", FileKeyCodes.NoKeyIssued);
        var usage = await UsageAsync(customerId, cancellationToken);
        if (usage.UsedBytes + content.Length > LimitBytes)
            throw new InternalAppException(409, $"Kho lưu trữ đã dùng {usage.UsedBytes / 1048576.0:0.#} MB / 1 GB (kể cả thùng rác). Xóa hẳn bớt tài liệu để tải thêm.");
        var sha = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var twin = await Stored(customerId)
            .Join(db.FileStorages, v => v.FileStorageFileid, f => f.FileStorageFileid, (v, f) => new { v.CustomerVaultFileTitle, v.TrashedAt, f.FileStorageSha256 })
            .FirstOrDefaultAsync(r => r.FileStorageSha256 == sha, cancellationToken);
        if (twin is not null)
            throw new InternalAppException(409, $"“{Path.GetFileName(fileName)}” đã có trong kho của bạn (“{twin.CustomerVaultFileTitle}”{(twin.TrashedAt is null ? "" : ", trong thùng rác")}).");

        var safeName = Path.GetFileName(fileName);
        var cleanTitle = Clean(title, 300) ?? Path.GetFileNameWithoutExtension(safeName);
        var expires = ParseExpiry(expiresOn);
        var (stored, fileKey) = protection.Protect(content);
        var storageKey = $"customer-vault/{customerId}/{DateTime.UtcNow:yyyyMM}/{Guid.NewGuid():N}{extension}{(fileKey is null ? "" : ".enc")}";
        await files.SaveAsync(storageKey, stored, cancellationToken);
        CustomerVaultFile row;
        try
        {
            var now = DateTime.UtcNow;
            // Inside the execution strategy: a retrying SQL connection does not allow a transaction opened by hand.
            row = await db.ExecuteInTransactionAsync(async () =>
            {
                db.ChangeTracker.Clear();
                var file = new FileStorage
                {
                    FileStorageObjectid = customerId, FileStorageObjecttype = "CUSTOMER_VAULT", FileStorageFiletype = extension[1..].ToUpperInvariant(),
                    FileStorageFilename = safeName, FileStorageFilepath = storageKey, FileStorageStoragekey = storageKey,
                    FileStorageContenttype = contentType, FileStorageFilesize = content.LongLength, FileStorageSha256 = sha,
                    FileStorageUploadedbycustomerid = customerId, FileStorageUploadeddate = now,
                };
                db.FileStorages.Add(file);
                await db.SaveChangesAsync(cancellationToken);
                // The recovery copy and the customer's key only: no employee is given this file.
                await protection.GrantForCustomerUploadAsync(file.FileStorageFileid, fileKey, [], cancellationToken);
                if (fileKey is not null) await CustomerScans.GrantToCustomerAsync(db, file.FileStorageFileid, customerId, fileKey, cancellationToken);
                var created = new CustomerVaultFile
                {
                    CustomerId = customerId, FileStorageFileid = file.FileStorageFileid, CustomerVaultFileTitle = cleanTitle,
                    CustomerVaultFileNote = Clean(note, 1000), CustomerVaultFileCategory = Clean(category, 100),
                    CustomerVaultFileExpiresOn = expires, CustomerVaultFileOcrStatus = "QUEUED", CreatedAt = now,
                };
                db.CustomerVaultFiles.Add(created);
                ReviewData.Audit(db, "CUSTOMER_VAULT_UPLOADED", "FILE_STORAGE", file.FileStorageFileid, "CUSTOMER", customerId, now);
                await db.SaveChangesAsync(cancellationToken);
                return created;
            }, cancellationToken);
            if (fileKey is not null) CryptographicOperations.ZeroMemory(fileKey);
        }
        catch
        {
            await files.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }

        ocr.Enqueue(row.CustomerVaultFileId, content);
        return (await ListAsync(customerId, null, null, null, null, false, cancellationToken)).First(i => i.Id == row.CustomerVaultFileId.ToString());
    }

    /// <summary>Title, note, folder, end date and reminder; a new end date starts its reminders again.</summary>
    public async Task UpdateAsync(long customerId, long id, CustomerVaultEdit edit, CancellationToken cancellationToken)
    {
        var row = await Own(customerId, id, cancellationToken);
        row.CustomerVaultFileTitle = Clean(edit.Title, 300) ?? throw new ArgumentException("Nhập tên tài liệu.");
        row.CustomerVaultFileNote = Clean(edit.Note, 1000);
        row.CustomerVaultFileCategory = Clean(edit.Category, 100);
        var expires = ParseExpiry(edit.ExpiresOn);
        if (edit.RemindDays is < 1 or > 365) throw new ArgumentException("Nhắc trước từ 1 đến 365 ngày.");
        if (expires != row.CustomerVaultFileExpiresOn || edit.RemindDays != row.CustomerVaultFileRemindDays) row.CustomerVaultFileRemindedDays = null;
        row.CustomerVaultFileExpiresOn = expires;
        row.CustomerVaultFileRemindDays = expires is null ? null : edit.RemindDays;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A keyword the customer types: contract numbers of other parties often follow no pattern OCR knows.</summary>
    public async Task AddKeywordAsync(long customerId, long id, string? type, string? value, CancellationToken cancellationToken)
    {
        var row = await Own(customerId, id, cancellationToken);
        if (!Enum.TryParse<ScanKeywordType>(type, out var kind) || !Enum.IsDefined(kind)) throw new ArgumentException("Chọn loại thông tin.");
        var typed = (value ?? "").Trim();
        var normalised = ContractScanKeywordExtractor.NormaliseTyped(kind, typed)
            ?? (kind == ScanKeywordType.ContractCode && typed.Length is > 0 and <= 100 ? System.Text.RegularExpressions.Regex.Replace(VietnameseText.StripDiacritics(typed).ToUpperInvariant(), @"^(SO|NO\.?)(\s+|\s*:\s*)(?=\S)", "").Replace(" ", "") : null)
            ?? throw new ArgumentException(kind switch
            {
                ScanKeywordType.TaxCode => "Mã số thuế gồm 10 hoặc 13 chữ số.",
                ScanKeywordType.PhoneNumber => "Số điện thoại không hợp lệ.",
                ScanKeywordType.Email => "Email không hợp lệ.",
                _ => "Giá trị không hợp lệ.",
            });
        if (await db.CustomerVaultKeywords.AnyAsync(k => k.CustomerVaultFileId == row.CustomerVaultFileId && k.CustomerVaultKeywordType == (int)kind
                && k.CustomerVaultKeywordValue == normalised, cancellationToken))
            throw new InternalAppException(409, "Tài liệu đã có thông tin này.");
        db.CustomerVaultKeywords.Add(new CustomerVaultKeyword
        {
            CustomerVaultFileId = row.CustomerVaultFileId, CustomerVaultKeywordType = (int)kind, CustomerVaultKeywordValue = normalised, CustomerVaultKeywordSource = Manual,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Removes a keyword, read by OCR or typed: a wrong reading is the customer's to correct.</summary>
    public async Task RemoveKeywordAsync(long customerId, long id, long keywordId, CancellationToken cancellationToken)
    {
        var row = await Own(customerId, id, cancellationToken);
        var removed = await db.CustomerVaultKeywords
            .Where(k => k.CustomerVaultKeywordId == keywordId && k.CustomerVaultFileId == row.CustomerVaultFileId)
            .ExecuteDeleteAsync(cancellationToken);
        if (removed == 0) throw new InternalAppException(404, "Không tìm thấy thông tin này.");
    }

    /// <summary>To the bin: the file stays (and counts in the quota) for <see cref="TrashDays"/> days.</summary>
    public async Task TrashAsync(long customerId, long id, CancellationToken cancellationToken)
    {
        var row = await Own(customerId, id, cancellationToken);
        row.TrashedAt = DateTime.UtcNow;
        ReviewData.Audit(db, "CUSTOMER_VAULT_TRASHED", "FILE_STORAGE", row.FileStorageFileid, "CUSTOMER", customerId, row.TrashedAt.Value);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(long customerId, long id, CancellationToken cancellationToken)
    {
        var row = await InTrash(customerId, id, cancellationToken);
        row.TrashedAt = null;
        ReviewData.Audit(db, "CUSTOMER_VAULT_RESTORED", "FILE_STORAGE", row.FileStorageFileid, "CUSTOMER", customerId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Removes a document in the bin for good, at once.</summary>
    public async Task PurgeAsync(long customerId, long id, CancellationToken cancellationToken) =>
        await Purge(db, files, await InTrash(customerId, id, cancellationToken), customerId, cancellationToken);

    /// <summary>The file leaves the disk (freeing the quota); the row stays, marked, for the audit.</summary>
    internal static async Task Purge(MasterDbContext db, IFileStore files, CustomerVaultFile row, long? actor, CancellationToken cancellationToken)
    {
        var key = await db.FileStorages.Where(f => f.FileStorageFileid == row.FileStorageFileid).Select(f => f.FileStorageStoragekey).SingleAsync(cancellationToken);
        row.DeletedAt = DateTime.UtcNow;
        ReviewData.Audit(db, "CUSTOMER_VAULT_DELETED", "FILE_STORAGE", row.FileStorageFileid, actor is null ? "SYSTEM" : "CUSTOMER", actor ?? 0, row.DeletedAt.Value);
        await db.SaveChangesAsync(cancellationToken);
        await files.DeleteAsync(key, CancellationToken.None);
    }

    public async Task<CustomerVaultContent> OpenAsync(long customerId, long id, string? userKey, CancellationToken cancellationToken)
    {
        var (row, file, plain) = await ReadAsync(customerId, id, userKey, cancellationToken);
        ReviewData.Audit(db, "CUSTOMER_VAULT_OPENED", "FILE_STORAGE", row.FileStorageFileid, "CUSTOMER", customerId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return new CustomerVaultContent(file.FileStorageFilename, file.FileStorageContenttype, plain);
    }

    /// <summary>Reads the keywords again (after a restart cut the reading off, or to retry): needs the key to open the file.</summary>
    public async Task RereadAsync(long customerId, long id, string? userKey, CancellationToken cancellationToken)
    {
        var (row, _, plain) = await ReadAsync(customerId, id, userKey, cancellationToken);
        row.CustomerVaultFileOcrStatus = "QUEUED";
        await db.SaveChangesAsync(cancellationToken);
        ocr.Enqueue(row.CustomerVaultFileId, plain);
    }

    /// <summary>Sends the document as an attachment; replies go to the customer. At most a few addresses an hour.</summary>
    public async Task<IReadOnlyList<string>> EmailAsync(long customerId, long id, string? userKey, IReadOnlyList<string?>? recipients, string? note, CancellationToken cancellationToken)
    {
        var addresses = ScanEmail.Recipients(recipients);
        var cleanNote = ScanEmail.Note(note);
        if (!smtp.Value.IsConfigured()) throw new InternalAppException(503, "Hệ thống chưa gửi được email lúc này. Vui lòng thử lại sau.");
        var (row, file, plain) = await ReadAsync(customerId, id, userKey, cancellationToken);
        if (plain.Length > ScanEmail.MaxAttachmentBytes) throw new InternalAppException(413, "Tệp quá lớn để gửi qua email (tối đa 18 MB).");
        var me = await db.Customers.AsNoTracking().Where(c => c.CustomerId == customerId)
            .Select(c => new { c.CustomerDisplayName, c.CustomerEmail }).SingleAsync(cancellationToken);
        emailLimit.Reserve(customerId, addresses.Count);
        var content = new ScanEmailContent(row.CustomerVaultFileTitle, [("Tài liệu", row.CustomerVaultFileTitle)], file.FileStorageFilename, file.FileStorageContenttype, plain);
        var sent = new List<string>();
        foreach (var address in addresses)
        {
            await email.SendAsync(ScanEmail.Compose(address, content, me.CustomerDisplayName, cleanNote, me.CustomerEmail), cancellationToken);
            sent.Add(address);
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActionType = "CUSTOMER_VAULT_EMAILED", EntityName = "FILE_STORAGE", EntityId = row.FileStorageFileid.ToString(),
            ActorType = "CUSTOMER", ActorId = customerId, CreatedAt = DateTime.UtcNow,
            NewData = System.Text.Json.JsonSerializer.Serialize(new { sent, withNote = cleanNote is not null }),
        });
        await db.SaveChangesAsync(cancellationToken);
        return sent;
    }

    private async Task<(CustomerVaultFile Row, FileStorage File, byte[] Plain)> ReadAsync(long customerId, long id, string? userKey, CancellationToken cancellationToken)
    {
        var row = await Own(customerId, id, cancellationToken);
        var file = await db.FileStorages.AsNoTracking().SingleAsync(f => f.FileStorageFileid == row.FileStorageFileid, cancellationToken);
        var stored = await files.ReadAsync(file.FileStorageStoragekey, cancellationToken)
            ?? throw new InternalAppException(404, "Không tìm thấy tệp trên máy chủ.");
        return (row, file, await scans.DecryptForCustomerAsync(customerId, row.FileStorageFileid, stored, userKey, cancellationToken));
    }

    /// <summary>Documents still on disk: in use or in the bin.</summary>
    private IQueryable<CustomerVaultFile> Stored(long customerId) =>
        db.CustomerVaultFiles.AsNoTracking().Where(v => v.CustomerId == customerId && v.DeletedAt == null);

    private async Task<CustomerVaultFile> Own(long customerId, long id, CancellationToken cancellationToken) =>
        await db.CustomerVaultFiles.SingleOrDefaultAsync(v => v.CustomerVaultFileId == id && v.CustomerId == customerId && v.DeletedAt == null && v.TrashedAt == null, cancellationToken)
        ?? throw new InternalAppException(404, "Không tìm thấy tài liệu trong kho của bạn.");

    private async Task<CustomerVaultFile> InTrash(long customerId, long id, CancellationToken cancellationToken) =>
        await db.CustomerVaultFiles.SingleOrDefaultAsync(v => v.CustomerVaultFileId == id && v.CustomerId == customerId && v.DeletedAt == null && v.TrashedAt != null, cancellationToken)
        ?? throw new InternalAppException(404, "Không tìm thấy tài liệu trong thùng rác.");

    private static string? Clean(string? value, int max)
    {
        var text = string.Join(' ', (value ?? "").Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return text.Length == 0 ? null : text.Length > max ? text[..max] : text;
    }

    private static DateOnly? ParseExpiry(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) && day.Year is >= 1990 and <= 2200
            ? day
            : throw new ArgumentException("Ngày hết hạn không hợp lệ.");
    }

    private static DateTime? Day(string? value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    /// <summary>Today in Vietnam (UTC+7), as the customer counts days.</summary>
    internal static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
}

/// <summary>Addresses a customer may email from the vault: 20 an hour.</summary>
public sealed class CustomerVaultEmailLimit
{
    public const int PerHour = 20;
    private readonly ConcurrentDictionary<long, List<DateTimeOffset>> _sent = new();

    public void Reserve(long customerId, int count)
    {
        var list = _sent.GetOrAdd(customerId, _ => []);
        lock (list)
        {
            var now = DateTimeOffset.UtcNow;
            list.RemoveAll(at => now - at > TimeSpan.FromHours(1));
            if (list.Count + count > PerHour)
                throw new InternalAppException(429, $"Mỗi giờ gửi tối đa {PerHour} email. Bạn còn {Math.Max(0, PerHour - list.Count)} lượt.");
            for (var i = 0; i < count; i++) list.Add(now);
        }
    }
}

/// <summary>
/// Reads the keywords of vault documents in the background, from the plain bytes handed over at upload (the server
/// cannot open the file later without the customer's key). In memory: a restart leaves the reading INTERRUPTED, and
/// the customer reads it again with their key. Keywords the customer typed are kept.
/// </summary>
public sealed class CustomerVaultOcrQueue(IServiceScopeFactory scopes, ILogger<CustomerVaultOcrQueue> logger) : BackgroundService
{
    private readonly Channel<(long Id, byte[] Content)> _queue = Channel.CreateUnbounded<(long, byte[])>();

    public void Enqueue(long vaultFileId, byte[] content) => _queue.Writer.TryWrite((vaultFileId, content));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
            await db.CustomerVaultFiles.Where(v => v.CustomerVaultFileOcrStatus == "QUEUED" || v.CustomerVaultFileOcrStatus == "PROCESSING")
                .ExecuteUpdateAsync(set => set.SetProperty(v => v.CustomerVaultFileOcrStatus, "INTERRUPTED"), stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not mark interrupted vault readings (are 022 and 023 applied?).");
        }

        await foreach (var (id, content) in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
            try
            {
                await db.CustomerVaultFiles.Where(v => v.CustomerVaultFileId == id)
                    .ExecuteUpdateAsync(set => set.SetProperty(v => v.CustomerVaultFileOcrStatus, "PROCESSING"), stoppingToken);
                var result = await scope.ServiceProvider.GetRequiredService<IScanKeywordReader>().ReadAsync(content, stoppingToken);
                await db.CustomerVaultKeywords.Where(k => k.CustomerVaultFileId == id && k.CustomerVaultKeywordSource != CustomerVaultService.Manual)
                    .ExecuteDeleteAsync(stoppingToken);
                var typed = await db.CustomerVaultKeywords.AsNoTracking().Where(k => k.CustomerVaultFileId == id)
                    .Select(k => new { k.CustomerVaultKeywordType, k.CustomerVaultKeywordValue }).ToListAsync(stoppingToken);
                foreach (var keyword in result.Keywords.DistinctBy(k => (k.Type, k.Value)))
                {
                    var value = keyword.Value.Length > 400 ? keyword.Value[..400] : keyword.Value;
                    if (typed.Any(t => t.CustomerVaultKeywordType == (int)keyword.Type && t.CustomerVaultKeywordValue == value)) continue;
                    db.CustomerVaultKeywords.Add(new CustomerVaultKeyword
                    {
                        CustomerVaultFileId = id, CustomerVaultKeywordType = (int)keyword.Type, CustomerVaultKeywordValue = value, CustomerVaultKeywordSource = 1,
                    });
                }

                var row = await db.CustomerVaultFiles.SingleAsync(v => v.CustomerVaultFileId == id, stoppingToken);
                row.CustomerVaultFileOcrStatus = "SUCCESS";
                row.CustomerVaultFilePageCount = result.PageCount;
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Reading vault document {Id} failed.", id);
                try
                {
                    await db.CustomerVaultFiles.Where(v => v.CustomerVaultFileId == id)
                        .ExecuteUpdateAsync(set => set.SetProperty(v => v.CustomerVaultFileOcrStatus, "FAILED"), CancellationToken.None);
                }
                catch (Exception inner) when (inner is not OperationCanceledException)
                {
                    logger.LogWarning(inner, "Could not mark vault document {Id} as failed.", id);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(content);
            }
        }
    }
}

/// <summary>
/// Every few hours: empties the bin of documents older than <see cref="CustomerVaultService.TrashDays"/> days, and
/// emails customers whose contracts are about to end (at their chosen number of days, then 7 days, then on the day).
/// Each step is sent once per end date.
/// </summary>
public sealed class CustomerVaultMaintenance(IServiceScopeFactory scopes, ILogger<CustomerVaultMaintenance> logger) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Vault maintenance failed; it runs again later.");
            }

            await Task.Delay(Every, stoppingToken);
        }
    }

    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var files = scope.ServiceProvider.GetRequiredService<IFileStore>();
        var cutoff = DateTime.UtcNow.AddDays(-CustomerVaultService.TrashDays);
        foreach (var row in await db.CustomerVaultFiles.Where(v => v.DeletedAt == null && v.TrashedAt != null && v.TrashedAt < cutoff).ToListAsync(cancellationToken))
        {
            await CustomerVaultService.Purge(db, files, row, null, cancellationToken);
        }

        var smtp = scope.ServiceProvider.GetRequiredService<IOptions<SmtpEmailOptions>>().Value;
        if (!smtp.IsConfigured()) return;
        var email = scope.ServiceProvider.GetRequiredService<IEmailTransport>();
        var today = CustomerVaultService.Today();
        var due = await (
                from v in db.CustomerVaultFiles
                join c in db.Customers on v.CustomerId equals c.CustomerId
                where v.DeletedAt == null && v.TrashedAt == null && v.CustomerVaultFileExpiresOn != null && c.CustomerStatus == 1 && c.CustomerEmail != null
                select new { v, c.CustomerEmail, c.CustomerDisplayName })
            .ToListAsync(cancellationToken);
        foreach (var item in due)
        {
            var left = item.v.CustomerVaultFileExpiresOn!.Value.DayNumber - today.DayNumber;
            // Steps: the customer's chosen days, 7 days, the day itself. The smallest one reached is the one to send.
            var step = new[] { item.v.CustomerVaultFileRemindDays ?? CustomerVaultService.DefaultRemindDays, 7, 0 }
                .Distinct().Where(d => left <= d).DefaultIfEmpty(int.MinValue).Min();
            if (step == int.MinValue || left < -1 || (item.v.CustomerVaultFileRemindedDays is { } sent && sent <= step)) continue;
            if (!SmtpEmailOptions.IsEmailAddress(item.CustomerEmail)) continue;
            var end = item.v.CustomerVaultFileExpiresOn.Value.ToString("dd/MM/yyyy");
            var text = left > 0
                ? $"Tài liệu “{item.v.CustomerVaultFileTitle}” trong kho lưu trữ của quý khách hết hạn ngày {end} (còn {left} ngày)."
                : $"Tài liệu “{item.v.CustomerVaultFileTitle}” trong kho lưu trữ của quý khách hết hạn ngày {end}.";
            try
            {
                await email.SendAsync(new EmailMessage(item.CustomerEmail!, "Nhắc hạn hợp đồng trong kho lưu trữ",
                    text + " Đăng nhập cổng khách hàng, mục Kho lưu trữ, để xem lại.",
                    $"<p style=\"font-family:Arial,sans-serif;font-size:14px\">{WebUtility.HtmlEncode(text)} Đăng nhập cổng khách hàng, mục Kho lưu trữ, để xem lại.</p>"), cancellationToken);
                item.v.CustomerVaultFileRemindedDays = step;
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Could not remind customer {CustomerId} about vault document {Id}.", item.v.CustomerId, item.v.CustomerVaultFileId);
            }
        }
    }
}
