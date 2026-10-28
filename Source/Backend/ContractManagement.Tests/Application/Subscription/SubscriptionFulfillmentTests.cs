using ContractManagement.API.Domains.DTOs.Requests.Subscription;
using ContractManagement.API.Domains.Payments;
using ContractManagement.API.Domains.Recovery;
using ContractManagement.API.Domains.Services.Subscription;
using ContractManagement.Domains.Controllers.Subscription;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace ContractManagement.Tests.Domains.Subscription;

public sealed class SubscriptionFulfillmentTests
{
    private static readonly DateTime Now = new(2026, 10, 27, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PasswordGenerator_1000Times_Always12CharsWithDigitUpperLower()
    {
        var seen = new HashSet<string>();
        for (var i = 0; i < 1000; i++)
        {
            var password = TenantPasswordGenerator.Generate();
            Assert.Equal(12, password.Length);
            Assert.Contains(password, char.IsAsciiDigit);
            Assert.Contains(password, char.IsAsciiLetterUpper);
            Assert.Contains(password, char.IsAsciiLetterLower);
            Assert.True(PasswordRules.IsValid(password));
            seen.Add(password);
        }
        Assert.Equal(1000, seen.Count);
    }

    [Fact]
    public async Task Webhook_SentTwice_CreatesOnePayment_MarksPaid_AndConsumesVoucherOnce()
    {
        var (db, payOs, fulfillment, publicId) = await PaidCheckoutAsync(voucher: "TEST10");
        var orderCode = payOs.Created.Single().OrderCode;

        await fulfillment.HandleWebhookAsync(Webhook(orderCode, 810_000), default);
        await fulfillment.HandleWebhookAsync(Webhook(orderCode, 810_000), default);

        var payment = await db.SaasPayments.SingleAsync();
        Assert.Equal(("PAYOS", "SUCCESS", "FT123"), (payment.PaymentProvider, payment.Status, payment.PaymentReference));
        Assert.Equal("PAID", (await db.SaasInvoices.SingleAsync()).Status);
        var subscription = await db.Subscriptions.SingleAsync();
        Assert.Equal((true, "PENDING", publicId), (subscription.IsPaid, subscription.Status, subscription.PublicId));
        Assert.Equal(1, (await db.Promotions.SingleAsync()).UsageCount);
    }

    [Fact]
    public async Task Webhook_InvalidSignature_Returns400_UnknownOrder_Returns200()
    {
        var (db, payOs, fulfillment, _) = await PaidCheckoutAsync();
        var controller = new PayOsWebhookController(fulfillment);

        var bad = Webhook(payOs.Created.Single().OrderCode, 900_000);
        bad.Signature = "forged";
        Assert.IsType<BadRequestObjectResult>(await controller.Receive(bad, default));
        Assert.IsType<OkObjectResult>(await controller.Receive(Webhook(123, 2000), default)); // payload test của PayOS
        Assert.Empty(db.SaasPayments);

        // Gateway thật với key giả: chữ ký sai bị từ chối mà không gọi mạng.
        var real = new PayOsGateway(Options.Create(new PayOsOptions { ClientId = "c", ApiKey = "a", ChecksumKey = "k" }));
        var realController = new PayOsWebhookController(Fulfillment(db, real, new FakeProvisioning(db), new RecordingEmail()));
        Assert.IsType<BadRequestObjectResult>(await realController.Receive(Webhook(1, 1, "deadbeef"), default));
    }

    /// <summary>
    /// Khóa công thức ký trong docs/SUBSCRIPTION_E2E_TESTING.md: HMAC-SHA256(checksumKey, các trường data
    /// sắp xếp theo tên, nối "key=value" bằng "&"), hex thường. Body JSON đi qua model binding như request thật.
    /// </summary>
    [Fact]
    public async Task Webhook_SignedWithDocumentedFormula_IsAcceptedByRealPayOsGateway()
    {
        const string checksumKey = "test-checksum-key";
        var data = new SortedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["orderCode"] = 123456789L, ["amount"] = 810000L, ["description"] = "CTABC123", ["accountNumber"] = "12345678",
            ["reference"] = "FT-LOCAL-1", ["transactionDateTime"] = "2026-10-27 15:00:00", ["currency"] = "VND",
            ["paymentLinkId"] = "local-link", ["code"] = "00", ["desc"] = "success", ["counterAccountBankId"] = "",
            ["counterAccountBankName"] = "", ["counterAccountName"] = "", ["counterAccountNumber"] = "",
            ["virtualAccountName"] = "", ["virtualAccountNumber"] = ""
        };
        var signed = string.Join("&", data.Select(kv => $"{kv.Key}={kv.Value}"));
        var signature = Convert.ToHexStringLower(System.Security.Cryptography.HMACSHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(checksumKey), System.Text.Encoding.UTF8.GetBytes(signed)));
        var json = System.Text.Json.JsonSerializer.Serialize(new { code = "00", desc = "success", success = true, data, signature });
        var webhook = System.Text.Json.JsonSerializer.Deserialize<Webhook>(json, System.Text.Json.JsonSerializerOptions.Web)!;

        var gateway = new PayOsGateway(Options.Create(new PayOsOptions { ClientId = "c", ApiKey = "a", ChecksumKey = checksumKey }));
        var verified = await gateway.VerifyWebhookAsync(webhook);

        Assert.Equal((123456789L, 810000L, "FT-LOCAL-1"), (verified.OrderCode, verified.Amount, verified.Reference));
    }

    [Fact]
    public async Task Webhook_AmountMismatch_RecordsMismatch_AndDoesNotActivate()
    {
        var (db, payOs, fulfillment, _) = await PaidCheckoutAsync();

        await fulfillment.HandleWebhookAsync(Webhook(payOs.Created.Single().OrderCode, 1_000), default);

        Assert.Equal("AMOUNT_MISMATCH", (await db.SaasPayments.SingleAsync()).Status);
        Assert.Equal("UNPAID", (await db.SaasInvoices.SingleAsync()).Status);
        Assert.False((await db.Subscriptions.SingleAsync()).IsPaid);
    }

    [Fact]
    public async Task Reconcile_PaidRecordsPayment_ExpiredCancelsAndReleasesCode()
    {
        var (db, payOs, fulfillment, _) = await PaidCheckoutAsync();
        var checkout = PublicService(db, payOs);
        await checkout.CheckoutAsync(Request("expired-org", "e@b.vn"));
        payOs.Payments[payOs.Created[0].OrderCode] = new(PaymentLinkStatus.Paid, 900_000, "FT999");
        payOs.Payments[payOs.Created[1].OrderCode] = new(PaymentLinkStatus.Expired, 0, null);

        await fulfillment.ReconcileAsync(default);

        Assert.Equal("FT999", (await db.SaasPayments.SingleAsync()).PaymentReference);
        var expired = await db.Subscriptions.Include(s => s.Invoices).SingleAsync(s => s.RequestedTenantCode == "expired-org");
        Assert.Equal(("CANCELLED", "EXPIRED"), (expired.Status, expired.Invoices.Single().Status));
        Assert.True((await checkout.CheckTenantCodeAsync("expired-org")).Available);
    }

    [Fact]
    public async Task Provision_Success_ActivatesTenant_AndEmailsPasswordThatIsNeverStored()
    {
        var db = SubscriptionTestDb.Create();
        var provisioning = new FakeProvisioning(db);
        var email = new RecordingEmail();
        var publicId = (await PublicService(db, new FakePayOs()).CheckoutAsync(Request("trial-org", "t@b.vn", planVersionId: 1))).PublicId;

        await Fulfillment(db, new FakePayOs(), provisioning, email).ProvisionPendingAsync(default);

        var command = provisioning.Commands.Single();
        Assert.Equal(("trial-org", "t@b.vn", "t@b.vn"), (command.TenantCode, command.InitialManager.EmployeeAccount, command.InitialManager.EmployeeEmail));
        Assert.Null(command.SecurityContext.SystemAdminId);
        var password = command.InitialManager.EmployeePassword;
        Assert.True(PasswordRules.IsValid(password));

        db.ChangeTracker.Clear();
        var subscription = await db.Subscriptions.SingleAsync(s => s.PublicId == publicId);
        var tenant = await db.Tenants.SingleAsync();
        Assert.Equal(("ACTIVE", tenant.TenantId, subscription.CustomerId), (subscription.Status, subscription.TenantId!.Value, tenant.OwnerCustomerId));
        Assert.Equal(15, (subscription.EndAt!.Value - subscription.StartAt!.Value).Days);

        var message = email.Sent.Single();
        Assert.Equal("t@b.vn", message.Recipient);
        Assert.Contains("trial-org", message.TextBody);
        Assert.Contains(password, message.TextBody);
        Assert.Contains("http://app.test/", message.HtmlBody);
        Assert.DoesNotContain(await db.Customers.Select(c => c.CustomerPasswordHash).ToListAsync(), h => h != null);
    }

    [Fact]
    public async Task Provision_Failure_RetriesWithBackoff_ThenProvisioningFailed_WithoutEmail()
    {
        var db = SubscriptionTestDb.Create();
        var clock = new FixedClock(Now);
        var provisioning = new FakeProvisioning(db) { Fail = true };
        var email = new RecordingEmail();
        var publicId = (await PublicService(db, new FakePayOs()).CheckoutAsync(Request("trial-org", "t@b.vn", planVersionId: 1))).PublicId;
        var fulfillment = Fulfillment(db, new FakePayOs(), provisioning, email, clock);

        await fulfillment.ProvisionPendingAsync(default);
        await fulfillment.ProvisionPendingAsync(default); // chưa hết thời gian chờ → không thử
        Assert.Single(provisioning.Commands);

        for (var minute = 1; minute <= 5; minute++)
        {
            clock.Now = Now.AddMinutes(minute * 3);
            await fulfillment.ProvisionPendingAsync(default);
        }

        Assert.Equal(SubscriptionFulfillmentService.MaxProvisioningAttempts, provisioning.Commands.Count);
        var status = await PublicService(db, new FakePayOs()).GetStatusAsync(publicId);
        Assert.Equal("PROVISIONING_FAILED", status.Status);
        Assert.Contains("liên hệ hỗ trợ", status.Message);
        Assert.Empty(email.Sent);
    }

    private static async Task<(CentralDbContext Db, FakePayOs PayOs, SubscriptionFulfillmentService Fulfillment, Guid PublicId)>
        PaidCheckoutAsync(string? voucher = null)
    {
        var db = SubscriptionTestDb.Create();
        var payOs = new FakePayOs();
        var result = await PublicService(db, payOs).CheckoutAsync(Request("new-org", "a@b.vn", voucher: voucher));
        return (db, payOs, Fulfillment(db, payOs, new FakeProvisioning(db), new RecordingEmail()), result.PublicId);
    }

    private static SubscriptionCheckoutRequest Request(string tenantCode, string email, long planVersionId = 2, string? voucher = null) => new()
    {
        PlanVersionId = planVersionId, TenantCode = tenantCode, TenantName = "Công ty", FullName = "Nguyễn Văn A",
        Email = email, Phone = "0912345678", VoucherCode = voucher
    };

    private static Webhook Webhook(long orderCode, long amount, string signature = "valid") => new()
    {
        Code = "00", Success = true, Signature = signature,
        Data = new WebhookData { OrderCode = orderCode, Amount = amount, Reference = "FT123", Code = "00" }
    };

    private static PublicSubscriptionService PublicService(CentralDbContext db, FakePayOs payOs) => new(
        db, new FixedClock(Now), payOs, Options.Create(new PayOsOptions()), NullLogger<PublicSubscriptionService>.Instance);

    private static SubscriptionFulfillmentService Fulfillment(CentralDbContext db, IPayOsGateway payOs,
        FakeProvisioning provisioning, RecordingEmail email, TimeProvider? clock = null) => new(
        db, payOs, provisioning, email,
        Options.Create(new AccountRecoveryOptions { TenantFrontendBaseUrl = "http://app.test" }),
        clock ?? new FixedClock(Now), NullLogger<SubscriptionFulfillmentService>.Instance);
}
