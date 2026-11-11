using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Infrastructure.Persistence.Master.Scans;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.API.Internal;

/// <summary>
/// Administrator commands for the company recovery key, run on a trusted machine, not by the web API:
///   ApplicationMode=Internal: dotnet run --project ContractManagement.API -- generate-recovery-key &lt;folder&gt;
///     makes recovery-public.pem (set FileEncryption:RecoveryPublicKeyPath to it) and recovery-private.pem
///     (keep it off the API server: a safe, a USB key in a locked drawer, or the company key vault).
///   ApplicationMode=Internal: dotnet run --project ContractManagement.API -- recovery-grant &lt;recovery-private.pem&gt; &lt;employeeId&gt; [fileId]
///     lets the employee's current key open every file (or one file) kept with the recovery key, e.g. after the
///     employee lost their key and was issued a new one. Each grant is written to AUDIT_LOG.
/// </summary>
public static class RecoveryCommands
{
    public static bool IsCommand(string[] args) =>
        args.Length > 0 && args[0] is "generate-recovery-key" or "recovery-grant";

    public static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        try
        {
            return args[0] switch
            {
                "generate-recovery-key" => Generate(args),
                _ => await GrantAsync(args, services),
            };
        }
        catch (Exception exception) when (exception is InvalidOperationException or CryptographicException or IOException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static int Generate(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: generate-recovery-key <folder>");
            return 2;
        }

        var folder = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(folder);
        var publicPath = Path.Combine(folder, "recovery-public.pem");
        var privatePath = Path.Combine(folder, "recovery-private.pem");
        if (File.Exists(publicPath) || File.Exists(privatePath))
        {
            throw new InvalidOperationException($"{folder} already holds a recovery key; it is not overwritten.");
        }

        using var rsa = RSA.Create(4096);
        File.WriteAllText(publicPath, rsa.ExportSubjectPublicKeyInfoPem());
        File.WriteAllText(privatePath, rsa.ExportPkcs8PrivateKeyPem());
        Console.WriteLine($"Public key:  {publicPath}  (set FileEncryption:RecoveryPublicKeyPath to this file)");
        Console.WriteLine($"Private key: {privatePath}  (move it OFF the API server and keep it safe)");
        Console.WriteLine($"Key id: {FileCrypto.KeyId(rsa.ExportSubjectPublicKeyInfo())}");
        return 0;
    }

    private static async Task<int> GrantAsync(string[] args, IServiceProvider services)
    {
        if (args.Length < 3 || !long.TryParse(args[2], out var employeeId))
        {
            Console.Error.WriteLine("Usage: recovery-grant <recovery-private.pem> <employeeId> [fileId]");
            return 2;
        }

        long? onlyFile = args.Length > 3 && long.TryParse(args[3], out var parsed) ? parsed : null;
        using var recoveryKey = RSA.Create();
        recoveryKey.ImportFromPem(File.ReadAllText(args[1]));
        var keyId = FileCrypto.KeyId(recoveryKey.ExportSubjectPublicKeyInfo());

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ScanMasterDbContext>();
        var employeeKey = await db.EmployeeEncryptionKeys.AsNoTracking()
            .SingleOrDefaultAsync(key => key.EmployeeId == employeeId && key.EmployeeEncryptionKeyStatus == FileProtectionService.ActiveStatus)
            ?? throw new InvalidOperationException($"Employee {employeeId} has no active key: issue one first.");

        var held = await db.FileEncryptionKeys.AsNoTracking()
            .Where(row => row.EmployeeEncryptionKeyId == employeeKey.EmployeeEncryptionKeyId)
            .Select(row => row.FileStorageFileid)
            .ToListAsync();
        var recoveryRows = await db.FileEncryptionKeys.AsNoTracking()
            .Where(row => row.EmployeeEncryptionKeyId == null && row.FileEncryptionKeyRecoverykeyid == keyId)
            .Where(row => onlyFile == null || row.FileStorageFileid == onlyFile)
            .ToListAsync();
        if (recoveryRows.Count == 0)
        {
            throw new InvalidOperationException("No file is kept with this recovery key (wrong private key, or no such file).");
        }

        var now = DateTime.UtcNow;
        var granted = 0;
        foreach (var row in recoveryRows.Where(row => !held.Contains(row.FileStorageFileid)))
        {
            var fileKey = FileCrypto.Unwrap(row.FileEncryptionKeyWrappedkey, recoveryKey);
            db.FileEncryptionKeys.Add(new FileEncryptionKey
            {
                FileStorageFileid = row.FileStorageFileid,
                EmployeeEncryptionKeyId = employeeKey.EmployeeEncryptionKeyId,
                FileEncryptionKeyWrappedkey = FileCrypto.Wrap(fileKey, employeeKey.EmployeeEncryptionKeyPublickey),
                GrantedAt = now,
            });
            CryptographicOperations.ZeroMemory(fileKey);
            db.AuditLogs.Add(new AuditLog
            {
                ActionType = "EncryptedFileGrantedWithRecoveryKey",
                EntityName = FileProtectionService.FileEntity,
                EntityId = row.FileStorageFileid.ToString(),
                ActorType = "Administrator",
                NewData = $"{{\"employeeId\":{employeeId}}}",
                CreatedAt = now,
            });
            granted++;
        }

        await db.SaveChangesAsync();
        Console.WriteLine($"Granted {granted} file(s) to employee {employeeId}; {recoveryRows.Count - granted} already open to them.");
        return 0;
    }
}
