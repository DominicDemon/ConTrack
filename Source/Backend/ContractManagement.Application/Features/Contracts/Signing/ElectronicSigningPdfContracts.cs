namespace ContractManagement.Application.Features.Contracts.Signing;

public sealed record PdfSignaturePlacement(
    int PageNumber,
    decimal X,
    decimal Y,
    decimal Width,
    decimal Height,
    string? SignerFullName = null);

public interface IElectronicSigningPdfService
{
    byte[] StampSignature(
        byte[] sourcePdf,
        byte[] signaturePng,
        PdfSignaturePlacement placement);
}
