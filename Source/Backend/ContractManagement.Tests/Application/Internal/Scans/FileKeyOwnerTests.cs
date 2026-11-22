using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Infrastructure.Persistence.Master.Scans;

namespace ContractManagement.Tests.Application.Internal.Scans;

public sealed class FileKeyOwnerTests
{
    private static readonly FileKeyPolicy.Owner Owner = new("hanhchinh2", "Lan.Nguyen@congty.vn", "0912 381 947");

    [Theory]
    [InlineData("Kh0a#Rieng-Tu")]
    [InlineData("Song!Da7Ngay")]
    [InlineData("~Trang9Vang~xX")]
    public void GoodKeys_AreAccepted(string key) => Assert.Null(FileKeyPolicy.Problem(key, Owner));

    [Theory]
    [InlineData("", "ít nhất 12")]
    [InlineData("Ab1#Ab1#Ab1", "ít nhất 12")]
    [InlineData("Khóa#Riêng9tu", "không dấu")]
    [InlineData("Khoa Rieng#9tu", "không dấu")]
    [InlineData("khoarieng#9tu", "chữ hoa")]
    [InlineData("KhoaRieng9tuX", "chữ hoa")]
    [InlineData("KhoaRieng#tuX", "chữ hoa")]
    [InlineData("Khoa#Rieng1234", "theo thứ tự")]
    [InlineData("Khoa#Rieng0000x", "theo thứ tự")]
    [InlineData("Khoa#Rieng-dcba9", "theo thứ tự")]
    [InlineData("Xy#9Password!z", "dễ đoán")]
    [InlineData("Xy#9ConTrack!z", "dễ đoán")]
    [InlineData("Xy#9HanhChinh2!", "tên đăng nhập")]
    [InlineData("Xy#9lan.nguyen!", "tên đăng nhập")]
    [InlineData("Xy#9z381947!Kq", "tên đăng nhập")]
    public void WeakKeys_AreRefusedWithTheReason(string key, string reason) =>
        Assert.Contains(reason, FileKeyPolicy.Problem(key, Owner));

    [Fact]
    public void LongKeys_AreCappedSoTheyStillFitTheHeader() =>
        Assert.Contains("tối đa", FileKeyPolicy.Problem("Kh0a#" + new string('x', 30) + new string('y', 30), Owner));

    [Fact]
    public void Reseal_KeepsThePublicKey_SoEveryGrantedFileStillOpens()
    {
        var pair = FileCrypto.CreateUserKeyPair("Kh0a#Rieng-Tu");
        var (stored, fileKey) = FileCrypto.EncryptFile([1, 2, 3]);
        var wrapped = FileCrypto.Wrap(fileKey, pair.PublicKey);

        using var oldPrivate = FileCrypto.UnlockPrivateKey(pair.EncryptedPrivateKey, pair.Salt, pair.Iterations, "Kh0a#Rieng-Tu")!;
        var resealed = FileCrypto.Reseal(oldPrivate, "Song!Da7Ngay");
        using var newPrivate = FileCrypto.UnlockPrivateKey(resealed.EncryptedPrivateKey, resealed.Salt, resealed.Iterations, "Song!Da7Ngay");

        Assert.Equal(pair.PublicKey, resealed.PublicKey);
        Assert.NotNull(newPrivate);
        Assert.Equal([1, 2, 3], FileCrypto.DecryptFile(stored, FileCrypto.Unwrap(wrapped, newPrivate)));
        Assert.Null(FileCrypto.UnlockPrivateKey(resealed.EncryptedPrivateKey, resealed.Salt, resealed.Iterations, "Kh0a#Rieng-Tu"));
    }

    [Fact]
    public void Code_IsUsedOnce()
    {
        var store = new FileKeyCodeStore(new FakeTimeProvider());
        var code = store.Issue(7);

        store.Verify(7, code);
        Assert.Equal(400, Assert.Throws<InternalAppException>(() => store.Verify(7, code)).Status);
    }

    [Fact]
    public void Code_EndsAfterFiveWrongTries_AndAfterFiveMinutes()
    {
        var clock = new FakeTimeProvider();
        var store = new FileKeyCodeStore(clock);
        var code = store.Issue(7);
        var wrong = code == "000000" ? "111111" : "000000";
        for (var i = 0; i < FileKeyCodeStore.MaxWrongTries; i++)
        {
            Assert.Throws<InternalAppException>(() => store.Verify(7, wrong));
        }

        Assert.Contains("hết hạn", Assert.Throws<InternalAppException>(() => store.Verify(7, code)).Message);

        clock.Advance(FileKeyCodeStore.ResendAfter);
        var fresh = store.Issue(7);
        clock.Advance(FileKeyCodeStore.Lifetime + TimeSpan.FromSeconds(1));
        Assert.Contains("hết hạn", Assert.Throws<InternalAppException>(() => store.Verify(7, fresh)).Message);
    }

    [Fact]
    public void Codes_WaitAMinuteBetweenSends_AndFiveAnHourAtMost()
    {
        var clock = new FakeTimeProvider();
        var store = new FileKeyCodeStore(clock);
        store.Issue(7);
        Assert.Equal(429, Assert.Throws<InternalAppException>(() => store.Issue(7)).Status);
        store.Issue(8);

        for (var i = 1; i < FileKeyCodeStore.MaxPerHour; i++)
        {
            clock.Advance(FileKeyCodeStore.ResendAfter);
            store.Issue(7);
        }

        clock.Advance(FileKeyCodeStore.ResendAfter);
        Assert.Contains("1 giờ", Assert.Throws<InternalAppException>(() => store.Issue(7)).Message);
        clock.Advance(TimeSpan.FromHours(1));
        store.Issue(7);
    }

    [Theory]
    [InlineData("lan.nguyen@congty.vn", "l*****@congty.vn")]
    [InlineData("ab@x.vn", "a*@x.vn")]
    [InlineData("a@x.vn", "*@x.vn")]
    [InlineData("not an email", null)]
    public void Email_IsMaskedWhereTheCodeGoes(string address, string? expected) =>
        Assert.Equal(expected, FileKeyOwnerService.Mask(address));

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 18, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
