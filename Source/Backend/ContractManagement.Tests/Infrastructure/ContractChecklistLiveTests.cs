using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ContractManagement.Tests.Infrastructure;

[Collection("InternalMaster")]
public class ContractChecklistLiveTests
{
    [MasterDatabaseFact]
    public async Task ChecklistIsScopedVersionedConcurrentAndDoesNotResetProgress()
    {
        var connection = Environment.GetEnvironmentVariable("CONTRACK_MASTER_TEST_CONNECTION")!;
        await using var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseSqlServer(connection).Options);
        var storage = new InternalTemplateStorage(Path.Combine(Path.GetTempPath(), "contrack-checklist-tests"));
        var repository = new InternalRepository(db, storage);
        var word = new InternalWordTemplates(db, storage, new InternalWordDocument(new ConfigurationBuilder().Build()), new EphemeralDataProtectionProvider());
        var service = new ContractChecklistService(db, word);
        var progress = new ContractProgressService(db, service);
        var suffix = Guid.NewGuid().ToString("N")[..16];
        var employee = new Employee { EmployeeCode = "C3_" + suffix, EmployeeUsername = "c3_" + suffix, EmployeeDisplayName = "TEST3 Sale", EmployeePasswordHash = "TEST_DISABLED", EmployeeStatus = "ACTIVE" };
        var customer = new Customer { CustomerDisplayName = "TEST3 Customer " + suffix, CustomerPersonalPhone = "09" + DateTime.UtcNow.Ticks.ToString()[^8..], CustomerPasswordHash = "TEST_DISABLED" };
        db.AddRange(employee, customer); await db.SaveChangesAsync();
        var actorId = employee.EmployeeId;
        var sale = new InternalUser(actorId, "Sale test", "test", ["draft.write"]);
        var office = sale with { Permissions = ["contract.check", "template.manage"] };
        var stranger = sale with { Id = -1 };
        var templateRow = new ContractTemplate { ContractTemplateCode = "CHECK_" + Guid.NewGuid().ToString("N"), ContractTemplateName = "TEST3 checklist", ContractTemplateDocumentType = 1, ContractTemplateLanguageMode = 1, IsActive = true, CreatedDate = DateTime.UtcNow };
        db.Add(templateRow); await db.SaveChangesAsync();
        var id = templateRow.ContractTemplateId;
        Assert.Equal(403, (await Assert.ThrowsAsync<InternalAppException>(() => service.SaveTemplate(id, new(null, ["payment"]), sale, default))).Status);
        await service.SaveTemplate(id, new(null, ["payment", "warranty"]), office, default);
        var config = await service.Template(id, office, default);
        Assert.Equal(409, (await Assert.ThrowsAsync<InternalAppException>(() => service.SaveTemplate(id, new(null, []), office, default))).Status);
        var template = new TemplateDefinition(id, "TEST3 checklist", "test", true, [], [new("PAY", "Thanh toán", "Bên A thanh toán toàn bộ giá trị cho Bên B trong vòng mười ngày sau khi nghiệm thu.")]);
        var content = new DraftContent { TemplateId = id, TemplateSnapshot = template, Name = "TEST3 " + Guid.NewGuid().ToString("N"), CustomerId = customer.CustomerId, PartyType = "PERSON", Terms = template.Terms };
        DraftValidation.Prepare(content, template);
        var draft = await repository.Save(null, new(content, null, null), sale, default);
        var view = await service.Inspect(draft.Id, sale, default);
        Assert.True(view.Configured);
        Assert.True(view.Findings.Single(x => x.Key == "payment").Found);
        Assert.False(view.Findings.Single(x => x.Key == "warranty").Found);
        Assert.Equal(403, (await Assert.ThrowsAsync<InternalAppException>(() => service.Inspect(draft.Id, stranger, default))).Status);
        Assert.Empty((await progress.List(new(Search: content.Name), stranger, default)).Items);
        var before = Assert.Single((await progress.List(new(Search: content.Name), sale, default)).Items);
        Assert.Equal("DRAFT", before.Stage);
        Assert.Equal(1, before.WarningCount);
        var secondPage = await progress.List(new(Search: content.Name, Page: 2), sale, default);
        Assert.Empty(secondPage.Items);
        Assert.Equal(1, secondPage.Total);
        Assert.Equal(25, secondPage.PageSize);
        var decision = new ChecklistDecision(draft.RowVersion, draft.VersionRowVersion, view.RowVersion, view.ContentHash!, "warranty", "NOT_APPLICABLE", "Không cung cấp thiết bị");
        Assert.Equal(403, (await Assert.ThrowsAsync<InternalAppException>(() => service.Decide(draft.Id, decision, sale, default))).Status);
        async Task<int> ConcurrentDecision()
        {
            await using var other = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseSqlServer(connection).Options);
            var otherWord = new InternalWordTemplates(other, storage, new InternalWordDocument(new ConfigurationBuilder().Build()), new EphemeralDataProtectionProvider());
            try { await new ContractChecklistService(other, otherWord).Decide(draft.Id, decision, office, default); return 200; }
            catch (InternalAppException error) { return error.Status; }
        }
        Assert.Equal(new[] {200, 409}, (await Task.WhenAll(ConcurrentDecision(), ConcurrentDecision())).Order().ToArray());
        db.ChangeTracker.Clear();
        var after = Assert.Single((await progress.List(new(Search: content.Name), sale, default)).Items);
        Assert.Equal(before.EnteredAt, after.EnteredAt);
        Assert.Equal(0, after.WarningCount);
        Assert.Empty((await progress.List(new(Search: content.Name, Warnings: true), sale, default)).Items);
        await service.SaveTemplate(id, new(config.RowVersion, ["effect"]), office, default);
        Assert.Equal(new[] {"payment", "warranty"}, (await service.Inspect(draft.Id, office, default)).Rules);
        draft = (await repository.Draft(draft.Id, default))!;
        draft.Content.Name += " changed";
        draft = await repository.Save(draft.Id, new(draft.Content, draft.RowVersion, draft.VersionRowVersion), sale, default);
        view = await service.Inspect(draft.Id, office, default);
        Assert.Null(view.Findings.Single(x => x.Key == "warranty").Resolution);
        await service.Decide(draft.Id, new(draft.RowVersion, draft.VersionRowVersion, view.RowVersion, view.ContentHash!, "", "APPLY_TEMPLATE", ""), office, default);
        Assert.Equal(new[] {"effect"}, (await service.Inspect(draft.Id, office, default)).Rules);
        draft = (await repository.Draft(draft.Id, default))!;
        draft.Content.TemplateSnapshot = draft.Content.TemplateSnapshot! with { WordStorageKey = "missing-test-file.docx", WordSha256 = new string('0', 64) };
        await repository.Save(draft.Id, new(draft.Content, draft.RowVersion, draft.VersionRowVersion), sale, default);
        var unreadable = Assert.Single((await progress.List(new(Search: draft.Content.Name, Warnings: true), sale, default)).Items);
        Assert.NotNull(unreadable.InspectionError);
    }
}
