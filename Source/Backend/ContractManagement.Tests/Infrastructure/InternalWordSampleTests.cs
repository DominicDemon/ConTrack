using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ContractManagement.Tests.Infrastructure;

public sealed class WordSampleFactAttribute : FactAttribute
{
    public WordSampleFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CONTRACK_WORD_SAMPLE_ROOT"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CONTRACK_MASTER_TEST_CONNECTION")))
            Skip = "Set CONTRACK_WORD_SAMPLE_ROOT and CONTRACK_MASTER_TEST_CONNECTION for local Word acceptance.";
    }
}

[Collection("InternalMaster")]
public class InternalWordSampleTests
{
    [WordSampleFact]
    public async Task InsertedParagraphMatchesSourceFontSizeAndIndentInRenderedPdf()
    {
        var root = Environment.GetEnvironmentVariable("CONTRACK_WORD_SAMPLE_ROOT")!;
        await using var source = new FileStream(Path.Combine(root, "20140407-HDKT-DTC-BASAO.doc"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var data = new MemoryStream(); await source.CopyToAsync(data);
        var word = new InternalWordDocument(new ConfigurationBuilder().Build());
        var normalized = await word.Normalize(new("sample.doc", Convert.ToBase64String(data.ToArray())), default);
        var blocks = InternalWordDocument.Blocks(normalized);
        var details = InternalWordDetails.Extract(normalized, "BASAO");
        Assert.Contains("BA SAO", details.Fields["CUSTOMER_COMPANY"]);
        Assert.Equal(16000000m, details.TotalAmount);
        await File.WriteAllTextAsync(Path.Combine(root,"ConTrack","Source",".local","word-acceptance","detected-details.json"), System.Text.Json.JsonSerializer.Serialize(details));
        var anchor = blocks.First(b => b.Text.StartsWith("Năng lực và nhu cầu"));
        var edited = InternalWordDocument.Edit(normalized, blocks, additions:
            [new("format-check", anchor.Id, "paragraph", "", "NOIDUNGMOI điều khoản hợp đồng có hiệu lực")]);
        var pdf = await word.Pdf(edited, default);
        var output = Path.Combine(root, "ConTrack", "Source", ".local", "word-acceptance", "paragraph-formatting.pdf");
        await File.WriteAllBytesAsync(output, pdf);
        using var document = UglyToad.PdfPig.PdfDocument.Open(pdf);
        var words = document.GetPages().SelectMany(p => p.GetWords()).ToList();
        var original = words.First(w => w.Text == "Năng");
        var inserted = words.Single(w => w.Text == "NOIDUNGMOI");
        Assert.Equal(original.Letters[0].FontName, inserted.Letters[0].FontName);
        Assert.Equal(original.Letters[0].PointSize, inserted.Letters[0].PointSize);
        Assert.InRange(Math.Abs(original.BoundingBox.Left - inserted.BoundingBox.Left), 0, 1);
    }

    [WordSampleFact]
    public async Task FiveSamplesConvertPreviewActivateReloadAndRender()
    {
        var sampleRoot = Environment.GetEnvironmentVariable("CONTRACK_WORD_SAMPLE_ROOT")!;
        var outputRoot = Path.Combine(sampleRoot, "ConTrack", "Source", ".local", "word-acceptance");
        Directory.CreateDirectory(outputRoot);
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable("CONTRACK_MASTER_TEST_CONNECTION")).Options;
        await using var db = new MasterDbContext(options);
        var actor = await db.Employees.Where(e => e.EmployeeUsername == "sale.local").Select(e => e.EmployeeId).SingleAsync();
        var storage = new InternalTemplateStorage(Path.Combine(outputRoot, "storage"));
        var word = new InternalWordDocument(new ConfigurationBuilder().Build());
        var service = new InternalWordTemplates(db, storage, word, new EphemeralDataProtectionProvider(),
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        var files = Directory.GetFiles(sampleRoot, "20*.doc*").OrderBy(p => p).ToList();
        Assert.Equal(5, files.Count);
        foreach (var file in files)
        {
            // Word can keep the user-provided sample open while the local acceptance run reads it.
            await using var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sourceBytes = new MemoryStream();
            await sourceStream.CopyToAsync(sourceBytes);
            var source = new TemplateFileInput(Path.GetFileName(file), Convert.ToBase64String(sourceBytes.ToArray()));
            var extraction = await service.Extract(source, actor, default);
            var type = source.FileName.Contains("ĐNTT") ? 5 : source.FileName.Contains("BienBan") ? 3 : source.FileName.Contains("PLHD") ? 2 : 1;
            var fields = new List<DraftField>();
            var blocks = extraction.Blocks;
            if (type == 1)
                blocks = blocks.Select(b => b with { Text = b.Text.Replace("CÔNG TY TNHH DỊCH VỤ ĂN UỐNG BA SAO", "{{CUSTOMER_COMPANY}}") }).ToList();
            if (type == 5)
            {
                fields.Add(new("APPENDIX_CODE", "Số phụ lục", true));
                fields.Add(new("REQUEST_AMOUNT", "Số tiền đề nghị", true, "NUMBER"));
                blocks = blocks.Select(b => b with { Text = System.Text.RegularExpressions.Regex.Replace(b.Text,
                    @"DTC-BAOSAO[^\s]*?/PLHĐ", "{{APPENDIX_CODE}}").Replace("4.000.000", "{{REQUEST_AMOUNT}}") }).ToList();
                Assert.Contains(blocks, b => b.Text.Contains("{{APPENDIX_CODE}}"));
                Assert.Contains(blocks, b => b.Text.Contains("{{REQUEST_AMOUNT}}"));
            }
            var values = extraction.CoreFields.ToDictionary(f => f.Key, f => "Dữ liệu thử");
            values["APPENDIX_CODE"] = "PL-TEST-2026";
            values["REQUEST_AMOUNT"] = "7500000";
            if (type != 5) { values.Remove("APPENDIX_CODE"); values.Remove("REQUEST_AMOUNT"); }
            var input = new WordTemplateInput(source.FileName, source.Base64, extraction.DocumentBase64, "TEST_WORD_" + type,
                type, blocks, fields, values, extraction.SourceToken);
            if (type == 1)
            {
                input = input with { Tables = [new("acceptance-table", blocks[0].Id, true,
                    [[new("BẢNG BỔ SUNG KIỂM THỬ", 2)], [new("Khách hàng"), new("{{CUSTOMER_COMPANY}}")]])] };
                await Assert.ThrowsAsync<ArgumentException>(() => service.Preview(input with {
                    Tables = [new("invalid", blocks[0].Id, true, [[new("{{UNMAPPED_TABLE_FIELD}}")]])]
                }, actor, default));
            }
            var preview = await service.Preview(input, actor, default);
            if (type == 3)
            {
                input = input with { ManualContent = true, Additions = [
                    new("manual-clause", blocks[0].Id, "clause", "Điều bổ sung", "Nội dung sửa tay {{GIU_NGUYEN}}", Order: 1),
                    new("manual-image", blocks[0].Id, "image", "Ảnh thử nghiệm", "", InternalWordDocumentTests.TinyPng, 3, "center", 2)] };
                preview = await service.Preview(input, actor, default);
                await Assert.ThrowsAsync<ArgumentException>(() => service.Activate(new(input with { Additions = [] }, preview.PreviewToken), actor, default));
            }
            if (type == 1)
                await Assert.ThrowsAsync<ArgumentException>(() => service.Activate(new(input with { Tables = [] }, preview.PreviewToken), actor, default));
            var unmapped = blocks.ToList();
            unmapped[0] = unmapped[0] with { Text = "{{UNMAPPED_FIELD}}" };
            await Assert.ThrowsAsync<ArgumentException>(() => service.Preview(input with { Blocks = unmapped, ManualContent = false }, actor, default));
            await Assert.ThrowsAsync<ArgumentException>(() => service.Preview(input with { Fields = [new("TOTAL_AMOUNT", "Trùng trường lõi")] }, actor, default));
            var pdf = Convert.FromBase64String(preview.PdfBase64);
            Assert.True(pdf.AsSpan().StartsWith("%PDF-"u8));
            await File.WriteAllBytesAsync(Path.Combine(outputRoot, Path.GetFileNameWithoutExtension(file) + ".pdf"), pdf);
            await Assert.ThrowsAsync<ArgumentException>(() => service.Activate(new(input with { Name = "Changed" }, preview.PreviewToken), actor, default));
            await Assert.ThrowsAsync<ArgumentException>(() => service.Activate(new(input, preview.PreviewToken), actor + 999999, default));
            TemplateDefinition? template = null;
            try
            {
                template = await service.Activate(new(input, preview.PreviewToken), actor, default);
                Assert.Equal(type, template.DocumentType);
                var repeated = await service.Activate(new(input, preview.PreviewToken), actor, default);
                Assert.Equal(template.Id, repeated.Id);
                var metadata = await db.FileStorages.SingleAsync(f => f.FileStorageObjecttype == "INTERNAL_TEMPLATE" && f.FileStorageObjectid == template.Id);
                var reloaded = await storage.Read(metadata, default);
                Assert.Equal(template.WordSha256, reloaded.WordSha256);
                var savedBytes = await File.ReadAllBytesAsync(Path.Combine(storage.Root, reloaded.WordStorageKey!));
                if (type == 3)
                {
                    Assert.True(reloaded.ManualWordContent);
                    var draft = new DraftContent { TemplateId = template.Id, CustomerId = 0, Name = "", Terms = reloaded.Terms.ToList() };
                    var manual = await service.Render(draft, reloaded, false, default);
                    Assert.Contains(InternalWordDocument.Blocks(manual), b => b.Text.Contains("{{GIU_NGUYEN}}"));
                    Assert.Contains(InternalWordDocument.Blocks(manual, true), b => b.Images?.Any(i => i.Base64 == InternalWordDocumentTests.TinyPng) == true);
                }
                var filled = InternalWordDocument.Fill(savedBytes, values);
                if (type == 5)
                {
                    var filledText = string.Join(" ", InternalWordDocument.Blocks(filled).Select(b => b.Text));
                    Assert.Contains("PL-TEST-2026", filledText);
                    Assert.Contains("7500000", filledText);
                    Assert.DoesNotContain("4.000.000", filledText);
                    values["REQUEST_AMOUNT"] = "9000000";
                    Assert.Contains("9000000", string.Join(" ", InternalWordDocument.Blocks(InternalWordDocument.Fill(savedBytes, values)).Select(b => b.Text)));
                }
                if (type == 1)
                {
                    var content = new DraftContent { TemplateId = template.Id, CustomerId = 1, Name = "Hợp đồng thử",
                        Fields = template.Fields.ToDictionary(f => f.Key, _ => "Thử nghiệm"), Terms = template.Terms.ToList() };
                    var rendered = await service.Render(content, reloaded, false, default);
                    InternalWordDocument.Validate(rendered);
                    Assert.Contains("BẢNG BỔ SUNG KIỂM THỬ", string.Join(" ", InternalWordDocument.Blocks(rendered).Select(b => b.Text)));
                    Assert.Contains(reloaded.Terms, t => t.Content.Contains("BẢNG BỔ SUNG KIỂM THỬ"));
                    Assert.Contains("Bên A: Thử nghiệm", string.Join(" ", InternalWordDocument.Blocks(rendered).Select(b => b.Text)));
                    await File.WriteAllBytesAsync(Path.Combine(outputRoot, "contract-rendered.docx"), rendered);
                }
            }
            finally
            {
                if (template is not null)
                {
                    db.ChangeTracker.Clear();
                    var stored = await db.FileStorages.Where(f => f.FileStorageObjectid == template.Id && f.FileStorageObjecttype.StartsWith("INTERNAL_TEMPLATE")).ToListAsync();
                    await db.FileStorages.Where(f => f.FileStorageObjectid == template.Id && f.FileStorageObjecttype.StartsWith("INTERNAL_TEMPLATE")).ExecuteDeleteAsync();
                    await db.ContractTemplateFields.Where(f => f.ContractTemplateId == template.Id).ExecuteDeleteAsync();
                    await db.ContractTemplates.Where(t => t.ContractTemplateId == template.Id).ExecuteDeleteAsync();
                    await db.AuditLogs.Where(a => a.EntityName == "CONTRACT_TEMPLATE" && a.EntityId == template.Id.ToString()).ExecuteDeleteAsync();
                    foreach (var item in stored) File.Delete(Path.Combine(storage.Root, item.FileStorageStoragekey));
                }
            }
        }
    }
}
