using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Infrastructure;

/// <summary>Template library (Tạo mẫu văn bản) and documents attached to a contract (019-template-parent.sql).</summary>
public class TemplateHierarchyTests
{
    private static readonly InternalUser Manager = new(1, "Hành chính", "hc", ["template.manage", "draft.write", "draft.manage.all"]);
    private static readonly InternalUser Sale = new(2, "Sale", "sale", ["draft.write"]);

    private static ContractTemplate Template(long id, int type, long? parent = null, bool active = true) => new()
    {
        ContractTemplateId = id, ContractTemplateCode = "T" + id, ContractTemplateName = "Mẫu " + id, ContractTemplateDocumentType = type,
        ParentTemplateId = parent, IsActive = active, CreatedDate = DateTime.UtcNow
    };

    private static ContractDocument Document(long id, long template, int type, long employee = 2, long customer = 10) => new()
    {
        ContractDocumentId = id, ContractTemplateId = template, ContractDocumentType = type, EmployeeId = employee, CustomerId = customer,
        ContractDocumentName = "Văn bản " + id, ContractDocumentCode = "HD-" + id, CreatedDate = DateTime.UtcNow.AddMinutes(id), RowVersion = []
    };

    private static async Task<(MasterDbContext Db, InternalRepository Repository)> Seed()
    {
        var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        // 1: contract template with 2 (appendix) and 3 (handover) attached; 4: another contract template; 5: a free appendix.
        db.ContractTemplates.AddRange(Template(1, 1), Template(2, 2, 1), Template(3, 3, 1), Template(4, 1), Template(5, 2));
        db.ContractDocuments.AddRange(Document(100, 1, 1), Document(101, 2, 2));
        db.ContractDocumentRelations.Add(new ContractDocumentRelation { ParentDocumentId = 100, ChildDocumentId = 101,
            ContractDocumentRelationType = InternalRepository.ChildRelation, CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return (db, new InternalRepository(db, new InternalTemplateStorage(Path.GetTempPath())));
    }

    [Fact]
    public async Task OnlyAChildTypeAttachesAndOnlyToAnActiveContractTemplate()
    {
        var (db, repository) = await Seed();
        await using var _ = db;

        var attached = await repository.UpdateTemplate(5, new("Phụ lục giá", 2, 4), Manager, default);
        Assert.Equal(4, attached.ParentTemplateId);
        Assert.Equal("Mẫu 4", attached.ParentName);

        // A contract template is never a child; a child never hangs under another child.
        await Assert.ThrowsAsync<ArgumentException>(() => repository.UpdateTemplate(4, new("Mẫu 4", 1, 1), Manager, default));
        Assert.Equal(404, (await Assert.ThrowsAsync<InternalAppException>(() => repository.UpdateTemplate(5, new("x", 2, 2), Manager, default))).Status);
        // A contract template with children keeps its type.
        await Assert.ThrowsAsync<ArgumentException>(() => repository.UpdateTemplate(1, new("Mẫu 1", 2, null), Manager, default));
        Assert.Equal(403, (await Assert.ThrowsAsync<InternalAppException>(() => repository.UpdateTemplate(5, new("x", 2, null), Sale, default))).Status);
    }

    [Fact]
    public async Task HiddenTemplatesStayInTheLibraryAndCanBeRestored()
    {
        var (db, repository) = await Seed();
        await using var _ = db;

        await repository.DeleteTemplate(3, Manager, default);
        var library = await repository.TemplateLibrary(default);
        Assert.Equal(5, library.Count);
        Assert.False(library.Single(t => t.Id == 3).Active);
        Assert.Equal(2, library.Single(t => t.Id == 1).ChildCount);
        Assert.Equal(1, library.Single(t => t.Id == 1).DocumentCount);
        Assert.DoesNotContain(await repository.Templates(default), t => t.Id == 3);

        Assert.True((await repository.RestoreTemplate(3, Manager, default)).Active);
        Assert.Equal(404, (await Assert.ThrowsAsync<InternalAppException>(() => repository.RestoreTemplate(3, Manager, default))).Status);
    }

    [Fact]
    public async Task AContractListsItsAttachedDocumentsAndChildTemplates()
    {
        var (db, repository) = await Seed();
        await using var _ = db;

        var contract = await repository.Related(100, Sale, default);
        Assert.Null(contract.Parent);
        Assert.Equal([101L], contract.Children.Select(c => c.Id));
        Assert.Equal([2L, 3L], contract.ChildTemplates.Select(t => t.Id));

        var appendix = await repository.Related(101, Sale, default);
        Assert.Equal(100, appendix.Parent?.Id);
        Assert.Empty(appendix.ChildTemplates);
        // Another sale's contract is not theirs to read.
        Assert.Equal(403, (await Assert.ThrowsAsync<InternalAppException>(() =>
            repository.Related(100, new InternalUser(3, "Sale khác", "s2", ["draft.write"]), default))).Status);
    }

    [Fact]
    public async Task AChildDocumentUsesAChildTemplateOfItsContractAndTheSameCustomer()
    {
        var (db, repository) = await Seed();
        await using var _ = db;

        await repository.CheckChildDocument(100, new DraftContent { TemplateId = 3, CustomerId = 10 }, Sale, default);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.CheckChildDocument(100, new DraftContent { TemplateId = 5, CustomerId = 10 }, Sale, default));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.CheckChildDocument(100, new DraftContent { TemplateId = 3, CustomerId = 11 }, Sale, default));
        // Only a contract takes attached documents.
        await Assert.ThrowsAsync<ArgumentException>(() => repository.CheckChildDocument(101, new DraftContent { TemplateId = 3, CustomerId = 10 }, Sale, default));
        Assert.Equal(404, (await Assert.ThrowsAsync<InternalAppException>(() => repository.CheckChildDocument(999, new DraftContent { TemplateId = 3, CustomerId = 10 }, Sale, default))).Status);
    }
}
