using ContractManagement.Infrastructure.Persistence.Master;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ContractManagement.Tests.Infrastructure;
public class InternalWordDetailsTests
{
    private static byte[] Sample(string name = "Công ty thử", string total = "16.000.000") {
        using var stream = new MemoryStream();
        using(var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document)) {
            var main=doc.AddMainDocumentPart();
            main.Document = new Document(new Body(new[] {"HỢP ĐỒNG", "CUNG CẤP PHẦN MỀM", "Số: 123", "Bên A: " + name,
                "Địa chỉ", "Địa chỉ A", "Mã số thuế", "0101234567", "Bên B: Công ty cung cấp", "Địa chỉ: Địa chỉ B",
                "Điều 1: Phạm vi", "Bộ luật có hiệu lực ngày 01/01/2006", "Tổng giá trị hợp đồng: " + total + " VNĐ"}
                .Select(t=>new Paragraph(new Run(new Text(t))))));
        }
        return stream.ToArray();
    }
    [Fact]
    public void ReadsEditedDocumentPartiesAndExplicitTotalWithoutGuessingDates() {
        var result=InternalWordDetails.Extract(Sample(),"file");
        Assert.Equal("HỢP ĐỒNG CUNG CẤP PHẦN MỀM",result.Name);
        Assert.Equal("Công ty thử",result.Fields["CUSTOMER_COMPANY"]);
        Assert.Equal("Địa chỉ A",result.Fields["CUSTOMER_ADDRESS"]);
        Assert.Equal("Địa chỉ B",result.Fields["PROVIDER_ADDRESS"]);
        Assert.Equal(16000000m,result.TotalAmount);
        Assert.Null(result.EffectiveDate); Assert.Null(result.ExpireDate);
        var changed=InternalWordDetails.Extract(Sample("Khách đã sửa","25.000.000"),"file");
        Assert.Equal("Khách đã sửa",changed.Fields["CUSTOMER_COMPANY"]);
        Assert.Equal(25000000m,changed.TotalAmount);
    }
    [Fact]
    public void DoesNotInventAmountsOrInterpretPlaceholdersAsPartyValues() {
        var result=InternalWordDetails.Extract(Sample("{{CUSTOMER_COMPANY}}","chưa thỏa thuận"),"file");
        Assert.False(result.Fields.ContainsKey("CUSTOMER_COMPANY"));
        Assert.Null(result.TotalAmount); Assert.Empty(result.Items);
    }
}
