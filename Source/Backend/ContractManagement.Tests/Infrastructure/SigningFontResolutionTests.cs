using ContractManagement.Infrastructure.Persistence.Master;
using PdfSharp.Drawing;
using System.Runtime.CompilerServices;

namespace ContractManagement.Tests.Infrastructure;

public sealed class SigningFontResolutionTests
{
    [Fact]
    public void SigningServiceCanRenderArialInsidePdfLayoutRectangles()
    {
        if (!OperatingSystem.IsWindows()) return;

        RuntimeHelpers.RunClassConstructor(typeof(InternalSigningService).TypeHandle);

        using var document = new PdfSharp.Pdf.PdfDocument();
        var page = document.AddPage();
        using var graphics = XGraphics.FromPdfPage(page);
        var font = new XFont("Arial", 10, XFontStyleEx.Regular);
        graphics.DrawString("Contract title", font, XBrushes.Black,
            new XRect(48, 48, page.Width.Point - 96, 28), XStringFormats.TopLeft);

        using var stream = new MemoryStream();
        document.Save(stream);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(stream.ToArray()[..5]));
    }
}
