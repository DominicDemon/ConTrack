using ContractManagement.Application.Common.Files;

namespace ContractManagement.Tests.Application.Support;

public sealed class UploadedImagesTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13];
    private static string Url(string type, byte[] bytes) => $"data:{type};base64,{Convert.ToBase64String(bytes)}";

    [Fact]
    public void RealImagesPassAndBlankEntriesAreDropped()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2 };
        var webp = "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();
        var list = UploadedImages.SupportImages([Url("image/png", Png), Url("image/jpg", jpeg), Url("image/webp", webp), " ", null]);
        Assert.Equal(3, list.Count);
    }

    [Theory]
    [InlineData("https://tracker.example/pixel.png")]
    [InlineData("data:image/png,not-base64")]
    [InlineData("data:image/png;base64,PHNjcmlwdD4=")] // "<script>" pretending to be a PNG
    [InlineData("data:text/html;base64,PGgxPg==")]
    public void LinksAndFakeImagesAreRefused(string url) =>
        Assert.Throws<ArgumentException>(() => UploadedImages.SupportImages([url]));

    [Fact]
    public void DeclaredTypeMustMatchTheBytesAndSizeAndCountAreLimited()
    {
        Assert.Throws<ArgumentException>(() => UploadedImages.SupportImages([Url("image/jpeg", Png)]));
        var big = new byte[UploadedImages.MaxSupportImageBytes + 1]; Png.CopyTo(big, 0);
        Assert.Throws<ArgumentException>(() => UploadedImages.SupportImages([Url("image/png", big)]));
        var four = Enumerable.Range(0, 4).Select(i => Url("image/png", [.. Png, (byte)i])).ToList();
        Assert.Throws<ArgumentException>(() => UploadedImages.SupportImages(four));
    }

    [Fact]
    public void SignatureMustBeAPng()
    {
        Assert.Equal(Png, UploadedImages.SignaturePng(Url("image/png", Png), "bad"));
        Assert.Throws<ArgumentException>(() => UploadedImages.SignaturePng(Convert.ToBase64String([0xFF, 0xD8, 0xFF]), "bad"));
    }
}
