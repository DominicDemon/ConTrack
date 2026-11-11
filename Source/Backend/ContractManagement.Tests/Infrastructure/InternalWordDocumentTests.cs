using ContractManagement.Infrastructure.Persistence.Master;
using ContractManagement.Application.Features.Internal;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ContractManagement.Tests.Infrastructure;

public class InternalWordDocumentTests
{
    internal const string TinyPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=";
    private static byte[] Document()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(
                new Paragraph(new Run(new RunProperties(new Bold()), new Text("Hello ")),
                    new Run(new Text("{{CUS")), new Run(new Text("TOMER_COMPANY}}")), new Run(new Text(" end"))),
                new Table(new TableRow(new TableCell(new Paragraph(new Run(new Text("Old amount"))))))));
            var header = main.AddNewPart<HeaderPart>();
            header.Header = new Header(new Paragraph(new Run(new Text("{{CUSTOMER_COMPANY}}"))));
        }
        return stream.ToArray();
    }

    [Fact]
    public void ReplacesSplitPlaceholderInBodyAndHeaderAndKeepsTable()
    {
        var bytes = Document();
        InternalWordDocument.Validate(bytes);
        var result = InternalWordDocument.Fill(bytes, new Dictionary<string, string> { ["CUSTOMER_COMPANY"] = "Công ty thử" });
        var blocks = InternalWordDocument.Blocks(result);
        Assert.Contains(blocks, b => b.Text == "Hello Công ty thử end");
        Assert.Contains(blocks, b => b.Location == "Đầu trang" && b.Text == "Công ty thử");
        Assert.Contains(blocks, b => b.Location.Contains("Ô bảng") && b.Text == "Old amount");
        using var stream = new MemoryStream(result);
        using var doc = WordprocessingDocument.Open(stream, false);
        Assert.Single(doc.MainDocumentPart!.Document!.Descendants<Bold>());
    }

    [Fact]
    public void EditsAndDeletesOnlySelectedParagraphAndRejectsChangedIds()
    {
        var bytes = Document();
        var blocks = InternalWordDocument.Blocks(bytes);
        var changed = blocks.Select(b => b.Text == "Old amount" ? b with { Text = "" } : b).ToList();
        var edited = InternalWordDocument.Blocks(InternalWordDocument.Edit(bytes, changed));
        Assert.Equal(blocks[0].Text, edited[0].Text);
        Assert.Contains(edited, b => b.Location.Contains("Ô bảng") && b.Text == "");
        changed[0] = changed[0] with { Id = "invalid" };
        Assert.Throws<ArgumentException>(() => InternalWordDocument.Edit(bytes, changed));
    }

    [Fact]
    public void InsertsTablesInOrderWithMergedCellsBordersAndFillableFields()
    {
        var bytes = Document();
        var blocks = InternalWordDocument.Blocks(bytes);
        var tables = new List<WordTable> {
            new("one", blocks[0].Id, true, [[new("Tên"), new("Giá")], [new("{{CUSTOMER_COMPANY}}", 2)]]),
            new("two", blocks[0].Id, false, [[new("Bảng không viền")]])
        };
        var edited = InternalWordDocument.Edit(bytes, blocks, tables);
        var filled = InternalWordDocument.Fill(edited, new Dictionary<string, string> { ["CUSTOMER_COMPANY"] = "Khách thử nghiệm" });
        using var stream = new MemoryStream(filled);
        using var doc = WordprocessingDocument.Open(stream, false);
        var actual = doc.MainDocumentPart!.Document!.Body!.Elements<Table>().ToList();
        Assert.Equal(3, actual.Count);
        Assert.Contains("Khách thử nghiệm", actual[0].InnerText);
        Assert.Equal(2, actual[0].Descendants<GridSpan>().Last().Val!.Value);
        Assert.Equal(BorderValues.Single, actual[0].Descendants<TopBorder>().Single().Val!.Value);
        Assert.Equal(BorderValues.Nil, actual[1].Descendants<TopBorder>().Single().Val!.Value);
        Assert.Equal("Old amount", actual[2].InnerText);
        Assert.Empty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(actual[0]).Select(e => e.Description));
        Assert.Empty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(actual[1]).Select(e => e.Description));
    }

    [Fact]
    public void RejectsMalformedTablesAndHeaderAnchors()
    {
        var bytes = Document();
        var blocks = InternalWordDocument.Blocks(bytes);
        Assert.Throws<ArgumentException>(() => InternalWordDocument.Edit(bytes, blocks,
            [new("bad", blocks[0].Id, true, [[new("A", 2)], [new("B")]])]));
        Assert.Throws<ArgumentException>(() => InternalWordDocument.Edit(bytes, blocks,
            [new("bad", "missing", true, [[new("A")]])]));
        Assert.Throws<ArgumentException>(() => InternalWordDocument.Edit(bytes, blocks,
            [new("bad", blocks.Last().Id, true, [[new("A")]])]));
        Assert.Throws<ArgumentException>(() => InternalWordDocument.Edit(bytes, blocks,
            [new("bad", blocks[0].Id, true, [[new("A", 13)]])]));
    }

    [Fact]
    public void InsertsAfterExistingTableWithoutNestingAndCanRemoveAddedTable()
    {
        var bytes = Document();
        var blocks = InternalWordDocument.Blocks(bytes);
        var edited = InternalWordDocument.Edit(bytes, blocks, [new("new", blocks[1].Id, true, [[new("New row")]])]);
        using var stream = new MemoryStream(edited);
        using var doc = WordprocessingDocument.Open(stream, false);
        Assert.Equal(new[] {"Old amount", "New row"}, doc.MainDocumentPart!.Document!.Body!.Elements<Table>().Select(t => t.InnerText));
        Assert.Equal(blocks, InternalWordDocument.Blocks(InternalWordDocument.Edit(bytes, blocks, [])));
    }

    [Fact]
    public void InsertsClauseImageAndTableInRequestedOrderAndPreservesImageOnReload()
    {
        var bytes = Document();
        var blocks = InternalWordDocument.Blocks(bytes);
        var result = InternalWordDocument.Edit(bytes, blocks,
            [new("table", "0", true, [[new("Bảng sau điều khoản")]], 2)],
            [new("clause", "0", "clause", "Điều bổ sung", "Dòng một\nDòng hai", Order: 1),
             new("image", "0", "image", "Ảnh minh họa", "", TinyPng, 5, "center", 3)]);
        using var stream = new MemoryStream(result);
        using var doc = WordprocessingDocument.Open(stream, false);
        var children = doc.MainDocumentPart!.Document!.Body!.ChildElements;
        Assert.Equal("Điều bổ sung", children[1].InnerText);
        Assert.Equal("Dòng một", children[2].InnerText);
        Assert.Equal("Dòng hai", children[3].InnerText);
        Assert.IsType<Table>(children[4]);
        Assert.Single(children[5].Descendants<Drawing>());
        Assert.Empty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(children[5]).Select(e => e.Description));
        Assert.Single(doc.MainDocumentPart.ImageParts);
        var extracted = InternalWordDocument.Blocks(result, true);
        Assert.Contains(extracted, b => b.Images?.SingleOrDefault()?.Base64 == TinyPng);
        var reedited = InternalWordDocument.Edit(result, extracted);
        Assert.Contains(InternalWordDocument.Blocks(reedited, true), b => b.Images?.Count == 1);
        Assert.Equal(result, InternalWordDocument.Edit(bytes, blocks,
            [new("table", "0", true, [[new("Bảng sau điều khoản")]], 2)],
            [new("clause", "0", "clause", "Điều bổ sung", "Dòng một\nDòng hai", Order: 1),
             new("image", "0", "image", "Ảnh minh họa", "", TinyPng, 5, "center", 3)]));
    }

    [Fact]
    public void RejectsInvalidImagesDimensionsAndInsertionLocations()
    {
        var bytes = Document(); var blocks = InternalWordDocument.Blocks(bytes);
        Assert.Throws<ArgumentException>(() => InternalWordDocument.Edit(bytes, blocks, additions:
            [new("image", "0", "image", "", "", Convert.ToBase64String("not an image"u8.ToArray()))]));
        Assert.Throws<ArgumentException>(() => InternalWordDocument.Edit(bytes, blocks, additions:
            [new("image", "0", "image", "", "", TinyPng, 30)]));
        Assert.Throws<ArgumentException>(() => InternalWordDocument.Edit(bytes, blocks, additions:
            [new("clause", "unknown", "clause", "Title", "Text")]));
    }

    [Fact]
    public void AcceptsNumericInsertionOrderFromBrowserWithApiJsonOptions()
    {
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        options.Converters.Add(new ContractManagement.API.Internal.StringLongConverter());
        var addition = System.Text.Json.JsonSerializer.Deserialize<WordContent>(
            """{"id":"a","afterBlockId":"0","kind":"clause","title":"Điều mới","text":"Nội dung","order":1,"widthCm":10,"align":"left"}""", options);
        Assert.Equal(1, addition!.Order);
        var table = System.Text.Json.JsonSerializer.Deserialize<WordTable>(
            """{"id":"t","afterBlockId":"0","borders":true,"rows":[[{"text":"Ô mới","colSpan":1}]],"order":2}""", options);
        Assert.Equal(2, table!.Order);
    }

    [Fact]
    public void AddedParagraphsInheritSourceTypographySpacingAndIndentWithoutSectionBreaks()
    {
        var bytes = Document();
        using var sourceStream = new MemoryStream(); sourceStream.Write(bytes); sourceStream.Position = 0;
        using (var sourceDoc = WordprocessingDocument.Open(sourceStream, true))
        {
            var paragraph = sourceDoc.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().First();
            paragraph.ParagraphProperties = new ParagraphProperties(new ParagraphStyleId { Val = "ContractBody" },
                new SpacingBetweenLines { After = "120", Line = "360", LineRule = LineSpacingRuleValues.Auto },
                new Indentation { Left = "720", FirstLine = "240" }, new Justification { Val = JustificationValues.Both },
                new SectionProperties());
            foreach (var run in paragraph.Elements<Run>()) run.RunProperties = new RunProperties(
                new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" }, new Italic(), new FontSize { Val = "28" });
        }
        bytes = sourceStream.ToArray();
        var edited = InternalWordDocument.Edit(bytes, InternalWordDocument.Blocks(bytes), additions:
            [new("p", "0", "paragraph", "", "Đoạn thêm\nDòng tiếp"), new("c", "0", "clause", "Điều mới", "Nội dung")]);
        using var resultStream = new MemoryStream(edited);
        using var resultDoc = WordprocessingDocument.Open(resultStream, false);
        var paragraphs = resultDoc.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ToList();
        foreach (var paragraph in paragraphs.Skip(1))
        {
            Assert.Equal("ContractBody", paragraph.ParagraphProperties!.ParagraphStyleId!.Val!.Value);
            Assert.Equal("360", paragraph.ParagraphProperties.SpacingBetweenLines!.Line!.Value);
            Assert.Equal("720", paragraph.ParagraphProperties.Indentation!.Left!.Value);
            Assert.Equal(JustificationValues.Both, paragraph.ParagraphProperties.Justification!.Val!.Value);
            Assert.Null(paragraph.ParagraphProperties.SectionProperties);
            Assert.Equal("28", paragraph.Elements<Run>().Single().RunProperties!.FontSize!.Val!.Value);
            Assert.Equal("Times New Roman", paragraph.Elements<Run>().Single().RunProperties!.RunFonts!.Ascii!.Value);
        }
        Assert.NotNull(paragraphs.Single(p => p.InnerText == "Điều mới").Elements<Run>().Single().RunProperties!.Bold);
        Assert.NotNull(paragraphs[0].ParagraphProperties!.SectionProperties);
    }

    [Fact]
    public void RejectsInvalidOrOversizedFiles()
    {
        Assert.Throws<ArgumentException>(() => InternalWordDocument.Decode("broken"));
        Assert.Throws<ArgumentException>(() => InternalWordDocument.Decode(new string('A', 14_000_001)));
        Assert.Throws<ArgumentException>(() => InternalWordDocument.Validate("not a docx"u8.ToArray()));
    }
}
