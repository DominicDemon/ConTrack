using System.Security.Cryptography;
using ContractManagement.Application.Common.Security;
using Xunit;

namespace ContractManagement.Tests;

public class FileCryptoTests
{
    private static readonly byte[] Scan = "%PDF-1.4 hợp đồng đã ký"u8.ToArray();

    [Fact]
    public void AGeneratedKeyHas12CharactersOfEveryKind()
    {
        for (var i = 0; i < 200; i++)
        {
            var key = FileCrypto.GenerateUserKey();
            Assert.Equal(12, key.Length);
            Assert.Contains(key, char.IsUpper);
            Assert.Contains(key, char.IsLower);
            Assert.Contains(key, char.IsDigit);
            Assert.Contains(key, c => !char.IsLetterOrDigit(c));
            Assert.DoesNotContain(key, c => "0O1lI".Contains(c));
        }
    }

    [Fact]
    public void TheRightKeyOpensTheFile_AndTheStoredBytesAreNotTheScan()
    {
        var userKey = FileCrypto.GenerateUserKey();
        var pair = FileCrypto.CreateUserKeyPair(userKey);
        var (stored, fileKey) = FileCrypto.EncryptFile(Scan);
        var wrapped = FileCrypto.Wrap(fileKey, pair.PublicKey);

        Assert.True(FileCrypto.IsEncrypted(stored));
        Assert.False(FileCrypto.IsEncrypted(Scan));
        Assert.Equal(-1, stored.AsSpan().IndexOf("PDF"u8));

        using var privateKey = FileCrypto.UnlockPrivateKey(pair.EncryptedPrivateKey, pair.Salt, pair.Iterations, userKey);
        Assert.NotNull(privateKey);
        Assert.Equal(Scan, FileCrypto.DecryptFile(stored, FileCrypto.Unwrap(wrapped, privateKey!)));
    }

    [Fact]
    public void AWrongKeyDoesNotUnlock()
    {
        var pair = FileCrypto.CreateUserKeyPair("Abcd2345!xyz");

        Assert.Null(FileCrypto.UnlockPrivateKey(pair.EncryptedPrivateKey, pair.Salt, pair.Iterations, "Abcd2345!xyZ"));
    }

    [Fact]
    public void AChangedFileIsRefused()
    {
        var (stored, fileKey) = FileCrypto.EncryptFile(Scan);
        stored[^1] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() => FileCrypto.DecryptFile(stored, fileKey));
    }

    [Fact]
    public void TheRecoveryKeyOpensAFileWithoutAnyEmployeeKey()
    {
        using var recovery = RSA.Create(3072);
        var (stored, fileKey) = FileCrypto.EncryptFile(Scan);
        var wrapped = FileCrypto.Wrap(fileKey, recovery.ExportSubjectPublicKeyInfo());

        Assert.Equal(Scan, FileCrypto.DecryptFile(stored, FileCrypto.Unwrap(wrapped, recovery)));
    }
}
