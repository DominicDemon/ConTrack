using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Internal.Scans;
using Microsoft.Extensions.Options;

namespace ContractManagement.Tests.Application.Internal.Scans;

public sealed class ContractScanKeywordReaderTests
{
    private const string FirstPage =
        "HOP DONG CUNG CAP PHAN MEM\nSo: HD-20261026-000001\nMa so thue: 0312345678\nDien thoai: 0912 345 678\n";
    private const string SecondPage =
        "Ben B\nMa so thue: 0109876543\nDien thoai: 024 3999 8888\nSo: HD-20261026-000001\n";
    private const string Filler = "Dieu khoan chung ve quyen va nghia vu cua cac ben trong hop dong nay.\n";

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
    private static readonly byte[] Pdf = "%PDF-1.4 fake"u8.ToArray();

    private static string[] Values(ScanKeywordResult result, ScanKeywordType type) =>
        result.Keywords.Where(keyword => keyword.Type == type).Select(keyword => keyword.Value).ToArray();

    [Fact]
    public async Task Image_IsReadOnce_AndItsKeywordsAreOnPageOne()
    {
        var engine = new FakeEngine(FirstPage);

        var result = await Reader(engine, new FakeRasterizer(0)).ReadAsync(Jpeg);

        Assert.Equal((1, 1), (result.PagesRead, result.PageCount));
        Assert.Equal(["HD-20261026-000001"], Values(result, ScanKeywordType.ContractCode));
        Assert.Equal(["0312345678"], Values(result, ScanKeywordType.TaxCode));
        Assert.Equal(["0912345678"], Values(result, ScanKeywordType.PhoneNumber));
        Assert.All(result.Keywords, keyword => Assert.Equal(1, keyword.Page));
        Assert.Equal([(int?)null], engine.Dpis);
    }

    [Fact]
    public async Task Pdf_ReadsEveryPage_AndKeepsEachKeywordOnceWithItsFirstPage()
    {
        var engine = new FakeEngine(FirstPage, Filler, SecondPage);
        var rasterizer = new FakeRasterizer(3);

        var result = await Reader(engine, rasterizer).ReadAsync(Pdf);

        Assert.Equal((3, 3), (result.PagesRead, result.PageCount));
        Assert.Equal([0, 1, 2], rasterizer.Rendered);
        Assert.Equal([(int?)300, 300, 300], engine.Dpis);
        Assert.Equal(["HD-20261026-000001"], Values(result, ScanKeywordType.ContractCode));
        Assert.Equal(["0312345678", "0109876543"], Values(result, ScanKeywordType.TaxCode));
        Assert.Equal(["0912345678", "02439998888"], Values(result, ScanKeywordType.PhoneNumber));
        Assert.Equal(1, result.Keywords.Single(keyword => keyword.Value == "HD-20261026-000001").Page);
        Assert.Equal(3, result.Keywords.Single(keyword => keyword.Value == "0109876543").Page);
    }

    [Fact]
    public async Task Pdf_ReadsAtMostKeywordMaxPages()
    {
        var engine = new FakeEngine(Filler, Filler, SecondPage);
        var rasterizer = new FakeRasterizer(3);

        var result = await Reader(engine, rasterizer, keywordMaxPages: 2).ReadAsync(Pdf);

        Assert.Equal((2, 3), (result.PagesRead, result.PageCount));
        Assert.Equal([0, 1], rasterizer.Rendered);
        Assert.Empty(result.Keywords);
    }

    [Fact]
    public async Task ScanWithoutAnyKeyword_IsNotAnError()
    {
        var result = await Reader(new FakeEngine(Filler), new FakeRasterizer(0)).ReadAsync(Jpeg);

        Assert.Empty(result.Keywords);
        Assert.Equal(1, result.PagesRead);
    }

    [Fact]
    public async Task UnsupportedFile_IsRejectedAsAClientError_WithoutRunningOcr()
    {
        var engine = new FakeEngine();

        var error = await Assert.ThrowsAsync<ContractScanOcrException>(() =>
            Reader(engine, new FakeRasterizer(0)).ReadAsync("GIF89a..."u8.ToArray()));

        Assert.Equal(ContractScanOcrErrorCodes.UnsupportedFile, error.FailureCode);
        Assert.True(error.IsClientError);
        Assert.Empty(engine.Dpis);
    }

    [Fact]
    public async Task PdfWithoutPages_IsRejectedAsAClientError()
    {
        var error = await Assert.ThrowsAsync<ContractScanOcrException>(() =>
            Reader(new FakeEngine(), new FakeRasterizer(0)).ReadAsync(Pdf));

        Assert.Equal(ContractScanOcrErrorCodes.PdfUnreadable, error.FailureCode);
        Assert.True(error.IsClientError);
    }

    [Fact]
    public async Task EngineFailure_IsNotAClientError_SoTheJobIsRetried()
    {
        var engine = new FakeEngine
        {
            Failure = new ContractScanOcrException(
                ContractScanOcrErrorCodes.EngineUnavailable, "Tesseract is not installed.")
        };

        var error = await Assert.ThrowsAsync<ContractScanOcrException>(() =>
            Reader(engine, new FakeRasterizer(0)).ReadAsync(Jpeg));

        Assert.False(error.IsClientError);
    }

    private static ContractScanKeywordReader Reader(
        IScanOcrEngine engine, IScanPageRasterizer rasterizer, int keywordMaxPages = 30) =>
        new(engine, rasterizer, Options.Create(new ScanOcrOptions { KeywordMaxPages = keywordMaxPages }));

    /// <summary>Returns the given text per call, in order.</summary>
    private sealed class FakeEngine(params string[] pages) : IScanOcrEngine
    {
        public Exception? Failure { get; init; }

        public List<int?> Dpis { get; } = [];

        public Task<string> RecognizeAsync(byte[] image, int? dpi, CancellationToken cancellationToken = default)
        {
            Dpis.Add(dpi);
            return Failure is not null
                ? throw Failure
                : Task.FromResult(pages[Dpis.Count - 1]);
        }
    }

    private sealed class FakeRasterizer(int pageCount) : IScanPageRasterizer
    {
        public List<int> Rendered { get; } = [];

        public int GetPageCount(byte[] pdf) => pageCount;

        public ScanPageImage RenderPage(byte[] pdf, int pageIndex)
        {
            Rendered.Add(pageIndex);
            return new ScanPageImage([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], 300);
        }
    }
}
