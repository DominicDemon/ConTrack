namespace ContractManagement.Application.Features.Contracts.Signing;

public sealed record PdfSignaturePlacement(
    int PageNumber,
    decimal X,
    decimal Y,
    decimal Width,
    decimal Height);

public interface IElectronicSigningPdfService
{
    byte[] StampSignature(
        byte[] sourcePdf,
        byte[] signaturePng,
        PdfSignaturePlacement placement);
}
