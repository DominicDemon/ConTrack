using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Infrastructure;

[Collection("InternalMaster")]
public class InternalDraftTests
{
    private static TemplateDefinition Template => new(1, "Demo", "v1", true,
        [new("COMPANY", "Công ty", true)], [new("LOCKED", "Cố định", "Giữ nguyên", false)]);
    private static DraftContent Content() => new() { TemplateId = 1, CustomerId = 1, Name = "Nháp thử", Terms = [new("LOCKED", "Cố định", "Giữ nguyên", false)],
        Items = [new(null, "Dịch vụ", 2, 100000, 10, 0)], VatPercent = 10 };

    [Fact]
    public void CalculatesMoneyAndAllowsIncompleteDraftWithoutTrustingClientTotals()
    {
        var input = Content(); input.Totals = new(1, 2, 3, 999);
        var result = InternalDraftPolicy.Prepare(input, Template);
        Assert.Equal(new DraftTotals(200000, 20000, 18000, 198000), result.Totals);
        Assert.Contains("Công ty", result.MissingFields);
    }
    [Fact]
    public void RejectsConflictingDiscountsAndInvalidDates()
    {
        var input = Content(); input.Items = [input.Items[0] with { DiscountAmount = 5 }];
        Assert.Throws<ArgumentException>(() => InternalDraftPolicy.Prepare(input, Template));
        input = Content(); input.EffectiveDate = new(2026, 12, 1); input.ExpireDate = new(2026, 1, 1);
        Assert.Throws<ArgumentException>(() => InternalDraftPolicy.Prepare(input, Template));
    }
    [Fact]
    public void ProtectsFixedTermsAndStripsExecutableMarkup()
    {
        var input = Content(); input.Terms.Clear();
        Assert.Throws<ArgumentException>(() => InternalDraftPolicy.Prepare(input, Template));
        input = Content(); input.Name = "<script>alert(1)</script><b>Hợp đồng</b>";
        Assert.Equal("Hợp đồng", InternalDraftPolicy.Prepare(input, Template).Name);
    }
    [Fact]
    public void SaleCannotAccessOtherOwnersButOfficeCan()
    {
        Assert.Throws<InternalAppException>(() => InternalDraftService.RequireAccess(new(1, "Sale", "sale", ["draft.write"]), 2));
        InternalDraftService.RequireAccess(new(1, "Office", "office", ["draft.write", "draft.manage.all"]), 2);
    }

    [MasterDatabaseFact]
    public async Task LiveDraftRoundTripConcurrencyAuthorizationAndSnapshot()
    {
        var connection = Environment.GetEnvironmentVariable("CONTRACK_MASTER_TEST_CONNECTION")!;
        await using var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseSqlServer(connection).Options);
        var root = Environment.GetEnvironmentVariable("CONTRACK_INTERNAL_TEMPLATE_ROOT")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../private-storage/internal-templates"));
        var repository = new InternalRepository(db, new InternalTemplateStorage(root));
        var service = new InternalDraftService(repository);
        var saleId = await db.Employees.Where(e => e.EmployeeUsername == "sale.local").Select(e => e.EmployeeId).SingleAsync();
        var officeId = await db.Employees.Where(e => e.EmployeeUsername == "office.local").Select(e => e.EmployeeId).SingleAsync();
        var sale = (await repository.GetUser(saleId, default))!; var office = (await repository.GetUser(officeId, default))!;
        var template = (await repository.Templates(default)).First();
        var customer = await repository.CreateCustomer(new("TEST_" + Guid.NewGuid().ToString("N"), "0" + Random.Shared.NextInt64(100000000, 999999999), null, null, null, null), saleId, default);
        var ids = new List<long>();
        try
        {
            var input = new DraftContent { TemplateId = template.Id, CustomerId = customer.Id, Name = "Nháp kiểm thử", Terms = template.Terms.ToList(),
                Items = [new(null, "Hạng mục kiểm thử", 2, 100000, 10, 0)], VatPercent = 10 };
            var draft = await service.Save(null, new(input, null, null), sale, default); ids.Add(draft.Id);
            Assert.Equal(198000, draft.Content.Totals.Total);
            Assert.NotEmpty(draft.Content.MissingFields);
            Assert.Equal(1, await db.ContractVersions.CountAsync(v => v.ContractDocumentId == draft.Id));
            Assert.Equal(1, await db.ContractDocumentItems.CountAsync(v => v.ContractDocumentId == draft.Id));
            var c = await db.Customers.SingleAsync(c => c.CustomerId == customer.Id); c.CustomerDisplayName += " changed"; await db.SaveChangesAsync();
            var reloaded = await service.Get(draft.Id, sale, default);
            Assert.Equal(customer.Name, reloaded.Content.Fields["CUSTOMER_CONTACT_NAME"]);
            reloaded.Content.Name = "Đã cập nhật";
            var updated = await service.Save(draft.Id, new(reloaded.Content, draft.RowVersion, draft.VersionRowVersion), office, default);
            Assert.NotEqual(draft.RowVersion, updated.RowVersion);
            var stale = await Assert.ThrowsAsync<InternalAppException>(() => service.Save(draft.Id, new(reloaded.Content, draft.RowVersion, draft.VersionRowVersion), sale, default));
            Assert.Equal(409, stale.Status);
            var officeDraft = await service.Save(null, new(input, null, null), office, default); ids.Add(officeDraft.Id);
            Assert.Equal(403, (await Assert.ThrowsAsync<InternalAppException>(() => service.Get(officeDraft.Id, sale, default))).Status);
            var before = await db.ContractDocuments.CountAsync();
            input.Items = [new(null, "Sai", -1, 1, 0, 0)];
            await Assert.ThrowsAsync<ArgumentException>(() => service.Save(null, new(input, null, null), sale, default));
            Assert.Equal(before, await db.ContractDocuments.CountAsync());
            // Force a real SQL failure after document/version insertion to verify atomic rollback.
            input.Items = [];
            input.Fields = new() { [new string('X', 101)] = "Too long for field_key" };
            await Assert.ThrowsAsync<DbUpdateException>(() => repository.Save(null, new(input, null, null), sale, default));
            db.ChangeTracker.Clear();
            Assert.Equal(before, await db.ContractDocuments.CountAsync());
        }
        finally
        {
            db.ChangeTracker.Clear();
            foreach (var id in ids)
            {
                await db.ContractDynamicValues.Where(x => x.ContractDocumentId == id).ExecuteDeleteAsync();
                await db.ContractTerms.Where(x => x.ContractDocumentId == id).ExecuteDeleteAsync();
                await db.ContractDocumentItems.Where(x => x.ContractDocumentId == id).ExecuteDeleteAsync();
                await db.ContractVersions.Where(x => x.ContractDocumentId == id).ExecuteDeleteAsync();
                await db.ContractDocuments.Where(x => x.ContractDocumentId == id).ExecuteDeleteAsync();
                await db.AuditLogs.Where(x => x.EntityName == "CONTRACT_DOCUMENTS" && x.EntityId == id.ToString()).ExecuteDeleteAsync();
            }
            await db.Customers.Where(x => x.CustomerId == customer.Id).ExecuteDeleteAsync();
            await db.AuditLogs.Where(x => x.EntityName == "CUSTOMER" && x.EntityId == customer.Id.ToString()).ExecuteDeleteAsync();
        }
    }
}
