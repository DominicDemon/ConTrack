using System.Buffers.Binary;
using ContractManagement.Application.Features.Contracts.Signing;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;

namespace ContractManagement.Infrastructure.Files;

public sealed class ElectronicSigningPdfService : IElectronicSigningPdfService
{
    private const int MaximumSignatureBytes = 1_000_000;
    private const int MaximumSignatureDimension = 2048;
    private const int MaximumSignaturePixels = 4_000_000;

    public byte[] StampSignature(
        byte[] sourcePdf,
        byte[] signaturePng,
        PdfSignaturePlacement placement)
    {
        ArgumentNullException.ThrowIfNull(sourcePdf);
        ArgumentNullException.ThrowIfNull(signaturePng);
        ArgumentNullException.ThrowIfNull(placement);
        if (sourcePdf.Length == 0 || !sourcePdf.AsSpan().StartsWith("%PDF-"u8))
        {
            throw new ArgumentException("The source document is not a PDF.", nameof(sourcePdf));
        }

        ValidatePng(signaturePng);
        if (placement.PageNumber <= 0
            || placement.X < 0
            || placement.Y < 0
            || placement.Width <= 0
            || placement.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(placement), "The signature rectangle is invalid.");
        }

        using var source = new MemoryStream(sourcePdf, writable: false);
        using var document = PdfReader.Open(source, PdfDocumentOpenMode.Modify);
        if (placement.PageNumber > document.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(placement), "The signature page does not exist.");
        }

        var page = document.Pages[placement.PageNumber - 1];
        var rectangle = new XRect(
            (double)placement.X,
            (double)placement.Y,
            (double)placement.Width,
            (double)placement.Height);
        if (rectangle.Right > page.Width.Point || rectangle.Bottom > page.Height.Point)
        {
            throw new ArgumentOutOfRangeException(nameof(placement), "The signature rectangle is outside the PDF page.");
        }

        using var signatureStream = new MemoryStream(signaturePng, writable: false);
        using var image = XImage.FromStream(signatureStream);
        var nameHeight = string.IsNullOrWhiteSpace(placement.SignerFullName) ? 0 : 14;
        var signatureHeight = Math.Max(1, rectangle.Height - nameHeight);
        var scale = Math.Min(rectangle.Width / image.PointWidth, signatureHeight / image.PointHeight);
        var width = image.PointWidth * scale;
        var height = image.PointHeight * scale;
        var x = rectangle.X + (rectangle.Width - width) / 2;
        var y = rectangle.Y + (signatureHeight - height) / 2;

        using var graphics = XGraphics.FromPdfPage(page);
        graphics.DrawRectangle(XBrushes.White, rectangle);
        graphics.DrawImage(image, x, y, width, height);
        if (nameHeight > 0)
        {
            var signerName = placement.SignerFullName!.Trim();
            var fontSize = 9d;
            var nameFont = new XFont("Arial", fontSize, XFontStyleEx.Regular);
            while (fontSize > 6 && graphics.MeasureString(signerName, nameFont).Width > rectangle.Width - 8)
            {
                fontSize -= 0.5;
                nameFont = new XFont("Arial", fontSize, XFontStyleEx.Regular);
            }
            var nameArea = new XRect(rectangle.X, rectangle.Y + signatureHeight, rectangle.Width, nameHeight);
            graphics.DrawString(signerName, nameFont, XBrushes.Black, nameArea, XStringFormats.TopCenter);
        }

        using var result = new MemoryStream();
        document.Save(result, closeStream: false);
        return result.ToArray();
    }

    private static void ValidatePng(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length is < 24 or > MaximumSignatureBytes
            || !bytes[..8].SequenceEqual(signature)
            || !bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new ArgumentException("A valid PNG signature image up to 1 MB is required.", nameof(bytes));
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(20, 4));
        if (width <= 0 || height <= 0
            || width > MaximumSignatureDimension
            || height > MaximumSignatureDimension
            || (long)width * height > MaximumSignaturePixels)
        {
            throw new ArgumentException("Signature image dimensions exceed the allowed limit.", nameof(bytes));
        }
    }
}
