using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Infrastructure;

public sealed class ContractNumberRuleTests
{
    [MasterDatabaseFact]
    public async Task FirstWriterOwnsConfigurationAndConcurrentAllocationsAreDistinct()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable("CONTRACK_MASTER_TEST_CONNECTION")).Options;
        await using var setup = new MasterDbContext(options);
        var owner = new CustomerBusiness {
            CustomerId = await setup.Customers.Select(x => x.CustomerId).FirstAsync(),
            CustomerBusinessLegalName = "TEST numbering", CustomerBusinessCompanyTaxIdentifier = Guid.NewGuid().ToString("N")[..13]
        };
        setup.CustomerBusinesses.Add(owner);
        await setup.SaveChangesAsync();
        var id = owner.CustomerBusinessId;
        var sale = new InternalUser(1,"Sale","sale",["draft.write"]);
        var office = sale with { Permissions = ["contract.check"] };
        var code = "T" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var input = new NumberRule { CompanyCode = code, TypeCode = "KD" };
        async Task<int> Create()
        {
            await using var db = new MasterDbContext(options);
            try { await new ContractNumberRules(db).Save(id, owner.CustomerId, input, sale, false, default); return 200; }
            catch (InternalAppException e) { return e.Status; }
        }
        async Task<string> Issue(DateTime day, bool commit = true)
        {
            await using var db = new MasterDbContext(options);
            await using var tx = await db.Database.BeginTransactionAsync();
            var result = (await db.Database.SqlQuery<string>($"EXEC dbo.AllocateFormattedContractNumber @owner={id}, @day={day}").ToListAsync()).Single();
            if (commit) await tx.CommitAsync();
            return result;
        }
        try
        {
            var rules = new ContractNumberRules(setup);
            Assert.Equal(400,(await Assert.ThrowsAsync<InternalAppException>(() => rules.Save(id,-1,input,sale,false,default))).Status);
            Assert.Equal(new[] {200,409}, (await Task.WhenAll(Create(),Create())).Order().ToArray());
            Assert.Equal(403,(await Assert.ThrowsAsync<InternalAppException>(() => rules.Save(id,owner.CustomerId,input,sale,true,default))).Status);
            Assert.Equal(403,(await Assert.ThrowsAsync<InternalAppException>(() => rules.Save(0,null,input,sale,false,default))).Status);
            var day = new DateTime(2030,12,31);
            var numbers = await Task.WhenAll(Issue(day), Issue(day));
            Assert.Equal(new[] {code+"-KD-203012-000001",code+"-KD-203012-000002"}, numbers.Order().ToArray());
            input.TypeCode = "DV"; input.IncludeDay = true;
            await rules.Save(id,owner.CustomerId,input,office,true,default);
            Assert.Equal(code+"-DV-20301231-000003", await Issue(day,false));
            Assert.Equal(code+"-DV-20301231-000003", await Issue(day));
            Assert.Equal(code+"-DV-20310101-000001", await Issue(day.AddDays(1)));
        }
        finally
        {
            await setup.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.BUSINESS_NUMBER_COUNTER WHERE owner_id={id}");
            await setup.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.CONTRACT_NUMBER_RULE WHERE owner_id={id}");
            setup.CustomerBusinesses.Remove(owner);
            await setup.SaveChangesAsync();
        }
    }
}
