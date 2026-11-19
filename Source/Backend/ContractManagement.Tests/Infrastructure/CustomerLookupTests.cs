using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Infrastructure;

/// <summary>Draft creation: Sale finds an existing customer by email, tax code or phone; each legal entity is its own result.</summary>
public sealed class CustomerLookupTests
{
    private static InternalRepository Repo(MasterDbContext db) => new(db, null!);

    private static MasterDbContext Seed()
    {
        var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Customers.AddRange(
            new Customer { CustomerId = 1, CustomerDisplayName = "Nguyễn An", CustomerEmail = "an@x.vn", CustomerPersonalPhone = "0901234567", CustomerPasswordHash = "x", CustomerStatus = 1 },
            new Customer { CustomerId = 2, CustomerDisplayName = "Khách khóa", CustomerEmail = "locked@x.vn", CustomerPersonalPhone = "0907654321", CustomerPasswordHash = "x", CustomerStatus = 0 });
        db.CustomerBusinesses.AddRange(
            new CustomerBusiness { CustomerBusinessId = 10, CustomerId = 1, CustomerBusinessLegalName = "Công ty A", CustomerBusinessCompanyTaxIdentifier = "0101234567",
                CustomerBusinessOfficeAddress = "Hà Nội", CustomerBusinessRepresentativeName = "Nguyễn An" },
            new CustomerBusiness { CustomerBusinessId = 11, CustomerId = 1, CustomerBusinessLegalName = "Công ty B", CustomerBusinessCompanyTaxIdentifier = "0309-876-543",
                CustomerBusinessEmail = "b@x.vn" });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task PhoneOrEmailFindsThePersonAndEachLegalEntitySeparately()
    {
        await using var db = Seed();
        foreach (var term in new[] { "0901234567", "+84 901 234 567", "AN@X.VN" })
        {
            var found = await Repo(db).LookupCustomers(term, default);
            Assert.Equal([null, 10L, 11L], found.Select(r => r.BusinessId));   // the person, then one result per legal entity
            Assert.All(found, r => Assert.Equal(1, r.Customer.Id));
        }
        var company = (await Repo(db).LookupCustomers("0901234567", default)).Single(r => r.BusinessId == 10);
        // Bên A comes filled from the legal entity.
        Assert.Equal(("Công ty A", "0101234567", "Hà Nội"), (company.Customer.Fields["CUSTOMER_COMPANY"], company.Customer.Fields["CUSTOMER_TAX_CODE"], company.Customer.Fields["CUSTOMER_ADDRESS"]));
    }

    [Fact]
    public async Task TaxCodeFindsOnlyThatEntityAndLockedOrUnknownFindNothing()
    {
        await using var db = Seed();
        Assert.Equal([11L], (await Repo(db).LookupCustomers("0309876543", default)).Select(r => r.BusinessId));   // separators ignored
        Assert.Equal([11L], (await Repo(db).LookupCustomers("b@x.vn", default)).Select(r => r.BusinessId));
        Assert.Empty(await Repo(db).LookupCustomers("locked@x.vn", default));
        Assert.Empty(await Repo(db).LookupCustomers("0999999999", default));
        Assert.Empty(await Repo(db).LookupCustomers("  ", default));
    }

    [Fact]
    public async Task ProfileRefillsBenAFromThePersonOrTheChosenEntity()
    {
        await using var db = Seed();
        var person = await Repo(db).CustomerProfileFor(1, null, default);
        Assert.Equal(("Nguyễn An", null), (person!.Customer.Fields["CUSTOMER_COMPANY"], person.BusinessId));
        var entity = await Repo(db).CustomerProfileFor(1, 11, default);
        Assert.Equal(("Công ty B", 11L), (entity!.Customer.Fields["CUSTOMER_COMPANY"], entity.BusinessId));
        Assert.Null((await Repo(db).CustomerProfileFor(1, 999, default))!.BusinessId);   // unknown entity: the person
        Assert.Null(await Repo(db).CustomerProfileFor(2, null, default));                // locked customer
    }
}
