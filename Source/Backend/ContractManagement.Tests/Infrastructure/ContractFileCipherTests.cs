using System.Security.Cryptography;
using ContractManagement.Infrastructure.Persistence.Master;
using ContractManagement.Infrastructure.Persistence.Master.Scans;
using Xunit;

namespace ContractManagement.Tests.Infrastructure;

public class ContractFileCipherTests
{
    private static readonly byte[] Pdf = "%PDF-1.7 Hợp đồng HD-20261124-000001, bên A ký, bên B ký"u8.ToArray();

    private sealed class Recovery(RSA rsa) : IRecoveryPublicKey
    {
        public byte[] PublicKey => rsa.ExportSubjectPublicKeyInfo();
        public string KeyId => "test";
        public RSA? CreatePrivateKey() => null;
    }

    private sealed class NoRecovery : IRecoveryPublicKey
    {
        public byte[] PublicKey => throw new InvalidOperationException("Máy chủ chưa cấu hình khóa dự phòng.");
        public string KeyId => "";
        public RSA? CreatePrivateKey() => null;
    }

    private static byte[] NewKey() => RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void StoredBytesAreNotThePdf_AndTheServerKeyOpensThem()
    {
        var cipher = new ContractFileCipher(NewKey());
        var stored = cipher.Protect(Pdf);

        Assert.True(ContractFileCipher.IsEncrypted(stored));
        Assert.Equal(-1, stored.AsSpan().IndexOf("PDF"u8));
        Assert.Equal(-1, stored.AsSpan().IndexOf("HD-20261124"u8));
        Assert.Equal(Pdf, cipher.Unprotect(stored));
    }

    [Fact]
    public void TheSameFileIsStoredDifferentlyEachTime()
    {
        var cipher = new ContractFileCipher(NewKey());
        Assert.NotEqual(cipher.Protect(Pdf), cipher.Protect(Pdf));
    }

    [Fact]
    public void AChangedByteIsRefused()
    {
        var cipher = new ContractFileCipher(NewKey());
        var stored = cipher.Protect(Pdf);
        stored[^1] ^= 0x01;
        Assert.ThrowsAny<CryptographicException>(() => cipher.Unprotect(stored));
    }

    [Fact]
    public void AnotherServerKeyCannotOpenTheFile()
    {
        var stored = new ContractFileCipher(NewKey()).Protect(Pdf);
        Assert.ThrowsAny<CryptographicException>(() => new ContractFileCipher(NewKey()).Unprotect(stored));
    }

    [Fact]
    public void APlainFileWrittenBeforeEncryptionIsReadAsItIs()
    {
        var cipher = new ContractFileCipher(NewKey());
        Assert.Same(Pdf, cipher.Unprotect(Pdf));
    }

    [Fact]
    public void TheRecoveryKeyOpensTheFileWhenTheServerKeyIsLost()
    {
        using var recovery = RSA.Create(3072);
        var stored = new ContractFileCipher(NewKey(), new Recovery(recovery)).Protect(Pdf);
        Assert.Equal(Pdf, ContractFileCipher.UnprotectWithRecovery(stored, recovery));

        using var other = RSA.Create(3072);
        Assert.ThrowsAny<CryptographicException>(() => ContractFileCipher.UnprotectWithRecovery(stored, other));
    }

    [Fact]
    public void WithoutARecoveryKeyTheFileIsStillEncrypted()
    {
        var cipher = new ContractFileCipher(NewKey(), new NoRecovery());
        var stored = cipher.Protect(Pdf);
        Assert.True(ContractFileCipher.IsEncrypted(stored));
        Assert.Equal(Pdf, cipher.Unprotect(stored));
        using var rsa = RSA.Create(2048);
        Assert.Throws<CryptographicException>(() => ContractFileCipher.UnprotectWithRecovery(stored, rsa));
    }

    [Fact]
    public async Task StorageWritesEncryptedAndReadsPlain_AndWithoutACipherPassesBytesThrough()
    {
        var folder = Path.Combine(Path.GetTempPath(), "cipher-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(folder, "a.pdf");
            var storage = new InternalTemplateStorage(folder, new ContractFileCipher(NewKey()));
            await storage.WriteAsync(path, Pdf, default);
            Assert.True(ContractFileCipher.IsEncrypted(await File.ReadAllBytesAsync(path)));
            Assert.Equal(Pdf, await storage.ReadAsync(path, default));

            var plain = new InternalTemplateStorage(folder);
            var other = Path.Combine(folder, "b.pdf");
            await plain.WriteAsync(other, Pdf, default);
            Assert.Equal(Pdf, await File.ReadAllBytesAsync(other));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void AKeyThatIsNot32BytesIsRefused() =>
        Assert.Throws<ArgumentException>(() => new ContractFileCipher(new byte[16]));
}
