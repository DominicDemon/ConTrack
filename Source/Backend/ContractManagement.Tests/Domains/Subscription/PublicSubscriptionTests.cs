using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.DTOs.Requests.Subscription;
using ContractManagement.API.Domains.Payments;
using ContractManagement.API.Domains.Services.Subscription;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using ContractManagement.Infrastructure.Persistence.Central.Saas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PayOS.Models.V2.PaymentRequests;

namespace ContractManagement.Tests.Domains.Subscription;

public sealed class PublicSubscriptionTests
{
    private static readonly DateTime Now = new(2026, 10, 27, 8, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("PERCENTAGE", 10, null, 900_000, 90_000)]
    [InlineData("PERCENTAGE", 10, 50_000d, 900_000, 50_000)]       // chặn bởi max_discount_amount
    [InlineData("PERCENTAGE", 100, null, 900_000, 900_000)]       // giảm 100% → tổng 0
    [InlineData("PERCENTAGE", 33, null, 1_000, 330)]
    [InlineData("PERCENTAGE", 33.33, null, 1_000, 333)]           // làm tròn xuống tới đồng
    [InlineData("FIXED_AMOUNT", 200_000, null, 900_000, 200_000)]
    [InlineData("FIXED_AMOUNT", 2_000_000, null, 900_000, 900_000)] // không vượt subtotal → tổng không âm
    public void CalculateDiscount(string type, double value, double? max, double subtotal, double expected)
    {
        var promotion = Promotion(type, (decimal)value, (decimal?)max);
        Assert.Equal((decimal)expected, SubscriptionRules.CalculateDiscount((decimal)subtotal, promotion));
    }

    [Fact]
    public void ValidatePromotion_CoversStatusWindowUsageAndFreeTotal()
    {
        Assert.Null(SubscriptionRules.ValidatePromotion(Promotion(), 900_000, Now));
        Assert.Equal(SubscriptionRules.VoucherInactive,
            SubscriptionRules.ValidatePromotion(Promotion(status: "INACTIVE"), 900_000, Now));
        Assert.Equal(SubscriptionRules.VoucherNotStarted,
            SubscriptionRules.ValidatePromotion(Promotion(startAt: Now.AddDays(1)), 900_000, Now));
        Assert.Equal(SubscriptionRules.VoucherExpired,
            SubscriptionRules.ValidatePromotion(Promotion(endAt: Now), 900_000, Now));
        Assert.Equal(SubscriptionRules.VoucherUsageLimitReached,
            SubscriptionRules.ValidatePromotion(Promotion(limit: 5, used: 5), 900_000, Now));
        Assert.Equal(SubscriptionRules.VoucherNotApplicable,
            SubscriptionRules.ValidatePromotion(Promotion(), 0, Now));
    }

    [Theory]
    [InlineData("abc", true)]
    [InlineData("  ABC-Company ", true)]
    [InlineData("a1-b2", true)]
    [InlineData("ab", false)]
    [InlineData("-abc", false)]
    [InlineData("abc-", false)]
    [InlineData("ab_c", false)]
    [InlineData("công-ty", false)]
    [InlineData("a234567890123456789012345678901", false)] // 31 ký tự
    public void TenantCodeRule(string input, bool valid)
    {
        Assert.Equal(valid, SubscriptionRules.IsValidTenantCode(SubscriptionRules.NormalizeTenantCode(input)));
    }

    [Fact]
    public async Task GetPlans_ReturnsSeededPlans_TrialFirst_OnlyEffectiveVersions()
    {
        await using var db = SubscriptionTestDb.Create();
        db.PlanVersions.Add(new SaasPlanVersion
        {
            PlanVersionId = 99, PlanId = 2, VersionNumber = 9, Price = 1m, DurationDays = 1,
            EffectiveFrom = Now.AddDays(1), Status = "ACTIVE", CreatedAt = Now
        });
        await db.SaveChangesAsync();

        var plans = await Service(db).GetPlansAsync();

        Assert.Equal(["TRIAL", "STANDARD"], plans.Select(p => p.PlanCode));
        Assert.Equal(0m, plans[0].Versions.Single().Price);
        Assert.Equal([90, 180, 365], plans[1].Versions.Select(v => v.DurationDays!.Value));
    }

    [Fact]
    public async Task CheckTenantCode_RejectsExistingTenantAndLivePendingReservation()
    {
        await using var db = SubscriptionTestDb.Create();
        db.Tenants.Add(new Tenant
        {
            TenantCode = "Acme", TenantName = "Acme", Status = TenantStatus.Active, CreatedAt = Now,
            TenantDatabase = new TenantDatabase
            {
                DatabaseKey = "k", DatabaseName = "d", ConnectionString = "c",
                Mode = TenantDatabaseMode.Dedicated, CreatedAt = Now
            }
        });
        db.Customers.Add(new SaasCustomer { CustomerId = 1, CustomerEmail = "a@b.vn", CreatedAt = Now });
        AddPendingSubscription(db, 1, "held", dueAt: Now.AddMinutes(10));
        AddPendingSubscription(db, 2, "released", dueAt: Now.AddMinutes(-1));
        await db.SaveChangesAsync();
        var service = Service(db);

        Assert.Equal(SubscriptionRules.TenantCodeTaken, (await service.CheckTenantCodeAsync("ACME")).ErrorCode);
        Assert.Equal(SubscriptionRules.TenantCodeTaken, (await service.CheckTenantCodeAsync("held")).ErrorCode);
        Assert.True((await service.CheckTenantCodeAsync("released")).Available);
        Assert.Equal(SubscriptionRules.TenantCodeInvalid, (await service.CheckTenantCodeAsync("x")).ErrorCode);
        Assert.True((await service.CheckTenantCodeAsync(" New-Org ")).Available);
    }

    [Fact]
    public async Task PreviewVoucher_UsesServerPriceAndDoesNotConsumeVoucher()
    {
        await using var db = SubscriptionTestDb.Create();
        var service = Service(db);

        var preview = await service.PreviewVoucherAsync(new VoucherPreviewRequest { PlanVersionId = 2, VoucherCode = " test10 " });

        Assert.Equal((900_000m, 90_000m, 810_000m), (preview.Subtotal, preview.Discount, preview.Total));
        Assert.Equal(0, (await db.Promotions.SingleAsync()).UsageCount);

        var noVoucher = await service.PreviewVoucherAsync(new VoucherPreviewRequest { PlanVersionId = 2 });
        Assert.Equal(900_000m, noVoucher.Total);

        var trial = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.PreviewVoucherAsync(new VoucherPreviewRequest { PlanVersionId = 1, VoucherCode = "TEST10" }));
        Assert.Equal(SubscriptionRules.VoucherNotApplicable, trial.Code);

        var missing = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.PreviewVoucherAsync(new VoucherPreviewRequest { PlanVersionId = 2, VoucherCode = "NOPE" }));
        Assert.Equal(SubscriptionRules.VoucherNotFound, missing.Code);

        var plan = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.PreviewVoucherAsync(new VoucherPreviewRequest { PlanVersionId = 404 }));
        Assert.Equal(SubscriptionRules.PlanNotFound, plan.Code);
    }

    private static void AddPendingSubscription(CentralDbContext db, long id, string code, DateTime dueAt)
    {
        db.Subscriptions.Add(new SaasSubscription
        {
            SubscriptionId = id, CustomerId = 1, PlanVersionId = 2, Status = "PENDING",
            RequestedTenantCode = code, RequestedTenantName = code, PublicId = Guid.NewGuid(), CreatedAt = Now,
            Invoices =
            {
                new SaasInvoice
                {
                    InvoiceCode = $"INV-{id}", CustomerId = 1, BillingName = code, Subtotal = 1, TotalAmount = 1,
                    Status = "UNPAID", DueAt = dueAt, CreatedAt = Now
                }
            }
        });
    }

    private static SaasPromotion Promotion(string type = "PERCENTAGE", decimal value = 10, decimal? max = null,
        string status = "ACTIVE", DateTime? startAt = null, DateTime? endAt = null, int? limit = null, int used = 0) => new()
    {
        PromotionCode = "T", PromotionName = "T", DiscountType = type, DiscountValue = value, MaxDiscountAmount = max,
        Status = status, StartAt = startAt ?? Now.AddDays(-1), EndAt = endAt, UsageLimit = limit, UsageCount = used
    };

    [Fact]
    public async Task Checkout_PaidPlan_CreatesPendingOrderWithServerPrice_AndReturnsPayOsLink()
    {
        await using var db = SubscriptionTestDb.Create();
        var payOs = new FakePayOs();

        var result = await Service(db, payOs).CheckoutAsync(Checkout(voucher: "TEST10"));

        Assert.Equal("https://pay.test/1", result.CheckoutUrl);
        Assert.Equal(810_000m, result.Total);
        var request = Assert.Single(payOs.Created);
        Assert.Equal(810_000, request.Amount);
        Assert.True(request.Description.Length <= 9);
        Assert.Equal($"http://fe/result?subscription={result.PublicId}", request.ReturnUrl);

        var subscription = await db.Subscriptions.Include(s => s.Invoices).Include(s => s.Customer).SingleAsync();
        Assert.Equal(("PENDING", false, "new-org", "a@b.vn", "0912345678"),
            (subscription.Status, subscription.IsPaid, subscription.RequestedTenantCode,
                subscription.Customer!.CustomerEmail, subscription.Customer.CustomerHandPhone));
        var invoice = subscription.Invoices.Single();
        Assert.Equal(("UNPAID", request.OrderCode, "https://pay.test/1"), (invoice.Status, invoice.PayosOrderCode!.Value, invoice.PayosCheckoutUrl));
        Assert.Equal(1, await db.InvoicePromotionDetails.CountAsync());
        Assert.Equal(1, await db.SubscriptionPromotionDetails.CountAsync());
        Assert.Equal(0, (await db.Promotions.SingleAsync()).UsageCount); // chỉ trừ lượt khi thanh toán thành công
        Assert.Empty(db.SaasPayments);
    }

    [Fact]
    public async Task Checkout_Resubmit_ReturnsSameOrder_ButChangedPlanCancelsOldLink()
    {
        await using var db = SubscriptionTestDb.Create();
        var payOs = new FakePayOs();
        var service = Service(db, payOs);

        var first = await service.CheckoutAsync(Checkout());
        var again = await service.CheckoutAsync(Checkout());
        Assert.Equal(first.PublicId, again.PublicId);
        Assert.Single(payOs.Created);

        var changed = await service.CheckoutAsync(Checkout(planVersionId: 3));
        Assert.NotEqual(first.PublicId, changed.PublicId);
        Assert.Single(payOs.Cancelled);
        Assert.Equal("CANCELLED", (await db.Subscriptions.SingleAsync(s => s.PublicId == first.PublicId)).Status);
    }

    [Fact]
    public async Task Checkout_Trial_SkipsPayOs_RecordsFreePayment_AndOnlyOncePerEmail()
    {
        await using var db = SubscriptionTestDb.Create();
        var payOs = new FakePayOs();
        var service = Service(db, payOs);

        var result = await service.CheckoutAsync(Checkout(planVersionId: 1));

        Assert.Null(result.CheckoutUrl);
        Assert.Empty(payOs.Created);
        var subscription = await db.Subscriptions.SingleAsync();
        Assert.True(subscription.IsPaid);
        Assert.Equal("PENDING", subscription.Status);
        var payment = await db.SaasPayments.SingleAsync();
        Assert.Equal(("FREE", "SUCCESS", 0m), (payment.PaymentMethod, payment.Status, payment.Amount));
        Assert.Equal("PAID", (await db.SaasInvoices.SingleAsync()).Status);

        var status = await service.GetStatusAsync(result.PublicId);
        Assert.Equal("Đã thanh toán, hệ thống đang khởi tạo tài khoản.", status.Message);

        var again = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CheckoutAsync(Checkout(planVersionId: 1, tenantCode: "other-org")));
        Assert.Equal(SubscriptionRules.EmailTaken, again.Code);
    }

    [Fact]
    public async Task DuplicateEmail_BlockedWhenLinkedToOrganization_AllowedAfterCancel()
    {
        await using var db = SubscriptionTestDb.Create();
        var service = Service(db, new FakePayOs());
        var pending = await service.CheckoutAsync(Checkout());

        // Đơn chưa thanh toán còn hạn: chặn mã khác, cùng mã vẫn quay về đơn cũ.
        var otherCode = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CheckoutAsync(Checkout(tenantCode: "other-org")));
        Assert.Equal((409, SubscriptionRules.EmailTaken), (otherCode.StatusCode, otherCode.Code));
        Assert.Equal(pending.PublicId, (await service.CheckoutAsync(Checkout())).PublicId);
        Assert.True((await service.CheckEmailAsync(" A@B.vn ", "new-org")).Available);
        Assert.Equal(SubscriptionRules.EmailTaken, (await service.CheckEmailAsync("a@b.vn", "other-org")).ErrorCode);
        Assert.Equal(SubscriptionRules.EmailInvalid, (await service.CheckEmailAsync("not-an-email", null)).ErrorCode);

        // Hủy thì email dùng lại được.
        await service.CancelAsync(pending.PublicId);
        Assert.True((await service.CheckEmailAsync("a@b.vn", "other-org")).Available);
        var paid = await service.CheckoutAsync(Checkout(tenantCode: "other-org"));

        // Đã thanh toán (gắn với tổ chức): chặn mọi mã, kể cả cùng mã.
        var subscription = await db.Subscriptions.SingleAsync(s => s.PublicId == paid.PublicId);
        subscription.IsPaid = true;
        subscription.Status = "ACTIVE";
        await db.SaveChangesAsync();
        Assert.Equal(SubscriptionRules.EmailTaken, (await service.CheckEmailAsync("a@b.vn", "other-org")).ErrorCode);
        var again = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CheckoutAsync(Checkout(tenantCode: "third-org")));
        Assert.Equal(SubscriptionRules.EmailTaken, again.Code);
    }

    [Fact]
    public async Task Checkout_PayOsFailure_MarksPaymentFailed_AndReleasesTenantCode()
    {
        await using var db = SubscriptionTestDb.Create();
        var service = Service(db, new FakePayOs { FailCreate = true });

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CheckoutAsync(Checkout()));

        Assert.Equal((502, SubscriptionRules.PaymentGatewayError), (error.StatusCode, error.Code));
        Assert.Equal("PAYMENT_FAILED", (await db.Subscriptions.SingleAsync()).Status);
        Assert.True((await service.CheckTenantCodeAsync("new-org")).Available);
    }

    [Theory]
    [InlineData("", "a@b.vn", "0912345678", "New-Org", SubscriptionRules.FullNameInvalid)]
    [InlineData("A", "not-an-email", "0912345678", "New-Org", SubscriptionRules.EmailInvalid)]
    [InlineData("A", "a2345678901234567890123456789012345678901234567@b.vn", "0912345678", "New-Org", SubscriptionRules.EmailInvalid)]
    [InlineData("A", "a@b.vn", "912345678", "New-Org", SubscriptionRules.PhoneInvalid)]
    [InlineData("A", "a@b.vn", "0912 345 678", "x", SubscriptionRules.TenantCodeInvalid)]
    public async Task Checkout_ValidatesInput(string fullName, string email, string phone, string tenantCode, string expected)
    {
        await using var db = SubscriptionTestDb.Create();
        var request = Checkout(tenantCode: tenantCode);
        (request.FullName, request.Email, request.Phone) = (fullName, email, phone);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).CheckoutAsync(request));

        Assert.Equal(expected, error.Code);
    }

    [Fact]
    public async Task Cancel_CancelsOpenPayOsLink_ButKeepsOrderPaidOnPayOs()
    {
        await using var db = SubscriptionTestDb.Create();
        var payOs = new FakePayOs();
        var service = Service(db, payOs);
        var open = await service.CheckoutAsync(Checkout());
        var paid = await service.CheckoutAsync(Checkout(tenantCode: "paid-org", email: "p@b.vn"));
        payOs.Payments[payOs.Created[1].OrderCode] = new(PaymentLinkStatus.Paid, 900_000, "REF");

        Assert.Equal("CANCELLED", (await service.CancelAsync(open.PublicId)).Status);
        Assert.Equal("PENDING", (await service.CancelAsync(paid.PublicId)).Status);
        Assert.Single(payOs.Cancelled);
        Assert.Null((await service.GetStatusAsync(open.PublicId)).CheckoutUrl);

        var missing = await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetStatusAsync(Guid.NewGuid()));
        Assert.Equal(SubscriptionRules.SubscriptionNotFound, missing.Code);
    }

    private static SubscriptionCheckoutRequest Checkout(long planVersionId = 2, string tenantCode = "New-Org",
        string email = " A@B.vn ", string? voucher = null) => new()
    {
        PlanVersionId = planVersionId, TenantCode = tenantCode, TenantName = "Công ty Mới", FullName = "Nguyễn Văn A",
        Email = email, Phone = "0912.345.678", VoucherCode = voucher
    };

    private static PublicSubscriptionService Service(CentralDbContext db, FakePayOs? payOs = null) => new(
        db, new FixedClock(Now), payOs ?? new FakePayOs(),
        Options.Create(new PayOsOptions { ResultPageUrl = "http://fe/result" }),
        NullLogger<PublicSubscriptionService>.Instance);

}
