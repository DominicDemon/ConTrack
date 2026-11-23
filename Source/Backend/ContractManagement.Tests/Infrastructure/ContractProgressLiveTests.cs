using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Infrastructure;

[Collection("InternalMaster")]
public class ContractProgressLiveTests
{
    [Theory]
    [InlineData(true, null, true)]
    [InlineData(false, "{\"officeOrigin\":true}", false)]
    [InlineData(null, "{\"officeOrigin\":true}", true)]
    [InlineData(null, "{\"officeOrigin\":false}", false)]
    [InlineData(null, "{\"partyType\":\"BUSINESS\"}", false)]
    [InlineData(null, null, false)]
    [InlineData(null, "invalid", false)]
    public void WorkflowOriginUsesOnlyTheSnapshotOrCreationAudit(bool? snapshotOrigin, string? audit, bool expected)
    {
        Assert.Equal(expected, InternalRepository.ResolveOfficeOrigin(snapshotOrigin, audit));
    }

    [MasterDatabaseFact]
    public async Task ProgressIsScopedAndPaginatesWithHistoricalChecklistData()
    {
        var connection = Environment.GetEnvironmentVariable("CONTRACK_MASTER_TEST_CONNECTION")!;
        await using var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseSqlServer(connection).Options);
        var repository = new InternalRepository(db, new InternalTemplateStorage(Path.GetTempPath()));
        var progress = new ContractProgressService(db);
        var suffix = Guid.NewGuid().ToString("N")[..16];
        var employee = new Employee { EmployeeCode = "C3_" + suffix, EmployeeUsername = "c3_" + suffix,
            EmployeeDisplayName = "TEST3 Sale", EmployeePasswordHash = "TEST_DISABLED", EmployeeStatus = "ACTIVE" };
        var customer = new Customer { CustomerDisplayName = "TEST3 Customer " + suffix,
            CustomerPersonalPhone = "09" + DateTime.UtcNow.Ticks.ToString()[^8..], CustomerPasswordHash = "TEST_DISABLED" };
        var templateRow = new ContractTemplate { ContractTemplateCode = "PROGRESS_" + suffix, ContractTemplateName = "TEST3 Progress",
            ContractTemplateDocumentType = 1, ContractTemplateLanguageMode = 1, IsActive = true, CreatedDate = DateTime.UtcNow };
        db.AddRange(employee, customer, templateRow);
        await db.SaveChangesAsync();
        var sale = new InternalUser(employee.EmployeeId, "Sale test", "test", ["draft.write"]);
        var template = new TemplateDefinition(templateRow.ContractTemplateId, templateRow.ContractTemplateName, "test", true, [],
            [new("PAY", "Thanh toán", "Bên A thanh toán trong vòng mười ngày sau khi nghiệm thu.")]);
        var content = new DraftContent { TemplateId = template.Id, TemplateSnapshot = template, Name = "TEST3 " + suffix,
            CustomerId = customer.CustomerId, PartyType = "PERSON", Terms = template.Terms };
        DraftValidation.Prepare(content, template);
        var draft = await repository.Save(null, new(content, null, null), sale, default);
        var office = sale with { Permissions = ["contract.check"] };
        draft.Content.OfficeOrigin = true;
        var edited = await repository.Save(draft.Id, new(draft.Content, draft.RowVersion, draft.VersionRowVersion), office, default);
        Assert.Equal(false, edited.Content.OfficeOrigin);

        content.Name += " Office";
        content.OfficeOrigin = false;
        var officeDraft = await repository.Save(null, new(content, null, null), office, default);
        Assert.Equal(true, officeDraft.Content.OfficeOrigin);
        officeDraft.Content.OfficeOrigin = false;
        var editedOffice = await repository.Save(officeDraft.Id, new(officeDraft.Content, officeDraft.RowVersion, officeDraft.VersionRowVersion), sale, default);
        Assert.Equal(true, editedOffice.Content.OfficeOrigin);
        var versionId = await db.ContractDocuments.Where(d => d.ContractDocumentId == draft.Id).Select(d => d.ContractDocumentCurrentVersionId).SingleAsync();
        db.Add(new ContractChecklist { VersionId = versionId, Configured = true, Rules = "[\"warranty\"]" });
        await db.SaveChangesAsync();

        Assert.Empty((await progress.List(new(Search: draft.Code), sale with { Id = -1 }, default)).Items);
        var row = Assert.Single((await progress.List(new(Search: draft.Code), sale, default)).Items);
        Assert.Equal("DRAFT", row.Stage);
        var secondPage = await progress.List(new(Search: draft.Code, Page: 2), sale, default);
        Assert.Empty(secondPage.Items);
        Assert.Equal(1, secondPage.Total);
        Assert.Equal(25, secondPage.PageSize);
    }
}
