using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using PdfSharp.Drawing;
using UglyToad.PdfPig;

namespace ContractManagement.Tests.Infrastructure;

public sealed class InternalContractPdfLayoutTests
{
    [Fact]
    public void SigningPdfUsesReadableSectionsAndKeepsSignatureBoxesOnTheFinalPage()
    {
        if (!OperatingSystem.IsWindows()) return;
        var rendered = InternalSigningService.RenderContractPdf(
            new ContractDocument
            {
                ContractDocumentId = 42,
                ContractDocumentCode = "HD-2026-0042",
                ContractDocumentName = "Hợp đồng cung cấp phần mềm",
                CustomerId = 8,
                EmployeeId = 3
            },
            new ContractVersion
            {
                ContractVersionVersionNo = 2,
                ContractVersionCurrencycode = "VND"
            },
            new Customer
            {
                CustomerDisplayName = "Công ty khách hàng",
                CustomerPersonalPhone = "0901234567",
                CustomerEmail = "buyer@example.com"
            },
            new DraftContent
            {
                Name = "Hợp đồng cung cấp phần mềm",
                Fields = new Dictionary<string, string>
                {
                    ["CONTRACT_LEGAL_BASES"] = "Các bên tự nguyện giao kết hợp đồng.",
                    ["CUSTOMER_COMPANY"] = "Công ty khách hàng",
                    ["CUSTOMER_CONTACT_NAME"] = "Nguyễn Văn A",
                    ["CUSTOMER_ADDRESS"] = "Quận 1, Thành phố Hồ Chí Minh",
                    ["CUSTOMER_CONTACT_PHONE"] = "0901234567",
                    ["CUSTOMER_EMAIL"] = "buyer@example.com",
                    ["PROVIDER_LEGAL_NAME"] = "Công ty ConTrack",
                    ["PROVIDER_REPRESENTATIVE_NAME"] = "Trần Thị B",
                    ["PROVIDER_REPRESENTATIVE_TITLE"] = "Giám đốc",
                    ["PROVIDER_ADDRESS"] = "Quận 3, Thành phố Hồ Chí Minh",
                    ["PROVIDER_TAX_CODE"] = "0312345678"
                },
                Items = [new DraftItem(null, "Gói phần mềm quản lý hợp đồng", 2, 1_250_000, 0, 0)],
                Terms = [new DraftTerm("PAYMENT", "Thanh toán", string.Join(' ', Enumerable.Repeat("Khách hàng thanh toán theo tiến độ đã thống nhất trong hợp đồng.", 150)))],
                VatPercent = 10,
                EffectiveDate = new DateOnly(2026, 11, 11),
                ExpireDate = new DateOnly(2027, 11, 11),
                Totals = new DraftTotals(2_500_000, 0, 250_000, 2_750_000)
            });

        using var pdf = PdfDocument.Open(rendered.Content);
        var text = string.Join('\n', pdf.GetPages().Select(page => page.Text));

        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(rendered.Content[..5]));
        Assert.True(pdf.NumberOfPages > 1);
        Assert.Equal(pdf.NumberOfPages, rendered.SignaturePage);
        Assert.Contains("HD-2026-0042", text);
        Assert.Contains("Công ty khách hàng", text);
        Assert.Contains("Gói phần mềm quản lý hợp đồng", text);
        Assert.InRange(rendered.CustomerSignatureX + rendered.SignatureWidth, 0, 595 - 44);
        Assert.InRange(rendered.CompanySignatureX + rendered.SignatureWidth, 0, 595 - 44);
    }

    [Fact]
    public void CompanySignatureImageCanBeRecoveredFromTheSignedPdfForTheCustomerLayout()
    {
        if (!OperatingSystem.IsWindows()) return;
        // The API's assets are copied next to the test assembly.
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Email", "logo_light.png");
        var signaturePng = File.ReadAllBytes(logoPath);
        using var source = new PdfSharp.Pdf.PdfDocument();
        var page = source.AddPage();
        using (var graphics = XGraphics.FromPdfPage(page))
        using (var imageStream = new MemoryStream(signaturePng))
        using (var image = XImage.FromStream(imageStream))
            graphics.DrawImage(image, 50, 680, 170, 55);
        using var stream = new MemoryStream();
        source.Save(stream);

        var recovered = CustomerSigningService.ExtractCompanySignature(stream.ToArray(), 1);

        Assert.Equal(new byte[] { 137, 80, 78, 71 }, recovered[..4]);
    }
}
