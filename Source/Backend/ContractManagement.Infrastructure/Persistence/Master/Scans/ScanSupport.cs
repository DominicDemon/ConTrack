using System.Security.Cryptography;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

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
        _key = new Lazy<(byte[], string)>(() => Load(options.Value.RecoveryPublicKeyPath, environment.ContentRootPath));
        _privateKeyPem = new Lazy<string?>(() => LoadPrivate(options.Value.RecoveryPrivateKeyPath, environment.ContentRootPath));
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

public static class ScanRegistration
{
    public static IServiceCollection AddInternalScans(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped(_ => new ScanMasterDbContext(new DbContextOptionsBuilder<MasterDbContext>()
            .UseSqlServer(configuration.GetConnectionString("MasterDatabase"))
            .Options));
        services.Configure<FileStoreOptions>(configuration.GetSection(FileStoreOptions.SectionName));
        services.AddSingleton<IFileStore, LocalFileStore>();
        services.Configure<OcrStagingOptions>(configuration.GetSection(OcrStagingOptions.SectionName));
        services.AddSingleton<IOcrStagingStore, LocalOcrStagingStore>();
        services.Configure<FileEncryptionOptions>(configuration.GetSection(FileEncryptionOptions.SectionName));
        services.AddSingleton<IRecoveryPublicKey, PemRecoveryPublicKey>();
        services.AddSingleton<FileKeyAttemptTracker>();
        services.AddScoped<FileProtectionService>();

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
        services.AddScoped<ScanKeywordJobRunner>();
        services.AddHostedService<ScanKeywordJobWorker>();
        return services;
    }
}
