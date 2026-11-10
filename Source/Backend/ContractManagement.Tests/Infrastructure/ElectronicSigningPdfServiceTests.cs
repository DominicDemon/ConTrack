using ContractManagement.Application.Features.Contracts.Signing;
using ContractManagement.Infrastructure.Files;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;

namespace ContractManagement.Tests.Infrastructure;

public sealed class ElectronicSigningPdfServiceTests
{
    private static readonly byte[] TransparentPng = Convert.FromBase64String(
        "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");

    [Fact]
    public void StampSignature_CreatesANewPdfWithTheSamePageCount()
    {
        var original = CreatePdf();
        var signed = new ElectronicSigningPdfService().StampSignature(
            original,
            TransparentPng,
            new PdfSignaturePlacement(1, 40, 700, 180, 60));

        Assert.NotEqual(original, signed);
        using var result = new MemoryStream(signed);
        using var document = PdfReader.Open(result, PdfDocumentOpenMode.Import);
        Assert.Single(document.Pages);
    }

    [Fact]
    public void StampSignature_RejectsAPlacementOutsideThePdfPage()
    {
        var original = CreatePdf();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ElectronicSigningPdfService().StampSignature(
                original,
                TransparentPng,
                new PdfSignaturePlacement(1, 580, 800, 40, 60)));
    }

    private static byte[] CreatePdf()
    {
        using var document = new PdfDocument();
        document.AddPage();
        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }
}
