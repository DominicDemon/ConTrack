using ContractManagement.Domain.Internal.Scans;
namespace ContractManagement.Application.Features.Internal.Scans;

/// <summary>Reads the text of one scanned page image (PNG or JPEG).</summary>
public interface IScanOcrEngine
{
    Task<string> RecognizeAsync(
        byte[] image,
        int? dpi,
        CancellationToken cancellationToken = default);
}

/// <summary>Turns the pages of a scanned PDF into images the OCR engine can read.</summary>
public interface IScanPageRasterizer
{
    int GetPageCount(byte[] pdf);

    ScanPageImage RenderPage(byte[] pdf, int pageIndex);
}

/// <summary>
/// Reads the search keywords (contract code, tax codes, phone numbers) from the pages of a signed
/// paper scan, up to <c>ScanOcr:KeywordMaxPages</c>.
/// </summary>
public interface IScanKeywordReader
{
    Task<ScanKeywordResult> ReadAsync(
        byte[] file,
        CancellationToken cancellationToken = default);
}
