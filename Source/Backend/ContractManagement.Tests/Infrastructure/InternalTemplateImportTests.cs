using ContractManagement.Application.Features.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ContractManagement.Tests.Infrastructure;

[Collection("InternalMaster")]
public class InternalTemplateImportTests
{
    private static byte[] Word()
    {
        using var stream = new MemoryStream();
        using (var word = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph(new Run(new Text("Hợp đồng thử nghiệm"))), new Paragraph(new Run(new Text("Điều khoản thanh toán")))));
            main.Document.Save();
        }
        return stream.ToArray();
    }
    private static MasterDbContext Context(string? connection = null) => new(new DbContextOptionsBuilder<MasterDbContext>().UseSqlServer(connection ?? "Server=localhost;Database=unused;Integrated Security=true").Options);
    [Fact]
    public void ReadsWordParagraphsAndRejectsInvalidFile()
    {
        using var db = Context(); var importer = new InternalTemplateImport(db, new(Path.GetTempPath()));
        var result = importer.Extract(new("sample.docx", Convert.ToBase64String(Word())));
        Assert.Contains("Hợp đồng thử nghiệm\nĐiều khoản thanh toán", result.Content);
        Assert.NotNull(result.Warning);
        Assert.Throws<ArgumentException>(() => importer.Extract(new("fake.pdf", Convert.ToBase64String(Word()))));
        Assert.Throws<ArgumentException>(() => importer.Extract(new("sample.exe", "AAAA")));
        Assert.Throws<ArgumentException>(() => importer.Extract(new("sample.docx", "broken")));
    }
    [Fact]
    public void ReadsPdfAndFlagsPagesWithoutText()
    {
        using var db = Context(); var importer = new InternalTemplateImport(db, new(Path.GetTempPath()));
        var builder = new PdfDocumentBuilder(); var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText("Contract payment terms", 12, new PdfPoint(25, 700), font);
        var result = importer.Extract(new("sample.pdf", Convert.ToBase64String(builder.Build())));
        Assert.Contains("Contract payment terms", result.Content);
        var blank = new PdfDocumentBuilder();
        blank.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        Assert.NotNull(importer.Extract(new("scan.pdf", Convert.ToBase64String(blank.Build()))).Warning);
    }
    [MasterDatabaseFact]
    public async Task StoresReviewedTemplateAndSourceThenReloadsDefinition()
    {
        await using var db = Context(Environment.GetEnvironmentVariable("CONTRACK_MASTER_TEST_CONNECTION"));
        var root = Path.Combine(Path.GetTempPath(), "contract-import-test-" + Guid.NewGuid().ToString("N"));
        var storage = new InternalTemplateStorage(root); var importer = new InternalTemplateImport(db, storage);
        var actor = await db.Employees.Where(e => e.EmployeeUsername == "sale.local").Select(e => e.EmployeeId).SingleAsync();
        long? id = null;
        try
        {
            var result = await importer.Save(new("sample.docx", Convert.ToBase64String(Word()), "TEST_IMPORT", "Nội dung đã kiểm tra"), actor, default);
            id = result.Id;
            var files = await db.FileStorages.Where(f => f.FileStorageObjectid == id && (f.FileStorageObjecttype == "INTERNAL_TEMPLATE" || f.FileStorageObjecttype == "INTERNAL_TEMPLATE_SOURCE")).ToListAsync();
            Assert.Equal(2, files.Count);
            var loaded = await storage.Read(files.Single(f => f.FileStorageObjecttype == "INTERNAL_TEMPLATE"), default);
            Assert.True(loaded.IsImported); Assert.False(loaded.IsDemo);
            Assert.Equal("Nội dung đã kiểm tra", loaded.Terms[0].Content);
        }
        finally
        {
            db.ChangeTracker.Clear();
            if (id.HasValue)
            {
                await db.FileStorages.Where(f => f.FileStorageObjectid == id && (f.FileStorageObjecttype == "INTERNAL_TEMPLATE" || f.FileStorageObjecttype == "INTERNAL_TEMPLATE_SOURCE")).ExecuteDeleteAsync();
                await db.ContractTemplateFields.Where(f => f.ContractTemplateId == id).ExecuteDeleteAsync();
                await db.ContractTemplates.Where(t => t.ContractTemplateId == id).ExecuteDeleteAsync();
                await db.AuditLogs.Where(a => a.EntityName == "CONTRACT_TEMPLATE" && a.EntityId == id.ToString()).ExecuteDeleteAsync();
            }
            if (Directory.Exists(root)) { foreach (var file in Directory.GetFiles(root)) File.Delete(file); Directory.Delete(root); }
        }
    }
}
