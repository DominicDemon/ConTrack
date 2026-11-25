using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Infrastructure.Persistence.Master;
using ContractManagement.Infrastructure.Persistence.Master.Scans;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContractManagement.Tests.Infrastructure;

/// <summary>Paper-signing screen for a customer who has several legal entities.</summary>
public sealed class SignedEvidenceContractsTests
{
    private sealed class Reviewer : ICurrentEmployee
    {
        public long EmployeeId => 5;
        public bool HasPermission(string permissionCode) => true;
    }

    private static MasterDbContext Seed(string? snapshot)
    {
        var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Customers.Add(new Customer { CustomerId = 1, CustomerPersonalPhone = "0912345001", CustomerPasswordHash = "x", CustomerDisplayName = "Khách ABC", CustomerStatus = 1 });
        db.CustomerBusinesses.AddRange(
            new CustomerBusiness { CustomerBusinessId = 1, CustomerId = 1, CustomerBusinessLegalName = "Công ty Cổ phần ABC", CustomerBusinessCompanyTaxIdentifier = "0101010101" },
            new CustomerBusiness { CustomerBusinessId = 4, CustomerId = 1, CustomerBusinessLegalName = "Công ty TNHH Thương mại ABC", CustomerBusinessCompanyTaxIdentifier = "0404040404" });
        db.ContractDocuments.Add(new ContractDocument { ContractDocumentId = 1, ContractDocumentCode = "HD-1", ContractDocumentName = "Hợp đồng 1",
            ContractDocumentStatus = 3, ContractDocumentCurrentVersionId = 10, EmployeeId = 5, CustomerId = 1, RowVersion = [1] });
        db.ContractVersions.Add(new ContractVersion { ContractVersionId = 10, ContractDocumentId = 1, ContractVersionVersionNo = 1,
            ContractVersionCurrencycode = "VND", ContractVersionRowversion = [1] });
        if (snapshot is not null)
            db.ContractDynamicValues.Add(new ContractDynamicValue { ContractDocumentId = 1, ContractVersionId = 10, FieldKey = "__draft.v1", FieldValue = snapshot });
        db.SaveChanges();
        return db;
    }

    // Only the database is used by the list and the overview of a contract without evidence.
    private static SignedEvidenceService Service(MasterDbContext db) =>
        new(db, null!, new Reviewer(), Options.Create(new ScanOcrOptions()), null!, null!);

    [Fact]
    public async Task CustomerWithTwoLegalEntities_ContractListedOnceAndOverviewOpens()
    {
        await using var db = Seed(snapshot: null);
        var service = Service(db);

        var list = await service.ListContractsAsync(default);
        var overview = await service.GetOverviewAsync(1, default);

        Assert.Single(list);
        Assert.Equal(1, overview.DocumentId);
        // The draft names no entity: the customer's first one.
        Assert.Equal("Công ty Cổ phần ABC", overview.CustomerName);
        Assert.Equal("0101010101", overview.CustomerTaxCode);
    }

    [Fact]
    public async Task Overview_ComparesWithTheLegalEntityChosenInTheContract()
    {
        await using var db = Seed(snapshot: """{"customerId":"1","customerBusinessId":4,"partyType":"BUSINESS"}""");

        var overview = await Service(db).GetOverviewAsync(1, default);

        Assert.Equal("Công ty TNHH Thương mại ABC", overview.CustomerName);
        Assert.Equal("0404040404", overview.CustomerTaxCode);
    }
}
