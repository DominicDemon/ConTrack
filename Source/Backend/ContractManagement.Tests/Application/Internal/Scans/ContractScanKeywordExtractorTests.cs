using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Internal.Scans;
namespace ContractManagement.Tests.Application.Internal.Scans;

public sealed class ContractScanKeywordExtractorTests
{
    // Layout of the contract template: Vietnamese label, then the English label with the value.
    private const string CleanScan = """
        CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM
        HỢP ĐỒNG CUNG CẤP PHẦN MỀM
        Số: HD-20261026-000001
        BÊN A: CÔNG TY TNHH ABC
        Điện thoại: 0912 345 678
        Fax: 028 3822 1234
        Mã số thuế
        Tax number : 0312345678
        BÊN B: CÔNG TY CỔ PHẦN XYZ
        Điện thoại: 024 3999 8888
        Mã số thuế: 0109876543
        """;

    private static string[] Values(IEnumerable<ScanKeyword> keywords, ScanKeywordType type) =>
        keywords.Where(keyword => keyword.Type == type).Select(keyword => keyword.Value).ToArray();

    [Fact]
    public void CleanScan_YieldsCodeTaxCodesAndPhones()
    {
        var keywords = ContractScanKeywordExtractor.Extract(CleanScan);

        Assert.Equal(["HD-20261026-000001"], Values(keywords, ScanKeywordType.ContractCode));
        Assert.Equal(["0312345678", "0109876543"], Values(keywords, ScanKeywordType.TaxCode));
        Assert.Equal(["0912345678", "02439998888"], Values(keywords, ScanKeywordType.PhoneNumber));
        Assert.Equal(["CÔNG TY TNHH ABC", "CÔNG TY CỔ PHẦN XYZ"], Values(keywords, ScanKeywordType.CustomerName));
    }

    [Theory]
    [InlineData("Bên B (Bên bán): Công ty CP Giải pháp XYZ.", "Công ty CP Giải pháp XYZ")]
    [InlineData("BEN MUA :  Cong ty TNHH   Thuong mai ABC", "Cong ty TNHH Thuong mai ABC")]
    [InlineData("Khách hàng: Nguyễn Văn An", "Nguyễn Văn An")]
    [InlineData("Tên công ty: Công ty TNHH Sản xuất GHI", "Công ty TNHH Sản xuất GHI")]
    public void PartyName_IsTakenAfterItsLabel(string line, string expected)
    {
        Assert.Equal([expected], Values(ContractScanKeywordExtractor.Extract(line), ScanKeywordType.CustomerName));
    }

    [Theory]
    [InlineData("Đại diện: Ông Nguyễn Văn An")]
    [InlineData("Địa chỉ: 25 Lê Lợi, Quận 1")]
    [InlineData("Bên A:")]
    [InlineData("Bên A: 12")]
    [InlineData("Hai bên thống nhất: ký hợp đồng")]
    public void OtherLines_AreNotPartyNames(string line)
    {
        Assert.Empty(Values(ContractScanKeywordExtractor.Extract(line), ScanKeywordType.CustomerName));
    }

    [Fact]
    public void FaxNumber_IsNotAPhoneKeyword()
    {
        var keywords = ContractScanKeywordExtractor.Extract(CleanScan);

        Assert.DoesNotContain("02838221234", Values(keywords, ScanKeywordType.PhoneNumber));
    }

    [Theory]
    [InlineData("Số: HD - 2026 - OOO7", "HD-2026-0007")]
    [InlineData("S6: hd-2026-0007", "HD-2026-0007")]
    [InlineData("No. HD-2026-0007.", "HD-2026-0007")]
    public void ContractCode_ToleratesCommonOcrNoise(string line, string expected)
    {
        var keywords = ContractScanKeywordExtractor.Extract(line);

        Assert.Equal([expected], Values(keywords, ScanKeywordType.ContractCode));
    }

    [Theory]
    [InlineData("Tài khoản số : 9876543210")]
    [InlineData("Tài khoản số: 0123456789")]
    [InlineData("TÀI KHOẢN SỐ:\n1234567890123")]
    public void BankAccountNumber_IsNotAContractCode(string line)
    {
        var keywords = ContractScanKeywordExtractor.Extract(line);

        Assert.Empty(Values(keywords, ScanKeywordType.ContractCode));
    }

    [Fact]
    public void LongRunOfDigitsAfterTheCodeLabel_IsNotAContractCode_ButAShortNumberIs()
    {
        Assert.Empty(ContractScanKeywordExtractor.Extract("Số: 0312345678"));
        Assert.Equal(["123456"], Values(ContractScanKeywordExtractor.Extract("Số: 123456"), ScanKeywordType.ContractCode));
    }

    [Fact]
    public void ContractCode_StillFoundNextToABankAccountLine()
    {
        var keywords = ContractScanKeywordExtractor.Extract(
            "Số: HD-20261026-000001\nTài khoản số : 9876543210\nTại ngân hàng: ABC");

        Assert.Equal(["HD-20261026-000001"], Values(keywords, ScanKeywordType.ContractCode));
    }

    [Fact]
    public void SystemCode_IsFoundWithoutItsLabel()
    {
        var keywords = ContractScanKeywordExtractor.Extract("Căn cứ hợp đồng HD-20261O26-OOOOO1 đã ký");

        Assert.Equal(["HD-20261026-000001"], Values(keywords, ScanKeywordType.ContractCode));
    }

    [Fact]
    public void TaxCode_KeepsTheBranchSuffix()
    {
        var keywords = ContractScanKeywordExtractor.Extract("MST: 0312345678-001");

        Assert.Equal(["0312345678001"], Values(keywords, ScanKeywordType.TaxCode));
    }

    [Fact]
    public void BareTenDigitNumber_WithoutALabel_IsIgnored()
    {
        var keywords = ContractScanKeywordExtractor.Extract("Tài khoản số 0312345678 tại ngân hàng");

        Assert.Empty(keywords);
    }

    [Theory]
    [InlineData("Điện thoại: +84 912 345 678", "0912345678")]
    [InlineData("Tel: 84912345678", "0912345678")]
    [InlineData("SĐT: 0912.345.678", "0912345678")]
    [InlineData("Phone (+84) 24 3999 8888", "02439998888")]
    public void PhoneNumber_IsNormalisedToDigitsStartingWithZero(string line, string expected)
    {
        var keywords = ContractScanKeywordExtractor.Extract(line);

        Assert.Equal([expected], Values(keywords, ScanKeywordType.PhoneNumber));
    }

    [Fact]
    public void SeveralPhonesOnOneLine_AreSeparateKeywords()
    {
        var keywords = ContractScanKeywordExtractor.Extract("Điện thoại: 0912 345 678 / 0987 654 321");

        Assert.Equal(["0912345678", "0987654321"], Values(keywords, ScanKeywordType.PhoneNumber));
    }

    [Fact]
    public void RepeatedKeyword_IsKeptOnceWithItsFirstPage()
    {
        var first = ContractScanKeywordExtractor.Extract("Số: HD-2026-0007", page: 1);
        var again = ContractScanKeywordExtractor.Extract("Số: HD-2026-0007", page: 3);

        Assert.Equal(1, Assert.Single(first).Page);
        Assert.Equal(3, Assert.Single(again).Page);
        Assert.Single(ContractScanKeywordExtractor.Extract("Số: HD-2026-0007\nNo. HD-2026-0007"));
    }

    [Theory]
    [InlineData("HD-20261026-000001", new[] { "HD-20261026-000001", "20261026000001" })]
    [InlineData("hd - 2026 - 0007", new[] { "HD-2026-0007", "20260007" })]
    [InlineData("Công ty ABC", new[] { "CONGTYABC" })]
    [InlineData("0912 345 678", new[] { "0912345678" })]
    [InlineData("+84 912 345 678", new[] { "+84912345678", "84912345678", "0912345678" })]
    [InlineData("0312345678", new[] { "0312345678" })]
    public void SearchTerms_TurnWhatWasTypedIntoTheFormsStoredKeywordsUse(string typed, string[] expected)
    {
        Assert.Equal(expected, ContractScanKeywordExtractor.SearchTerms(typed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    [InlineData("123")]
    public void SearchTerms_TooShortToSearchScans_YieldNothing(string? typed)
    {
        Assert.Empty(ContractScanKeywordExtractor.SearchTerms(typed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void NoText_YieldsNoKeywords(string? text)
    {
        Assert.Empty(ContractScanKeywordExtractor.Extract(text));
    }

    [Fact]
    public void KeywordsPerType_AreCapped()
    {
        var text = string.Join('\n', Enumerable.Range(0, 40).Select(index => $"Số: HD-2026-{index:D4}"));

        var keywords = ContractScanKeywordExtractor.Extract(text);

        Assert.Equal(
            ContractScanKeywordExtractor.MaximumPerType,
            Values(keywords, ScanKeywordType.ContractCode).Length);
    }

    [Theory]
    [InlineData(ScanKeywordType.ContractCode, " hd - 20261026 - 000001 ", "HD-20261026-000001")]
    [InlineData(ScanKeywordType.ContractCode, "hđ-2026-OOO7", "HD-2026-0007")]
    [InlineData(ScanKeywordType.TaxCode, "0312345678", "0312345678")]
    [InlineData(ScanKeywordType.TaxCode, "0312345678-001", "0312345678001")]
    [InlineData(ScanKeywordType.PhoneNumber, "+84 912 345 678", "0912345678")]
    [InlineData(ScanKeywordType.PhoneNumber, "028.3822.1234", "02838221234")]
    [InlineData(ScanKeywordType.CustomerName, "  Công ty   TNHH ABC ", "Công ty TNHH ABC")]
    public void NormaliseTyped_StoresWhatStaffTypeLikeOcrKeywords(ScanKeywordType type, string typed, string expected)
    {
        Assert.Equal(expected, ContractScanKeywordExtractor.NormaliseTyped(type, typed));
    }

    [Theory]
    [InlineData(ScanKeywordType.ContractCode, "HD")]
    [InlineData(ScanKeywordType.ContractCode, "0312345678")]
    [InlineData(ScanKeywordType.TaxCode, "123")]
    [InlineData(ScanKeywordType.TaxCode, "03123456789")]
    [InlineData(ScanKeywordType.PhoneNumber, "0912 345 678 9")]
    [InlineData(ScanKeywordType.PhoneNumber, "1912345678")]
    [InlineData(ScanKeywordType.PhoneNumber, "   ")]
    [InlineData(ScanKeywordType.CustomerName, "AB")]
    public void NormaliseTyped_RejectsWhatIsNotAKeyword(ScanKeywordType type, string typed)
    {
        Assert.Null(ContractScanKeywordExtractor.NormaliseTyped(type, typed));
    }
}
