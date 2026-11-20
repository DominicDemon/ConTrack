using System.Data;
using System.Security.Cryptography;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

internal static class ScanTransactionSupport
{
    internal static Task ExecuteInTransactionAsync(
        this MasterDbContext db,
        Func<Task> action,
        CancellationToken cancellationToken = default,
        IsolationLevel? isolationLevel = null) =>
        db.ExecuteInTransactionAsync<object?>(
            async () =>
            {
                await action();
                return null;
            },
            cancellationToken,
            isolationLevel);

    internal static async Task<T> ExecuteInTransactionAsync<T>(
        this MasterDbContext db,
        Func<Task<T>> action,
        CancellationToken cancellationToken = default,
        IsolationLevel? isolationLevel = null) =>
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = isolationLevel.HasValue
                ? await db.Database.BeginTransactionAsync(isolationLevel.Value, cancellationToken)
                : await db.Database.BeginTransactionAsync(cancellationToken);
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
}

public static class FileKeyCodes
{
    public const string KeyRequired = "FileKeyRequired";
    public const string KeyWrong = "FileKeyWrong";
    public const string KeyLocked = "FileKeyLocked";
    public const string NoKeyIssued = "FileKeyNotIssued";
    public const string NotGranted = "FileNotGranted";
}

/// <summary>
/// The company recovery PUBLIC key from a PEM file. Only the public half is ever on this server: with it the API
/// wraps every new file key for recovery but cannot unwrap any. Read on first use, so a machine that does not use
/// the scan features needs no key; uploading a scan without one is refused rather than stored unprotected.
/// </summary>
public sealed class PemRecoveryPublicKey : IRecoveryPublicKey
{
    private readonly Lazy<(byte[] PublicKey, string KeyId)> _key;

    private readonly Lazy<string?> _privateKeyPem;

    public PemRecoveryPublicKey(IOptions<FileEncryptionOptions> options, IHostEnvironment environment)
    {
        // On a developer machine with nothing configured, a key pair is made once next to the scans, so the
        // feature works after a plain checkout. Never outside Development: there the key is the company's to make and keep.
        var publicPath = options.Value.RecoveryPublicKeyPath;
        var privatePath = options.Value.RecoveryPrivateKeyPath;
        if (string.IsNullOrWhiteSpace(publicPath) && environment.IsDevelopment())
        {
            var folder = Path.GetFullPath(Path.Combine("..", "private-storage", "dev-keys"), environment.ContentRootPath);
            publicPath = Path.Combine(folder, "recovery-public.pem");
            privatePath = Path.Combine(folder, "recovery-private.pem");
            UsesDevelopmentKey = true;
        }

        // PublicationOnly: a missing file is not remembered, so the key is found once an administrator puts it in place.
        _key = new Lazy<(byte[], string)>(() =>
        {
            if (UsesDevelopmentKey) EnsureDevelopmentPair(publicPath, privatePath);
            return Load(publicPath, environment.ContentRootPath);
        }, LazyThreadSafetyMode.PublicationOnly);
        // The public key first: it makes the developer pair, and the private key is checked against it.
        _privateKeyPem = new Lazy<string?>(() =>
        {
            _ = _key.Value;
            return LoadPrivate(privatePath, environment.ContentRootPath);
        }, LazyThreadSafetyMode.PublicationOnly);
    }

    /// <summary>The pair was generated for this developer machine (no FileEncryption:RecoveryPublicKeyPath, Development).</summary>
    public bool UsesDevelopmentKey { get; }

    private static readonly object DevelopmentPairLock = new();

    private static void EnsureDevelopmentPair(string publicPath, string privatePath)
    {
        lock (DevelopmentPairLock)
        {
            if (File.Exists(publicPath) && File.Exists(privatePath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(publicPath)!);
            using var rsa = RSA.Create(3072);
            File.WriteAllText(privatePath, rsa.ExportPkcs8PrivateKeyPem());
            File.WriteAllText(publicPath, rsa.ExportSubjectPublicKeyInfoPem());
        }
    }

    public byte[] PublicKey => _key.Value.PublicKey;

    public string KeyId => _key.Value.KeyId;

    /// <summary>
    /// The private half, when FileEncryption:RecoveryPrivateKeyPath names it: then a reissued key gets its old files
    /// back at once. It must match the public key.
    /// </summary>
    public RSA? CreatePrivateKey()
    {
        if (_privateKeyPem.Value is not { } pem)
        {
            return null;
        }

        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return rsa;
    }

    private string? LoadPrivate(string configured, string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        var path = Path.GetFullPath(configured, contentRoot);
        if (!File.Exists(path))
        {
            throw new InternalAppException(503, "Không tìm thấy tệp khóa dự phòng riêng mà máy chủ được cấu hình. Hãy báo quản trị.");
        }

        var pem = File.ReadAllText(path);
        using var check = RSA.Create();
        check.ImportFromPem(pem);
        check.ExportParameters(includePrivateParameters: true);
        if (FileCrypto.KeyId(check.ExportSubjectPublicKeyInfo()) != KeyId)
        {
            throw new InternalAppException(503, "Khóa dự phòng riêng trên máy chủ không khớp khóa công khai. Hãy báo quản trị.");
        }

        return pem;
    }

    private static (byte[], string) Load(string configured, string contentRoot)
    {
        var path = string.IsNullOrWhiteSpace(configured) ? null : Path.GetFullPath(configured, contentRoot);
        if (path is null || !File.Exists(path))
        {
            throw new InternalAppException(503, "Máy chủ chưa cấu hình khóa dự phòng nên chưa lưu được bản scan. Hãy báo quản trị.");
        }

        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(path));
        if (rsa.KeySize < 3072)
        {
            throw new InternalAppException(503, "Khóa dự phòng phải là khóa RSA từ 3072 bit trở lên.");
        }

        var publicKey = rsa.ExportSubjectPublicKeyInfo();
        return (publicKey, FileCrypto.KeyId(publicKey));
    }
}

/// <summary>
/// The same scan is stored once. A file whose bytes are already kept as a scan (an old paper contract or a
/// paper-signing evidence) is refused with where the first copy is; a copy of a rejected evidence may be sent again.
/// </summary>
internal static class ScanDuplicates
{
    public static string Hash(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    public static async Task EnsureNewAsync(MasterDbContext db, string fileName, string sha256, CancellationToken cancellationToken)
    {
        var copies = await db.FileStorages.AsNoTracking()
            .Where(file => file.FileStorageSha256 == sha256
                && (file.FileStorageStoragekey.StartsWith("scanned-contracts/") || file.FileStorageStoragekey.StartsWith("signed-evidence/")))
            .OrderBy(file => file.FileStorageFileid)
            .Select(file => new { file.FileStorageFileid, file.FileStorageFilename, file.FileStorageUploadeddate, file.FileStorageUploadedbyemployeeid })
            .ToListAsync(cancellationToken);
        if (copies.Count == 0)
        {
            return;
        }

        var ids = copies.Select(copy => copy.FileStorageFileid).ToList();
        var rejected = await (
                from documentFile in db.ContractDocumentFiles.AsNoTracking()
                join evidence in db.ContractSignedEvidences.AsNoTracking() on documentFile.ContractDocumentFileId equals evidence.ContractDocumentFileId
                where ids.Contains(documentFile.FileStorageFileid)
                    && evidence.ContractSignedEvidenceStatus == (int)ContractManagement.Domain.Internal.Scans.SignedEvidenceStatus.Rejected
                select documentFile.FileStorageFileid)
            .ToListAsync(cancellationToken);
        var first = copies.FirstOrDefault(copy => !rejected.Contains(copy.FileStorageFileid));
        if (first is null)
        {
            return;
        }

        var uploader = await db.Employees.AsNoTracking()
            .Where(employee => employee.EmployeeId == first.FileStorageUploadedbyemployeeid)
            .Select(employee => employee.EmployeeDisplayName)
            .FirstOrDefaultAsync(cancellationToken);
        // Dates are shown in Vietnam time, like the emails of the system.
        throw new InternalAppException(409,
            $"Tệp \"{fileName}\" đã có trong hệ thống: tải lên ngày {first.FileStorageUploadeddate.AddHours(7):dd/MM/yyyy}"
            + (uploader is null ? "" : $" bởi {uploader}")
            + $" (tên tệp \"{first.FileStorageFilename}\"). Không lưu thêm bản trùng; hãy tìm bản đã có trong Tìm kiếm.");
    }
}

public static class ScanRegistration
{
    public static IServiceCollection AddInternalScans(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FileStoreOptions>(configuration.GetSection(FileStoreOptions.SectionName));
        services.AddSingleton<IFileStore, LocalFileStore>();
        services.Configure<OcrStagingOptions>(configuration.GetSection(OcrStagingOptions.SectionName));
        services.AddSingleton<IOcrStagingStore, LocalOcrStagingStore>();
        services.Configure<FileEncryptionOptions>(configuration.GetSection(FileEncryptionOptions.SectionName));
        services.AddSingleton<IRecoveryPublicKey, PemRecoveryPublicKey>();
        services.AddSingleton<FileKeyAttemptTracker>();
        services.AddScoped<FileProtectionService>();
        services.AddSingleton<FileKeyCodeStore>();
        services.AddScoped<FileKeyOwnerService>();

        services.AddOptions<ScanOcrOptions>().Bind(configuration.GetSection(ScanOcrOptions.SectionName));
        services.AddSingleton<IScanOcrEngine, TesseractScanOcrEngine>();
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // PDFium ships native builds for these server platforms only.
            services.AddSingleton<IScanPageRasterizer, PdfiumScanPageRasterizer>();
        }

        services.AddScoped<IScanKeywordReader, ContractScanKeywordReader>();
        services.AddScoped<SignedEvidenceService>();
        services.AddScoped<ScannedContractService>();
        services.AddScoped<ScanSearchService>();
        services.Configure<ScanEmailOptions>(configuration.GetSection(ScanEmailOptions.SectionName));
        services.AddSingleton<ScanEmailQueue>();
        services.AddHostedService(provider => provider.GetRequiredService<ScanEmailQueue>());
        services.AddScoped<ScanEmailService>();
        services.AddScoped<ScanKeywordJobRunner>();
        services.AddHostedService<ScanKeywordJobWorker>();
        return services;
    }
}
