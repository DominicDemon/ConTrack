using ContractManagement.Domain.Entities.Master;
using ContractManagement.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Infrastructure;

/// <summary>A template written only by SQL (010-demo-template.sql) is read from CONTRACT_TEMPLATE + CONTRACT_TEMPLATE_FIELDS.</summary>
public class DatabaseTemplateTests
{
    [Fact]
    public async Task SqlDefinedTemplateIsReadFromDatabaseAndUploadsWithoutDefinitionAreSkipped()
    {
        await using var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.ContractTemplates.AddRange(
            new ContractTemplate { ContractTemplateId = 1, ContractTemplateCode = "INTERNAL_DEMO_V1", ContractTemplateName = "Mẫu minh họa", ContractTemplateDocumentType = 1, IsActive = true, CreatedDate = DateTime.UtcNow },
            new ContractTemplate { ContractTemplateId = 2, ContractTemplateCode = "UPLOADED", ContractTemplateName = "Mẫu tải lên", ContractTemplateDocumentType = 1, IsActive = true, CreatedDate = DateTime.UtcNow });
        db.ContractTemplateFields.AddRange(
            new ContractTemplateField { ContractTemplateFieldId = 1, ContractTemplateId = 1, ContractTemplateFieldKey = "CUSTOMER_COMPANY", ContractTemplateFieldLabel = "Tên khách hàng", ContractTemplateFieldDataType = "TEXT", IsRequired = true },
            new ContractTemplateField { ContractTemplateFieldId = 2, ContractTemplateId = 1, ContractTemplateFieldKey = "SCOPE", ContractTemplateFieldLabel = "Phạm vi cung cấp", ContractTemplateFieldDataType = "TERM", ExtraOptions = "" },
            new ContractTemplateField { ContractTemplateFieldId = 3, ContractTemplateId = 1, ContractTemplateFieldKey = "PAYMENT", ContractTemplateFieldLabel = "Thanh toán", ContractTemplateFieldDataType = "TERM" },
            new ContractTemplateField { ContractTemplateFieldId = 4, ContractTemplateId = 2, ContractTemplateFieldKey = "SCOPE", ContractTemplateFieldLabel = "Phạm vi", ContractTemplateFieldDataType = "TERM" });
        // Template 2 was uploaded (its source file is stored) but its definition file is missing: it stays hidden, as before.
        db.FileStorages.Add(new FileStorage { FileStorageFileid = 1, FileStorageObjecttype = "INTERNAL_TEMPLATE_SOURCE", FileStorageObjectid = 2,
            FileStorageFiletype = "SOURCE", FileStorageContenttype = "application/octet-stream", FileStorageSha256 = "00",
            FileStorageFilename = "a.docx", FileStorageFilepath = "a.docx", FileStorageStoragekey = "a.docx", FileStorageUploadeddate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var templates = await new InternalRepository(db, new InternalTemplateStorage(Path.GetTempPath())).Templates(default);

        var template = Assert.Single(templates);
        Assert.Equal(1, template.Id);
        Assert.Equal(["SCOPE", "PAYMENT"], template.Terms.Select(t => t.Key));
        Assert.All(template.Terms, t => Assert.Equal("", t.Content));
        Assert.Contains(template.Fields, f => f.Key == "CUSTOMER_COMPANY" && f.Required);
        Assert.DoesNotContain(template.Fields, f => f.Key == "SCOPE");
    }
}
