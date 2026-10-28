using ContractManagement.Controllers.Admin;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Saas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.ComponentModel.DataAnnotations;

namespace ContractManagement.Tests.Domains.Subscription;
public class AdminPlanTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(1, true)] [InlineData(24, true)] [InlineData(25, false)]
    public void DurationMonthsIsBounded(int months, bool valid)
    {
        var input = new PlansController.PlanInput { PlanCode = "PLAN", PlanName = "Gói", DurationDays = 730, DurationMonths = months, MaxUsers = 2, MaxContracts = 10, MaxScanPages = 20 };
        Assert.Equal(valid, Validator.TryValidateObject(input, new ValidationContext(input), new List<ValidationResult>(), true));
    }
    [Fact]
    public async Task RepricingPreservesDurationAndHistoryAndRejectsStaleRevision()
    {
        await using var db = new CentralDbContext(new DbContextOptionsBuilder<CentralDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        db.Plans.Add(new SaasPlan { PlanId = 10, PlanCode = "24M", PlanName = "24 tháng", IsPaid = true, Versions = new List<SaasPlanVersion> { new() { PlanVersionId = 100, VersionNumber = 1, DurationMonths = 24, DurationDays = 720, Price = 1000000, Status = "ACTIVE", BillingCycle = "CUSTOM" } } });
        await db.SaveChangesAsync();
        var http = new DefaultHttpContext { Session = new Session() }; http.Session.SetInt32("SystemAdminId", 7);
        var controller = new PlansController(db) { ControllerContext = new ControllerContext { HttpContext = http } };
        Assert.IsType<OkObjectResult>(await controller.Price(10, new() { Price = 2000000, Revision = 0 }, default));
        var plan = await db.Plans.Include(p => p.Versions).SingleAsync();
        Assert.Equal(2, plan.Versions.Count);
        var active = Assert.Single(plan.Versions, v => v.Status == "ACTIVE");
        Assert.Equal(720, active.DurationDays); Assert.Equal(24, active.DurationMonths);
        Assert.Equal(1000000, plan.Versions.Single(v => v.PlanVersionId == 100).Price);
        var audit = Assert.Single(await db.PlanAudits.ToListAsync());
        Assert.Equal(7, audit.ActorId); Assert.Contains("2000000", audit.AfterJson);
        Assert.IsType<ConflictObjectResult>(await controller.Price(10, new() { Price = 3000000, Revision = 0 }, default));
        Assert.Single(await db.PlanAudits.ToListAsync());
    }
    private sealed class Session : ISession
    {
        private readonly Dictionary<string, byte[]> values = new();
        public bool IsAvailable => true; public string Id => "test"; public IEnumerable<string> Keys => values.Keys;
        public void Clear() => values.Clear(); public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
