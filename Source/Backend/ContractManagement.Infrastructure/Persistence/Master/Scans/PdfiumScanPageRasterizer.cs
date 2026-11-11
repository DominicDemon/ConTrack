using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal.Scans;
using System.Runtime.Versioning;
using Microsoft.Extensions.Options;
using PDFtoImage;
using PDFtoImage.Exceptions;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

/// <summary>
/// Renders scanned PDF pages with PDFium, which the PDFtoImage package bundles for Windows,
/// Linux and macOS, so nothing has to be installed on the server.
/// </summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class PdfiumScanPageRasterizer : IScanPageRasterizer
{
    // PDFium is not thread safe.
    private static readonly object RenderLock = new();

    private readonly ScanOcrOptions _options;

    public PdfiumScanPageRasterizer(IOptions<ScanOcrOptions> options)
    {
        _options = options.Value;
    }

    public int GetPageCount(byte[] pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        return Guard(() => Conversion.GetPageCount(pdf, null));
    }

    public ScanPageImage RenderPage(byte[] pdf, int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        return Guard(() =>
        {
            var size = Conversion.GetPageSize(pdf, pageIndex, null);
            var dpi = PageDpi(size.Width, size.Height);
            using var png = new MemoryStream();
            Conversion.SavePng(png, pdf, pageIndex, null, new RenderOptions(Dpi: dpi, Grayscale: true));
            return new ScanPageImage(png.ToArray(), dpi);
        });
    }

    // Page sizes are in points (1/72 inch).
    private int PageDpi(float widthPoints, float heightPoints)
    {
        var longest = Math.Max(widthPoints, heightPoints);
        if (longest <= 0)
        {
            return _options.RasterDpi;
        }

        var capped = (int)Math.Floor(_options.MaxPixelsPerSide * 72d / longest);
        return Math.Clamp(capped, 1, _options.RasterDpi);
    }

    private static T Guard<T>(Func<T> render)
    {
        try
        {
            lock (RenderLock)
            {
                return render();
            }
        }
        catch (PdfPasswordProtectedException)
        {
            throw new ContractScanOcrException(
                ContractScanOcrErrorCodes.PdfUnreadable,
                "PDF đang đặt mật khẩu. Hãy tải lên bản scan không đặt mật khẩu.",
                isClientError: true);
        }
        catch (PdfException)
        {
            throw new ContractScanOcrException(
                ContractScanOcrErrorCodes.PdfUnreadable,
                "Không mở được tệp PDF; tệp có thể bị hỏng.",
                isClientError: true);
        }
    }
}
