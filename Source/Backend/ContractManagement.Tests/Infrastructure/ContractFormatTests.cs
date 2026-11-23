using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using UglyToad.PdfPig;

namespace ContractManagement.Tests.Infrastructure;

public sealed class ContractFormatTests
{
    [Fact]
    public void FormatOutsideTheAllowedRangesIsRefused()
    {
        Assert.Throws<ArgumentException>(() => (ContractFormat.Default with { FontFamily = "Comic Sans MS" }).Validated());
        Assert.Throws<ArgumentException>(() => (ContractFormat.Default with { BodyFontSize = 30 }).Validated());
        Assert.Throws<ArgumentException>(() => (ContractFormat.Default with { MarginLeftMm = 5 }).Validated());
        Assert.Throws<ArgumentException>(() => (ContractFormat.Default with { LineSpacing = 3 }).Validated());
        Assert.Equal(14.3, (ContractFormat.Default with { BodyFontSize = 14.26 }).Validated().BodyFontSize);
    }

    [Fact]
    public void SavedFormatChangesTheSystemContractPdf()
    {
        if (!OperatingSystem.IsWindows()) return;
        var content = new DraftContent
        {
            Name = "Hợp đồng dịch vụ", Fields = new() { ["CUSTOMER_COMPANY"] = "Công ty A", ["PROVIDER_LEGAL_NAME"] = "Công ty B" },
            Items = [new DraftItem(null, "Dịch vụ", 1, 1000, 0, 0)], Terms = [new DraftTerm("T1", "Phạm vi", "Nội dung")],
            EffectiveDate = new DateOnly(2026, 1, 1), ExpireDate = new DateOnly(2026, 12, 31)
        };
        var document = new ContractDocument { ContractDocumentId = 1, ContractDocumentCode = "HD-1", ContractDocumentName = "Hợp đồng dịch vụ" };
        var version = new ContractVersion { ContractVersionVersionNo = 1, ContractVersionCurrencycode = "VND" };
        var customer = new Customer { CustomerDisplayName = "Công ty A" };
        var format = ContractFormat.Default with { FontFamily = "Arial", ShowNationalMotto = false, TitleUppercase = true };

        using var pdf = PdfDocument.Open(InternalSigningService.RenderContractPdf(document, version, customer, content, format).Content);
        var page = pdf.GetPage(1);
        Assert.Contains("HỢP ĐỒNG DỊCH VỤ", page.Text);
        Assert.DoesNotContain("CỘNG HÒA XÃ HỘI", page.Text);
        Assert.All(page.Letters.Where(l => !char.IsWhiteSpace(l.Value[0])).Take(20), l => Assert.Contains("Arial", l.FontName));
    }
}
