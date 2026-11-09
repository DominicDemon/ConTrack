using System.Security.Cryptography;

namespace ContractManagement.Application.Common.Security;

/// <summary>
/// The cryptography of stored scans (Database/Proposals/003_FileEncryption.sql):
/// a random 256-bit key per file encrypts the file with AES-256-GCM; that key is wrapped with RSA-OAEP-SHA256
/// for each employee who may open the file and for the company's recovery key. An employee's RSA private key
/// is kept encrypted with a key derived from their 12-character key (PBKDF2-SHA256), which is never stored.
/// </summary>
public static class FileCrypto
{
    public const int UserKeyLength = 12;
    public const int Pbkdf2Iterations = 600_000;
    private const int RsaKeyBits = 3072;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    /// <summary>Starts every encrypted file, so an old plain file is still told apart.</summary>
    private static readonly byte[] FileMagic = "CMENC1\0\0"u8.ToArray();

    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%&*?";

    /// <summary>
    /// 12 characters with at least one capital, one small letter, one digit and one symbol. Letters and digits
    /// that look alike (0/O, 1/l/I) are left out, as the key is read off a screen and typed by hand.
    /// </summary>
    public static string GenerateUserKey()
    {
        var all = Upper + Lower + Digits + Symbols;
        var chars = new char[UserKeyLength];
        chars[0] = Upper[RandomNumberGenerator.GetInt32(Upper.Length)];
        chars[1] = Lower[RandomNumberGenerator.GetInt32(Lower.Length)];
        chars[2] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        chars[3] = Symbols[RandomNumberGenerator.GetInt32(Symbols.Length)];
        for (var i = 4; i < UserKeyLength; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }

    public sealed record UserKeyPair(byte[] PublicKey, byte[] EncryptedPrivateKey, byte[] Salt, int Iterations);

    public static UserKeyPair CreateUserKeyPair(string userKey)
    {
        using var rsa = RSA.Create(RsaKeyBits);
        var salt = RandomNumberGenerator.GetBytes(32);
        var kek = DeriveKey(userKey, salt, Pbkdf2Iterations);
        return new UserKeyPair(rsa.ExportSubjectPublicKeyInfo(), Seal(rsa.ExportPkcs8PrivateKey(), kek), salt, Pbkdf2Iterations);
    }

    /// <summary>Null when the key is wrong.</summary>
    public static RSA? UnlockPrivateKey(byte[] encryptedPrivateKey, byte[] salt, int iterations, string userKey)
    {
        var kek = DeriveKey(userKey, salt, iterations);
        byte[] pkcs8;
        try
        {
            pkcs8 = Open(encryptedPrivateKey, kek);
        }
        catch (AuthenticationTagMismatchException)
        {
            return null;
        }

        var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(pkcs8, out _);
        CryptographicOperations.ZeroMemory(pkcs8);
        return rsa;
    }

    public static bool IsEncrypted(ReadOnlySpan<byte> stored) => stored.StartsWith(FileMagic);

    /// <summary>The bytes to store and the file key to wrap for each reader.</summary>
    public static (byte[] Stored, byte[] FileKey) EncryptFile(byte[] content)
    {
        var fileKey = RandomNumberGenerator.GetBytes(32);
        var sealedContent = Seal(content, fileKey);
        var stored = new byte[FileMagic.Length + sealedContent.Length];
        FileMagic.CopyTo(stored, 0);
        sealedContent.CopyTo(stored, FileMagic.Length);
        return (stored, fileKey);
    }

    /// <summary>Throws <see cref="AuthenticationTagMismatchException"/> if the bytes were changed.</summary>
    public static byte[] DecryptFile(byte[] stored, byte[] fileKey) => Open(stored[FileMagic.Length..], fileKey);

    public static byte[] Wrap(byte[] fileKey, byte[] publicKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(publicKey, out _);
        return rsa.Encrypt(fileKey, RSAEncryptionPadding.OaepSHA256);
    }

    public static byte[] Unwrap(byte[] wrapped, RSA privateKey) => privateKey.Decrypt(wrapped, RSAEncryptionPadding.OaepSHA256);

    /// <summary>Names a recovery public key without giving it away: the hex SHA-256 of its SubjectPublicKeyInfo.</summary>
    public static string KeyId(byte[] publicKey) => Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant();

    private static byte[] DeriveKey(string userKey, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(userKey, salt, iterations, HashAlgorithmName.SHA256, 32);

    // nonce | tag | ciphertext
    private static byte[] Seal(byte[] plain, byte[] key)
    {
        var result = new byte[NonceSize + TagSize + plain.Length];
        var nonce = result.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plain, result.AsSpan(NonceSize + TagSize), result.AsSpan(NonceSize, TagSize));
        return result;
    }

    private static byte[] Open(byte[] sealedBytes, byte[] key)
    {
        if (sealedBytes.Length < NonceSize + TagSize)
        {
            throw new AuthenticationTagMismatchException();
        }

        var plain = new byte[sealedBytes.Length - NonceSize - TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(
            sealedBytes.AsSpan(0, NonceSize),
            sealedBytes.AsSpan(NonceSize + TagSize),
            sealedBytes.AsSpan(NonceSize, TagSize),
            plain);
        return plain;
    }
}
