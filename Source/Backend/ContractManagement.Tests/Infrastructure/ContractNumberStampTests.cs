using ContractManagement.Infrastructure.Persistence.Master;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ContractManagement.Tests.Infrastructure;

/// <summary>The issued contract number replaces the number a Word template copied from its source document.</summary>
public class ContractNumberStampTests
{
    private static byte[] Docx(params string[][] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            doc.AddMainDocumentPart().Document = new Document(new Body(paragraphs.Select(runs =>
                new Paragraph(runs.Select(text => new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve })))).ToArray<OpenXmlElement>()));
        return stream.ToArray();
    }
    private static List<string> Texts(byte[] docx) => InternalWordDocument.Blocks(docx).Select(b => b.Text).ToList();
    private static readonly Dictionary<string, string> Number = new() { ["CONTRACT_NUMBER"] = "HD-20261118-000001" };

    [Fact]
    public void ReplacesTheHeadingNumberLineOnly()
    {
        var source = Docx(["HỢP ĐỒNG"], ["Số điện thoại: 0909"], ["Số", ": DTC-PCM2014", "0407/HDKT"], ["Căn cứ vào:"], ["Số: giữ nguyên"]);
        var texts = Texts(InternalWordDocument.Fill(source, Number, preserveUnknown: true));
        Assert.Contains("Số: HD-20261118-000001", texts);
        Assert.Contains("Số điện thoại: 0909", texts);   // a phone number line is not a contract number
        Assert.Contains("Số: giữ nguyên", texts);         // after "Căn cứ" it is contract text, not the heading
    }

    [Fact]
    public void LeavesATemplateWithoutANumberLineAndAnEmptyNumberUntouched()
    {
        var plain = Docx(["HỢP ĐỒNG"], ["Điều 1: Số: 5 bộ"]);
        Assert.Equal(Texts(plain), Texts(InternalWordDocument.Fill(plain, Number, preserveUnknown: true)));
        var numbered = Docx(["Số hợp đồng: 01/2014"]);
        Assert.Equal(["Số hợp đồng: 01/2014"], Texts(InternalWordDocument.Fill(numbered, new Dictionary<string, string> { ["CONTRACT_NUMBER"] = "" })));
        Assert.Equal(["Số hợp đồng: HD-20261118-000001"], Texts(InternalWordDocument.Fill(numbered, Number)));
    }
}
