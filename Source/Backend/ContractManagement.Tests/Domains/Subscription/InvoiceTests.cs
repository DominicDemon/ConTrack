using System.Reflection;
using ContractManagement.API.Domains.DTOs.Requests.Subscription;
using ContractManagement.API.Domains.Email;
using ContractManagement.API.Domains.Payments;
using ContractManagement.API.Domains.Recovery;
using ContractManagement.API.Domains.Services.Subscription;
using ContractManagement.Domains.Controllers.Subscription;
using ContractManagement.Filter;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ContractManagement.Tests.Domains.Subscription;

public sealed class InvoiceTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task PaidProvisioning_QueuesOnce_SnapshotsStayImmutable_OnlyPurchaserCanRead()
    {
        await using var db = SubscriptionTestDb.Create();
        var clock = new FixedClock(Now);
        var gateway = new FakePayOs();
        var service = Public(db, gateway, clock);
        var checkout = await service.CheckoutAsync(Request());
        var invoice = await db.SaasInvoices.SingleAsync();
        var name = invoice.PlanNameSnapshot;
        var amount = invoice.TotalAmount;
        var invoiceService = new InvoiceService(db);
        Assert.Equal("PENDING", (await invoiceService.GetReadinessAsync(invoice.PublicId, default))!.Status);
        Assert.Null(await invoiceService.GetAsync(invoice.PublicId, 1, 1, default));
        var provisioning = new FakeProvisioning(db);
        var fulfillment = Fulfillment(db, gateway, provisioning, clock);
        await fulfillment.RecordPayOsPaymentAsync(invoice.PayosOrderCode!.Value, (long)amount, "TX123", default);
        var paidPending = await invoiceService.GetReadinessAsync(invoice.PublicId, default);
        Assert.True(paidPending!.IsPaid);
        Assert.Equal("PENDING", paidPending.Status);
        await fulfillment.ProvisionPendingAsync(default);
        await fulfillment.ProvisionPendingAsync(default);
        await fulfillment.RecordPayOsPaymentAsync(invoice.PayosOrderCode.Value, (long)amount, "TX123", default);
        Assert.Single(provisioning.Commands);
        Assert.Single(db.InvoiceEmailOutbox);
        Assert.Single(db.SaasInvoices);
        Assert.Single(db.SaasPayments);
        var subscription = await db.Subscriptions.SingleAsync();
        Assert.Equal(1, subscription.PurchaserEmployeeId);
        var version = await db.PlanVersions.Include(x => x.Plan).SingleAsync(x => x.PlanVersionId == subscription.PlanVersionId);
        version.Price = 2; version.Plan.PlanName = "Đã đổi tên";
        await db.SaveChangesAsync();
        var receipt = await invoiceService.GetAsync(invoice.PublicId, subscription.TenantId!.Value, 1, default);
        Assert.NotNull(receipt);
        Assert.Equal(name, receipt.PlanName);
        Assert.Equal(amount, receipt.TotalAmount);
        Assert.Equal(3, receipt.DurationMonths);
        Assert.Null(await invoiceService.GetAsync(invoice.PublicId, subscription.TenantId.Value + 1, 1, default));
        Assert.Null(await invoiceService.GetAsync(invoice.PublicId, subscription.TenantId.Value, 2, default));
        Assert.Equal("READY", (await invoiceService.GetReadinessAsync(invoice.PublicId, default))!.Status);
        Assert.Equal($"/billing/receipts/{invoice.PublicId}", (await service.GetStatusAsync(checkout.PublicId)).ReceiptPath);
        // The endpoint cannot bypass the fresh employee/password check.
        Assert.False(typeof(InvoicesController).GetCustomAttribute<SessionAuthorizeAttribute>()!.AllowWhenPasswordChangeRequired);
    }

    [Fact]
    public async Task Outbox_TestMailbox_EncodesHtml_LeaseAndRepeatDoNotResend()
    {
        await using var db = await ReadyDb();
        var clock = new FixedClock(Now);
        var mailbox = new RecoveryTestMailbox();
        var transport = new FailingEmail();
        var worker = Worker(clock, transport, mailbox, "Test");
        var row = await db.InvoiceEmailOutbox.Include(x => x.Invoice).SingleAsync();
        row.Invoice.BillingName = "<script>bad</script>";
        row.Invoice.BillingEmail = "billing@example.test";
        row.Status = "LEASED"; row.LeaseUntil = Now.AddMinutes(1);
        await db.SaveChangesAsync();
        await worker.ProcessAsync(db, default);
        Assert.Empty(mailbox.Messages);
        clock.Now = Now.AddMinutes(2);
        await worker.ProcessAsync(db, default);
        await worker.ProcessAsync(db, default);
        var mail = Assert.Single(mailbox.Messages);
        Assert.Equal("billing@example.test", mail.Recipient);
        Assert.Contains("&lt;script&gt;", mail.HtmlBody);
        Assert.DoesNotContain("<script>", mail.HtmlBody);
        Assert.Contains("/billing/receipts/", mail.HtmlBody);
        Assert.Contains("Xem xác nhận thanh toán", mail.HtmlBody);
        Assert.Equal(0, transport.Attempts);
        Assert.Equal("SENT", (await db.InvoiceEmailOutbox.SingleAsync()).Status);
    }

    [Fact]
    public async Task SmtpFailure_RetriesFiveWithBackoff_LeavesPaymentAndTenantIntact()
    {
        await using var db = await ReadyDb();
        var clock = new FixedClock(Now);
        var transport = new FailingEmail();
        var worker = Worker(clock, transport, new RecoveryTestMailbox(), "Smtp");
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var before = clock.Now;
            await worker.ProcessAsync(db, default);
            var row = await db.InvoiceEmailOutbox.SingleAsync();
            Assert.Equal(attempt, row.AttemptCount);
            Assert.Equal(before.AddSeconds(30 * Math.Pow(2, attempt - 1)), row.NextAttemptAt);
            await worker.ProcessAsync(db, default);
            Assert.Equal(attempt, transport.Attempts);
            clock.Now = row.NextAttemptAt;
        }
        await worker.ProcessAsync(db, default);
        Assert.Equal(5, transport.Attempts);
        Assert.Equal("FAILED", (await db.InvoiceEmailOutbox.SingleAsync()).Status);
        Assert.Equal("ACTIVE", (await db.Subscriptions.SingleAsync()).Status);
        Assert.Equal("PAID", (await db.SaasInvoices.SingleAsync()).Status);
        Assert.Single(db.Tenants);
    }

    [Fact]
    public async Task TargetedDelivery_DoesNotProcessOtherInvoices_AndDoesNotResend()
    {
        await using var db = await ReadyDb();
        var mailbox = new RecoveryTestMailbox();
        var worker = Worker(new FixedClock(Now), new FailingEmail(), mailbox, "Test");
        var invoice = await db.SaasInvoices.SingleAsync();
        await worker.ProcessAsync(db, default, Guid.NewGuid());
        Assert.Empty(mailbox.Messages);
        Assert.Equal(0, (await db.InvoiceEmailOutbox.SingleAsync()).AttemptCount);
        await worker.ProcessAsync(db, default, invoice.PublicId);
        await worker.ProcessAsync(db, default, invoice.PublicId);
        Assert.Single(mailbox.Messages);
        Assert.Equal("SENT", (await db.InvoiceEmailOutbox.SingleAsync()).Status);
    }

    [Fact]
    public async Task FreeCheckoutAndLegacyInvoices_DoNotCreateReceiptEmails_UnknownPurchaserRequiresReconciliation()
    {
        await using var db = SubscriptionTestDb.Create();
        var clock = new FixedClock(Now);
        var gateway = new FakePayOs();
        var request = Request(); request.PlanVersionId = 1;
        await Public(db, gateway, clock).CheckoutAsync(request);
        await Fulfillment(db, gateway, new FakeProvisioning(db), clock).ProvisionPendingAsync(default);
        Assert.Empty(db.InvoiceEmailOutbox);
        var subscription = await db.Subscriptions.SingleAsync();
        subscription.PurchaserEmployeeId = null;
        await db.SaveChangesAsync();
        var invoice = await db.SaasInvoices.SingleAsync();
        var service = new InvoiceService(db);
        Assert.Equal("RECONCILIATION_REQUIRED", (await service.GetReadinessAsync(invoice.PublicId, default))!.Status);
        Assert.Null(await service.GetAsync(invoice.PublicId, subscription.TenantId!.Value, 1, default));
    }

    private static async Task<CentralDbContext> ReadyDb()
    {
        var db = SubscriptionTestDb.Create(); var clock = new FixedClock(Now); var gateway = new FakePayOs();
        await Public(db, gateway, clock).CheckoutAsync(Request());
        var invoice = await db.SaasInvoices.SingleAsync();
        var fulfillment = Fulfillment(db, gateway, new FakeProvisioning(db), clock);
        await fulfillment.RecordPayOsPaymentAsync(invoice.PayosOrderCode!.Value, (long)invoice.TotalAmount, "TX", default);
        await fulfillment.ProvisionPendingAsync(default);
        return db;
    }

    [Fact]
    public async Task PurchaseHistory_IsScopedToPurchaserAndTenant_PaidOnly_AndPaged()
    {
        await using var db = await ReadyDb();
        var subscription = await db.Subscriptions.SingleAsync();
        for (var i = 1; i <= 23; i++)
            db.SaasInvoices.Add(new()
            {
                InvoiceCode = $"LIST-{i}", CustomerId = subscription.CustomerId!.Value,
                SubscriptionId = subscription.SubscriptionId, BillingName = "Buyer",
                PlanNameSnapshot = "Gói đã mua", Status = i == 22 ? "UNPAID" : "PAID",
                TotalAmount = i == 23 ? 0 : 900000, PaidAt = Now.AddMinutes(i),
            });
        await db.SaveChangesAsync();
        var service = new InvoiceService(db);
        var first = await service.ListAsync(subscription.TenantId!.Value, 1, 0, default);
        Assert.Equal(22, first.Total);
        Assert.Equal(1, first.Page);
        Assert.Equal(20, first.Items.Count);
        Assert.Equal("LIST-21", first.Items[0].InvoiceCode);
        var last = await service.ListAsync(subscription.TenantId.Value, 1, int.MaxValue, default);
        Assert.Equal(2, last.Page);
        Assert.Equal(2, last.Items.Count);
        Assert.Empty((await service.ListAsync(subscription.TenantId.Value, 2, 1, default)).Items);
        Assert.Empty((await service.ListAsync(subscription.TenantId.Value + 1, 1, 1, default)).Items);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Backfill_RequiresMatchingCreationAudits_NeverUsesReassignedAccount(bool reassigned)
    {
        await using var db = await ReadyDb();
        var subscription = await db.Subscriptions.SingleAsync();
        subscription.PurchaserEmployeeId = null;
        var correlation = $"subscription-{subscription.PublicId}";
        db.SecurityAudits.Add(new CentralSecurityAudit { TenantId = subscription.TenantId, Action = "TenantProvisioned",
            Result = "Success", CorrelationId = correlation, TargetType = "Tenant", OccurredAt = Now });
        await db.SaveChangesAsync();
        var factory = new MemoryTenantFactory();
        await using (var tenant = factory.Create("unused"))
        {
            tenant.TblEmployees.Add(new TblEmployee { EmployeeId = 7, EmployeeAccount = "original@example.test",
                EmployeeFullName = "Buyer", DateCreated = reassigned ? Now.AddDays(1) : Now });
            tenant.TblAuthorizationAudits.Add(new TblAuthorizationAudit { TenantId = subscription.TenantId!.Value,
                Action = "EmployeeCreated", Result = "Success", TargetType = "Employee", TargetId = "original@example.test",
                ActorType = "SystemAdmin", CorrelationId = correlation, OccurredAt = Now });
            await tenant.SaveChangesAsync();
        }
        var before = await db.InvoiceEmailOutbox.CountAsync();
        var backfill = new InvoicePurchaserBackfill(db, factory);
        Assert.Equal(reassigned ? 0 : 1, await backfill.RunAsync(default));
        Assert.Equal(reassigned ? (int?)null : 7, (await db.Subscriptions.SingleAsync()).PurchaserEmployeeId);
        Assert.Equal(0, await backfill.RunAsync(default));
        Assert.Equal(before, await db.InvoiceEmailOutbox.CountAsync());
    }

    [Fact]
    public async Task ConcurrentOutboxClaims_OnlyOneRevisionWins()
    {
        var name = Guid.NewGuid().ToString();
        await using var a = SubscriptionTestDb.Create(name);
        a.InvoiceEmailOutbox.Add(new() { InvoiceId = 1, NextAttemptAt = Now });
        await a.SaveChangesAsync();
        await using var b = SubscriptionTestDb.Create(name);
        var rowA = await a.InvoiceEmailOutbox.SingleAsync();
        var rowB = await b.InvoiceEmailOutbox.SingleAsync();
        rowA.Status = rowB.Status = "LEASED";
        rowA.Revision = Guid.NewGuid(); rowB.Revision = Guid.NewGuid();
        await a.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
    }

    private sealed class MemoryTenantFactory : ITenantDbContextFactory
    {
        private readonly DbContextOptions<DbDtctechContext> options = new DbContextOptionsBuilder<DbDtctechContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        public DbDtctechContext Create(string connectionString) => new(options);
    }
    private static SubscriptionCheckoutRequest Request() => new() { PlanVersionId = 2, TenantCode = "receipt-org", TenantName = "Công ty", FullName = "Nguyễn A", Email = "buyer@example.test", Phone = "0912345678" };
    private static PublicSubscriptionService Public(CentralDbContext db, FakePayOs gateway, TimeProvider clock) => new(db, clock, gateway, Options.Create(new PayOsOptions()), NullLogger<PublicSubscriptionService>.Instance);
    private static SubscriptionFulfillmentService Fulfillment(CentralDbContext db, FakePayOs gateway, FakeProvisioning provisioning, TimeProvider clock) => new(db, gateway, provisioning, new RecordingEmail(), Options.Create(new AccountRecoveryOptions { TenantFrontendBaseUrl = "http://app.test" }), clock, NullLogger<SubscriptionFulfillmentService>.Instance);
    private static InvoiceEmailWorker Worker(TimeProvider clock, IEmailTransport email, RecoveryTestMailbox mailbox, string provider) => new(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), Options.Create(new InvoiceEmailOptions { Enabled = true, Provider = provider }), Options.Create(new AccountRecoveryOptions { TenantFrontendBaseUrl = "http://app.test" }), email, mailbox, clock, NullLogger<InvoiceEmailWorker>.Instance);
    private sealed class FailingEmail : IEmailTransport
    {
        public int Attempts { get; private set; }
        public Task SendAsync(EmailMessage message, CancellationToken ct) { Attempts++; throw new IOException("SMTP unavailable"); }
    }
}
