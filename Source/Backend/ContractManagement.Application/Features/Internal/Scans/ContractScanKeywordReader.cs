using ContractManagement.Domain.Internal.Scans;
using Microsoft.Extensions.Options;

namespace ContractManagement.Application.Features.Internal.Scans;

public sealed class ContractScanKeywordReader : IScanKeywordReader
{
    // Over the whole document; one page alone is capped lower by the extractor.
    private const int MaximumPerType = 50;

    private readonly IScanOcrEngine _engine;
    private readonly IScanPageRasterizer _rasterizer;
    private readonly ScanOcrOptions _options;

    public ContractScanKeywordReader(
        IScanOcrEngine engine,
        IScanPageRasterizer rasterizer,
        IOptions<ScanOcrOptions> options)
    {
        _engine = engine;
        _rasterizer = rasterizer;
        _options = options.Value;
    }

    public async Task<ScanKeywordResult> ReadAsync(
        byte[] file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.Length == 0 || file.Length > _options.MaxFileBytes)
        {
            throw new ContractScanOcrException(
                ContractScanOcrErrorCodes.UnsupportedFile,
                $"Tệp phải có nội dung và không vượt quá {_options.MaxFileBytes / (1024 * 1024)} MB.",
                isClientError: true);
        }

        var kind = ScanFileKinds.Detect(file)
            ?? throw new ContractScanOcrException(
                ContractScanOcrErrorCodes.UnsupportedFile,
                "Chỉ đọc được bản scan PDF, JPG, JPEG hoặc PNG.",
                isClientError: true);

        var pageCount = kind == ScanFileKind.Pdf ? _rasterizer.GetPageCount(file) : 1;
        if (pageCount <= 0)
        {
            throw new ContractScanOcrException(
                ContractScanOcrErrorCodes.PdfUnreadable,
                "Không đọc được trang nào của bản scan.",
                isClientError: true);
        }

        var pagesToRead = Math.Min(pageCount, _options.KeywordMaxPages);
        // Pages are rendered one by one (PDFium is not thread-safe), then read by several OCR
        // processes at once: one Tesseract run uses about one core.
        var pages = new List<ScanPageImage>(pagesToRead);
        for (var index = 0; index < pagesToRead; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pages.Add(kind == ScanFileKind.Pdf ? _rasterizer.RenderPage(file, index) : new ScanPageImage(file, null));
        }

        var texts = new string?[pagesToRead];
        using var slots = new SemaphoreSlim(_options.MaxParallelPages);
        await Task.WhenAll(pages.Select(async (page, index) =>
        {
            await slots.WaitAsync(cancellationToken);
            try
            {
                texts[index] = await _engine.RecognizeAsync(page.Content, page.Dpi, cancellationToken);
            }
            finally
            {
                slots.Release();
            }
        }));

        // In page order, so a keyword keeps the first page it is on.
        var keywords = texts.SelectMany((text, index) => ContractScanKeywordExtractor.Extract(text, index + 1));
        var merged = keywords
            .GroupBy(keyword => (keyword.Type, keyword.Value))
            .Select(group => group.First())
            .GroupBy(keyword => keyword.Type)
            .SelectMany(group => group.Take(MaximumPerType))
            .ToList();
        var text = string.Join("\n\n", texts.Select((page, index) => $"--- Trang {index + 1} ---\n{page?.Trim()}"));
        return new ScanKeywordResult(merged, pagesToRead, pageCount, text);
    }
}
