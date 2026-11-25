using ContractManagement.Infrastructure.Persistence.Master;
using ContractManagement.Application.Features.Internal;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ContractManagement.Tests.Infrastructure;

public class InternalWordDocumentTests
{
    internal const string TinyPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=";

    private static byte[] MixedFonts()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            var header = main.AddNewPart<HeaderPart>();
            header.Header = new Header(new Paragraph(new Run(new RunProperties(new RunFonts { Ascii = "Calibri" }), new Text("Đầu trang"))));
            var footer = main.AddNewPart<FooterPart>();
            footer.Footer = new Footer(new Paragraph(new Run(new RunProperties(new RunFonts { Ascii = "Arial" }), new Text("Chân trang"))));
            main.AddNewPart<StyleDefinitionsPart>().Styles = new Styles(
                new DocDefaults(new RunPropertiesDefault(new RunPropertiesBaseStyle(new RunFonts { Ascii = "Calibri" }))),
                new Style(new StyleRunProperties(new RunFonts { HighAnsi = "Arial" })) { StyleId = "TableText", Type = StyleValues.Paragraph });
            main.AddNewPart<FootnotesPart>().Footnotes = new Footnotes(
                new Footnote(new Paragraph(new Run(new Text("Chú thích tiếng Việt")))) { Id = 1 });
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = new Numbering(
                new AbstractNum(new Level(new StartNumberingValue { Val = 1 }, new NumberingFormat { Val = NumberFormatValues.Bullet },
                    new LevelText { Val = "\uf0b7" }, new NumberingSymbolRunProperties(new RunFonts { Ascii = "Symbol" })) { LevelIndex = 0 },
                    new Level(new StartNumberingValue { Val = 1 }, new NumberingFormat { Val = NumberFormatValues.Bullet },
                        new LevelText { Val = "-" }, new NumberingSymbolRunProperties(new RunFonts { Ascii = "Arial" })) { LevelIndex = 1 }) { AbstractNumberId = 1 });
            main.Document = new Document(new Body(
                new Paragraph(new Run(new RunProperties(new RunFonts { Ascii = "Arial", AsciiTheme = ThemeFontValues.MinorHighAnsi },
                    new Bold(), new Italic(), new FontSize { Val = "30" }, new Underline { Val = UnderlineValues.Single }), new Text("Hợp đồng thử font"))),
                new Table(new TableRow(new TableCell(new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "TableText" }),
                    new Run(new Text("Nội dung ô bảng")))))),
                new Paragraph(new Run(new Text("Times New Roman"), new FootnoteReference { Id = 1 })),
                new SectionProperties(new HeaderReference { Id = main.GetIdOfPart(header), Type = HeaderFooterValues.Default },
                    new FooterReference { Id = main.GetIdOfPart(footer), Type = HeaderFooterValues.Default })));
        }
        var source = stream.ToArray();
        return InternalWordDocument.Edit(source, InternalWordDocument.Blocks(source), [], [new("font-test-image", "0", "image", "", "", TinyPng)]);
    }

    [Fact]
    public void ContractFontReplacesThemeAndStyleFontsWithoutChangingFormattingOrImages()
    {
        var result = InternalWordDocument.StandardizeContractFont(MixedFonts());
        using var doc = WordprocessingDocument.Open(new MemoryStream(result), false);
        var main = doc.MainDocumentPart!;
        var parts = new OpenXmlPart[] { main, main.StyleDefinitionsPart!, main.HeaderParts.Single(), main.FooterParts.Single(), main.FootnotesPart! };
        foreach (var part in parts)
        {
            Assert.NotEmpty(part.RootElement!.Descendants<RunFonts>());
            Assert.All(part.RootElement.Descendants<RunFonts>(), font =>
            {
                Assert.Equal("Times New Roman", font.Ascii?.Value);
                Assert.Equal("Times New Roman", font.HighAnsi?.Value);
                Assert.Equal("Times New Roman", font.EastAsia?.Value);
                Assert.Equal("Times New Roman", font.ComplexScript?.Value);
                Assert.Null(font.AsciiTheme);
            });
        }
        var title = main.Document!.Descendants<Run>().First(r => r.InnerText == "Hợp đồng thử font").RunProperties!;
        Assert.NotNull(title.Bold);
        Assert.NotNull(title.Italic);
        Assert.Equal("30", title.FontSize!.Val!.Value);
        Assert.Equal(UnderlineValues.Single, title.Underline!.Val!.Value);
        Assert.Single(main.Document.Descendants<Table>());
        Assert.Equal(new[] { "Symbol", "Times New Roman" }, main.NumberingDefinitionsPart!.Numbering!.Descendants<RunFonts>().Select(f => f.Ascii!.Value));
        using var image = main.ImageParts.Single().GetStream();
        using var bytes = new MemoryStream();
        image.CopyTo(bytes);
        Assert.Equal(TinyPng, Convert.ToBase64String(bytes.ToArray()));
    }

    [Fact]
    public void WordOverridesApplyOnlySelectedSettings()
    {
        var source = MixedFonts();
        var format = ContractManagement.Domain.Internal.ContractFormat.Default with { WordOverrides = [] };
        Assert.Same(source, InternalWordDocument.ApplyPrintFormat(source, format));

        var edited = InternalWordDocument.ApplyPrintFormat(source, format with { WordOverrides = ["margins", "spacing"], LineSpacing = 1.5 });
        using var doc = WordprocessingDocument.Open(new MemoryStream(edited), false);
        var main = doc.MainDocumentPart!;
        var body = main.Document!.Body!;
        Assert.Equal(1701u, body.Descendants<PageMargin>().Single().Left!.Value);
        var paragraph = body.Elements<Paragraph>().First();
        Assert.Equal("360", paragraph.ParagraphProperties!.SpacingBetweenLines!.Line!.Value);
        Assert.Equal("Arial", paragraph.Descendants<RunFonts>().Single().Ascii!.Value);
        Assert.Equal("30", paragraph.Descendants<FontSize>().Single().Val!.Value);
        Assert.Single(body.Descendants<Table>());
        Assert.Single(main.ImageParts);
    }

    [LibreOfficeContractFontFact]
    public async Task LibreOfficeRendersMixedTemplateTextInTimesNewRoman()
    {
        var converter = new InternalWordDocument(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var pdfBytes = await converter.Pdf(InternalWordDocument.StandardizeContractFont(MixedFonts()), default);
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(pdfBytes);
        var letters = pdf.GetPages().SelectMany(p => p.Letters).Where(l => l.Value.Any(char.IsLetter)).ToArray();
        Assert.NotEmpty(letters);
        Assert.All(letters, letter => Assert.Contains("TimesNewRoman", letter.FontName?.Replace(" ", "") ?? "", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Hợp đồng thử font", pdf.GetPage(1).Text);
    }

    private sealed class LibreOfficeContractFontFactAttribute : FactAttribute
    {
        public LibreOfficeContractFontFactAttribute()
        {
            if (!OperatingSystem.IsWindows() || ContractManagement.Domains.Services.ContractTemplate.LibreOfficeContractTemplatePdfRenderer.ResolveExecutablePath(null) is null)
                Skip = "Requires a Windows host with LibreOffice and Times New Roman installed.";
        }
    }
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
    public void ReordersOriginalTableAndParagraphsAndInsertsAfterTheMovedClause()
    {
        var source = Document();
        var blocks = InternalWordDocument.Blocks(source);
        var paragraph = blocks.Single(b => b.Text.StartsWith("Hello "));
        var cell = blocks.Single(b => b.Text == "Old amount");
        var title = Guid.NewGuid().ToString();
        var bytes = InternalWordDocument.Edit(source, blocks,
            additions: [new("img", title, "image", "", "", TinyPng)],
            bodyOrder: [new(cell.BodyUnit!), new(title, "Điều mới", true), new(paragraph.BodyUnit!)]);
        var actual = InternalWordDocument.Blocks(bytes, true);
        var body = actual.Where(b => b.BodyUnit != null).ToList();
        Assert.Equal("Old amount", body[0].Text);
        Assert.True(body[1].ClauseTitle);
        Assert.Equal(TinyPng, body[2].Images!.Single().Base64);
        Assert.Equal(paragraph.Text, body[3].Text);
        Assert.Contains(actual, b => b.Location == "Đầu trang");
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
