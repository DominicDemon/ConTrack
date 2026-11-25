using ContractManagement.Application.Features.Internal.Scans;

namespace ContractManagement.Tests.Application.Internal.Scans;

/// <summary>The vault search box, on the two documents it was found lacking with (24/11/2026).</summary>
public sealed class VaultSearchTests
{
    private static readonly VaultSearch.Document Warehouse = new("hop-dong-thue-kho", null, "Kho bai", "hop-dong-thue-kho.pdf",
        ["HDK-2026-0456", "0108877665", "0977665544", "CONG TY TNHH KHO VAN MIEN BAC", "lienhe@khovan.vn"]);

    private static readonly VaultSearch.Document Printer = new("Hợp đồng mua máy in", "đối tác Máy In Việt, giao hàng tháng 12", "Hợp đồng mua bán", "may-in.pdf",
        ["MB-2026-1120", "0312026777", "0905123456", "banhang@mayin.vn"]);

    [Theory]
    // Phone and tax code, with or without spaces, and a phone typed with the country code.
    [InlineData("0977", true, false)]
    [InlineData("977665", true, false)]
    [InlineData("0977 665 544", true, false)]
    [InlineData("+84977665544", true, false)]
    [InlineData("0312026777", false, true)]
    [InlineData("031 202 6777", false, true)]
    // Contract number: the dash and spaces do not matter, and part of it no longer catches other numbers.
    [InlineData("HDK-2026", true, false)]
    [InlineData("HDK 2026 0456", true, false)]
    [InlineData("hdk20260456", true, false)]
    [InlineData("2026", true, true)]
    // Names and titles without accents, case or word order.
    [InlineData("kho van", true, false)]
    [InlineData("Kho Vận Miền Bắc", true, false)]
    [InlineData("khovan", true, false)]
    [InlineData("máy in", false, true)]
    [InlineData("may in", false, true)]
    [InlineData("mayin", false, true)]
    [InlineData("hop dong may in", false, true)]
    [InlineData("doi tac may in", false, true)]
    [InlineData("thuê kho", true, false)]
    // The folder counts.
    [InlineData("Kho bai", true, false)]
    [InlineData("Kho bãi", true, false)]
    [InlineData("mua ban", false, true)]
    // Nothing typed: everything; nothing matching: nothing.
    [InlineData("", true, true)]
    [InlineData("   ", true, true)]
    [InlineData("khong co gi", false, false)]
    public void FindsTheRightDocuments(string query, bool warehouse, bool printer)
    {
        Assert.Equal(warehouse, VaultSearch.Matches(query, Warehouse));
        Assert.Equal(printer, VaultSearch.Matches(query, Printer));
    }
}
