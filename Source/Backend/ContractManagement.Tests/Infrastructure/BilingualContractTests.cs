using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using UglyToad.PdfPig;

namespace ContractManagement.Tests.Infrastructure;

/// <summary>A bilingual contract (English first, Vietnamese under it) as in the company's printed supply contracts.</summary>
public sealed class BilingualContractTests
{
    private static TemplateDefinition Template => new(1, "Mẫu", "v1", false,
        [new("CUSTOMER_COMPANY", "Tên khách hàng"), new("CUSTOMER_ADDRESS", "Địa chỉ khách hàng")],
        [new("SCOPE", "Phạm vi công việc", "")]);

    private static DraftContent Draft(string? mode) => new()
    {
        FormatVersion = 2, TemplateId = 1, CustomerId = 1, PartyType = "PERSON", Name = "Hợp đồng cung cấp và lắp đặt", LanguageMode = mode,
        NameEn = "Supply and installation contract", FieldsEn = new() { ["CUSTOMER_COMPANY"] = "Wanek Furniture Co., Ltd", ["UNKNOWN"] = "x" },
        Items = [new(null, "Phần mềm", 1, 100, 0, 0)], Milestones = [new("1", "Đợt 1", "SCOPE", 100)],
        Terms = [new("SCOPE", "Phạm vi công việc", "Bên B cung cấp phần mềm.", true, "Scope of the work", "Party B supplies the software.")]
    };

    [Fact]
    public void BilingualDraftNeedsEnglishTextAndVietnameseOnlyDraftDropsIt()
    {
        var bilingual = InternalDraftPolicy.Prepare(Draft(InternalDraftPolicy.Bilingual), Template);
        Assert.Empty(bilingual.MissingFields);
        Assert.Equal(["CUSTOMER_COMPANY"], bilingual.FieldsEn!.Keys);

        var missing = Draft(InternalDraftPolicy.Bilingual);
        missing.NameEn = " ";
        missing.Terms = [missing.Terms[0] with { ContentEn = "" }];
        InternalDraftPolicy.Prepare(missing, Template);
        Assert.Contains("Tên hợp đồng tiếng Anh", missing.MissingFields);
        Assert.Contains("Điều khoản tiếng Anh", missing.MissingFields);

        var vietnamese = InternalDraftPolicy.Prepare(Draft("VI"), Template);
        Assert.Null(vietnamese.LanguageMode);
        Assert.Null(vietnamese.NameEn);
        Assert.Null(vietnamese.FieldsEn);
        Assert.All(vietnamese.Terms, t => { Assert.Null(t.TitleEn); Assert.Null(t.ContentEn); });
        // Nothing English is stored for a Vietnamese contract, so its snapshot JSON is what it was before this feature.
        Assert.DoesNotContain("En\"", System.Text.Json.JsonSerializer.Serialize(vietnamese));

        Assert.Throws<ArgumentException>(() => InternalDraftPolicy.Prepare(Draft(InternalDraftPolicy.Bilingual), Template with { WordStorageKey = "a.docx" }));
        Assert.Throws<ArgumentException>(() => InternalDraftPolicy.Prepare(Draft("FR"), Template));
    }

    [Fact]
    public void SigningPdfPrintsEnglishThenVietnamese()
    {
        if (!OperatingSystem.IsWindows()) return;
        var content = InternalDraftPolicy.Prepare(Draft(InternalDraftPolicy.Bilingual), Template);
        var rendered = InternalSigningService.RenderContractPdf(
            new ContractDocument { ContractDocumentId = 6, ContractDocumentCode = "EHS-18-W3-06", ContractDocumentName = content.Name, CustomerId = 1, EmployeeId = 1 },
            new ContractVersion { ContractVersionVersionNo = 1, ContractVersionCurrencycode = "VND" },
            new Customer { CustomerDisplayName = "Hoa Nét", CustomerPersonalPhone = "0900000000" }, content);

        using var pdf = PdfDocument.Open(rendered.Content);
        var text = string.Join('\n', pdf.GetPages().Select(page => page.Text));
        foreach (var expected in new[] { "Supply and installation contract", "Hợp đồng cung cấp và lắp đặt", "PARTY A", "Wanek Furniture Co., Ltd",
                     "ARTICLE 1: Scope of the work", "ĐIỀU 1: Phạm vi công việc", "Party B supplies the software.", "Bên B cung cấp phần mềm." })
            Assert.Contains(expected, text);
        Assert.True(text.IndexOf("ARTICLE 1", StringComparison.Ordinal) < text.IndexOf("ĐIỀU 1", StringComparison.Ordinal));
    }
}
