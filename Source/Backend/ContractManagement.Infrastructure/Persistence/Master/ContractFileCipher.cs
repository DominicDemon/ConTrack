using System.Buffers.Binary;
using System.Security.Cryptography;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Infrastructure.Persistence.Master.Scans;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Persistence.Master;

/// <summary>
/// Encryption at rest of the online-contract files (contract PDFs at each signing step, Word copies of drafts and
/// templates). Unlike scans, these files are read by the server itself while signing (a signature is stamped into the
/// PDF, the customer downloads it after an OTP), so no person's 12-character key can be asked for. Each file gets
/// its own random AES-256-GCM key (<see cref="FileCrypto.EncryptFile"/>); that key is sealed with the server's
/// contract-file key (AES-256-GCM) and, when a recovery public key is set, also wrapped for the company recovery key
/// (RSA-OAEP-SHA256), so the files can be opened again if the contract-file key is lost.
/// What it protects against: the storage folder or a backup of it leaking. Whoever has both the folder and the
/// contract-file key reads the files, so keep the key out of the folder (environment, user-secrets or a key vault).
/// Layout: "CMENC2\0\0" | key id (8) | sealed file key (nonce 12 | tag 16 | key 32) | recovery length (2) | recovery wrap | CMENC1 file.
/// Files written before encryption are read as they are.
/// </summary>
public sealed class ContractFileCipher
{
    private static readonly byte[] Magic = "CMENC2\0\0"u8.ToArray();
    private const int KeyIdSize = 8, NonceSize = 12, TagSize = 16, FileKeySize = 32;
    private const int SealedKeySize = NonceSize + TagSize + FileKeySize;

    private readonly Lazy<byte[]?> _key;
    private readonly IRecoveryPublicKey? _recovery;

    public ContractFileCipher(IOptions<FileEncryptionOptions> options, IHostEnvironment environment, IRecoveryPublicKey? recovery = null)
    {
        _recovery = recovery;
        Enabled = options.Value.Enabled;
        _key = new Lazy<byte[]?>(() => LoadKey(options.Value, environment), LazyThreadSafetyMode.PublicationOnly);
    }

    /// <summary>A cipher with a given key, for tests and the migration command.</summary>
    public ContractFileCipher(byte[] key, IRecoveryPublicKey? recovery = null)
    {
        if (key.Length != FileKeySize) throw new ArgumentException("The contract-file key must be 32 bytes.");
        _recovery = recovery;
        Enabled = true;
        _key = new Lazy<byte[]?>(() => key);
    }

    public bool Enabled { get; }

    public static bool IsEncrypted(ReadOnlySpan<byte> stored) => stored.StartsWith(Magic);

    /// <summary>The bytes to write to disk. Refused (not stored in clear) when encryption is on and no key is set.</summary>
    public byte[] Protect(byte[] plain)
    {
        if (!Enabled) return plain;
        var key = _key.Value ?? throw new InvalidOperationException(
            "Máy chủ chưa cấu hình khóa mã hóa tệp hợp đồng (FileEncryption:ContractFileKey hoặc ContractFileKeyPath). Hãy báo quản trị.");
        var (cmenc1, fileKey) = FileCrypto.EncryptFile(plain);
        try
        {
            var sealedKey = Seal(fileKey, key);
            var recovery = RecoveryPublicKey() is { } publicKey ? FileCrypto.Wrap(fileKey, publicKey) : [];
            var result = new byte[Magic.Length + KeyIdSize + SealedKeySize + 2 + recovery.Length + cmenc1.Length];
            var span = result.AsSpan();
            Magic.CopyTo(span);
            KeyId(key).CopyTo(span[Magic.Length..]);
            var at = Magic.Length + KeyIdSize;
            sealedKey.CopyTo(span[at..]);
            at += SealedKeySize;
            BinaryPrimitives.WriteUInt16BigEndian(span[at..], checked((ushort)recovery.Length));
            at += 2;
            recovery.CopyTo(span[at..]);
            cmenc1.CopyTo(span[(at + recovery.Length)..]);
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(fileKey);
        }
    }

    /// <summary>The plain bytes. A file written before encryption is returned as it is.</summary>
    public byte[] Unprotect(byte[] stored)
    {
        if (!IsEncrypted(stored)) return stored;
        var key = _key.Value ?? throw new InvalidOperationException("Máy chủ chưa cấu hình khóa mã hóa tệp hợp đồng nên không mở được tệp này.");
        var (keyId, sealedKey, _, file) = Parse(stored);
        if (!keyId.SequenceEqual(KeyId(key)))
            throw new CryptographicException("Tệp hợp đồng được mã hóa bằng một khóa khác với khóa hiện tại của máy chủ.");
        var fileKey = Open(sealedKey, key);
        try { return FileCrypto.DecryptFile(file, fileKey); }
        finally { CryptographicOperations.ZeroMemory(fileKey); }
    }

    /// <summary>Opens a file with the company recovery private key (the contract-file key was lost).</summary>
    public static byte[] UnprotectWithRecovery(byte[] stored, RSA recoveryPrivateKey)
    {
        if (!IsEncrypted(stored)) return stored;
        var (_, _, recovery, file) = Parse(stored);
        if (recovery.Length == 0) throw new CryptographicException("Tệp này không có bản khóa dự phòng.");
        var fileKey = FileCrypto.Unwrap(recovery, recoveryPrivateKey);
        try { return FileCrypto.DecryptFile(file, fileKey); }
        finally { CryptographicOperations.ZeroMemory(fileKey); }
    }

    public async Task WriteAsync(string path, byte[] plain, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, Protect(plain), ct);
    }

    public async Task<byte[]> ReadAsync(string path, CancellationToken ct) => Unprotect(await File.ReadAllBytesAsync(path, ct));

    /// <summary>A new random contract-file key, base64, for FileEncryption:ContractFileKey.</summary>
    public static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(FileKeySize));

    private static (byte[] KeyId, byte[] SealedKey, byte[] Recovery, byte[] File) Parse(byte[] stored)
    {
        var at = Magic.Length;
        if (stored.Length < at + KeyIdSize + SealedKeySize + 2) throw new CryptographicException("Tệp hợp đồng đã mã hóa bị hỏng.");
        var keyId = stored[at..(at + KeyIdSize)];
        at += KeyIdSize;
        var sealedKey = stored[at..(at + SealedKeySize)];
        at += SealedKeySize;
        var recoveryLength = BinaryPrimitives.ReadUInt16BigEndian(stored.AsSpan(at));
        at += 2;
        if (stored.Length < at + recoveryLength) throw new CryptographicException("Tệp hợp đồng đã mã hóa bị hỏng.");
        return (keyId, sealedKey, stored[at..(at + recoveryLength)], stored[(at + recoveryLength)..]);
    }

    /// <summary>The recovery public key when one is set; a server without one still encrypts, only without the recovery copy.</summary>
    private byte[]? RecoveryPublicKey()
    {
        if (_recovery is null) return null;
        try { return _recovery.PublicKey; }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or CryptographicException) { return null; }
    }

    private static byte[] KeyId(byte[] key) => SHA256.HashData(key)[..KeyIdSize];

    private static byte[] Seal(byte[] plain, byte[] key)
    {
        var result = new byte[NonceSize + TagSize + plain.Length];
        RandomNumberGenerator.Fill(result.AsSpan(0, NonceSize));
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(result.AsSpan(0, NonceSize), plain, result.AsSpan(NonceSize + TagSize), result.AsSpan(NonceSize, TagSize));
        return result;
    }

    private static byte[] Open(byte[] sealedBytes, byte[] key)
    {
        var plain = new byte[sealedBytes.Length - NonceSize - TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(sealedBytes.AsSpan(0, NonceSize), sealedBytes.AsSpan(NonceSize + TagSize), sealedBytes.AsSpan(NonceSize, TagSize), plain);
        return plain;
    }

    /// <summary>
    /// FileEncryption:ContractFileKey (base64), else the file named by ContractFileKeyPath. On a developer machine with
    /// neither, a key is made once in private-storage/dev-keys, like the development recovery key; never outside Development.
    /// </summary>
    private static byte[]? LoadKey(FileEncryptionOptions options, IHostEnvironment environment)
    {
        if (!string.IsNullOrWhiteSpace(options.ContractFileKey)) return Decode(options.ContractFileKey);
        var path = options.ContractFileKeyPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            if (!environment.IsDevelopment()) return null;
            path = Path.Combine("..", "private-storage", "dev-keys", "contract-file-key.txt");
        }

        path = Path.GetFullPath(path, environment.ContentRootPath);
        if (!File.Exists(path))
        {
            if (!environment.IsDevelopment()) return null;
            lock (DevKeyLock)
            {
                if (!File.Exists(path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, NewKey());
                }
            }
        }

        return Decode(File.ReadAllText(path));
    }

    private static readonly object DevKeyLock = new();

    private static byte[] Decode(string base64)
    {
        var key = Convert.FromBase64String(base64.Trim());
        return key.Length == FileKeySize ? key : throw new InvalidOperationException("FileEncryption:ContractFileKey phải là 32 byte (base64).");
    }
}
