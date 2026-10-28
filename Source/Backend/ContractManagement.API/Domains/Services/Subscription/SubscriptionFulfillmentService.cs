using ContractManagement.API.Domains.Email;
using ContractManagement.API.Domains.Payments;
using ContractManagement.API.Domains.Recovery;
using ContractManagement.Infrastructure.MultiTenancy.Contracts;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Saas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using static ContractManagement.API.Domains.Services.Subscription.SubscriptionRules;

namespace ContractManagement.API.Domains.Services.Subscription;

/// <summary>
/// Sau checkout: ghi nhận thanh toán (webhook PayOS / đối soát) và kích hoạt tenant + gửi email tài khoản.
/// Hàng đợi kích hoạt chính là SUBSCRIPTION có is_paid = 1 và status = PENDING.
/// </summary>
public sealed class SubscriptionFulfillmentService
{
    public const int MaxProvisioningAttempts = 3;

    private readonly CentralDbContext _db;
    private readonly IPayOsGateway _payOs;
    private readonly ITenantProvisioningService _provisioning;
    private readonly IEmailTransport _email;
    private readonly AccountRecoveryOptions _frontend;
    private readonly TimeProvider _clock;
    private readonly ILogger<SubscriptionFulfillmentService> _logger;

    public SubscriptionFulfillmentService(
        CentralDbContext db,
        IPayOsGateway payOs,
        ITenantProvisioningService provisioning,
        IEmailTransport email,
        IOptions<AccountRecoveryOptions> frontend,
        TimeProvider clock,
        ILogger<SubscriptionFulfillmentService> logger)
    {
        _db = db;
        _payOs = payOs;
        _provisioning = provisioning;
        _email = email;
        _frontend = frontend.Value;
        _clock = clock;
        _logger = logger;
    }

    private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

    /// <summary>Webhook PayOS: chữ ký sai ném exception (controller trả 400); mọi trường hợp khác trả bình thường (200).</summary>
    public async Task HandleWebhookAsync(Webhook webhook, CancellationToken cancellationToken)
    {
        var data = await _payOs.VerifyWebhookAsync(webhook);
        if (webhook.Code != "00" || data.Code != "00")
        {
            _logger.LogInformation("Bỏ qua webhook PayOS không thành công cho orderCode {OrderCode}.", data.OrderCode);
            return;
        }
        await RecordPayOsPaymentAsync(data.OrderCode, data.Amount, data.Reference, cancellationToken);
    }

    /// <summary>
    /// Ghi nhận tiền vào cho hóa đơn có payos_order_code. Idempotent: hóa đơn đã PAID hoặc đã có payment cùng reference thì bỏ qua.
    /// Không tạo tenant ở đây; job kích hoạt sẽ nhận đơn PENDING + is_paid.
    /// </summary>
    public async Task RecordPayOsPaymentAsync(long orderCode, long amount, string? reference, CancellationToken cancellationToken)
    {
        // Webhook và job đối soát có thể chạy song song: va chạm (trùng payment_reference / usage_count) thì đọc lại và thử lại.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await RecordPayOsPaymentCoreAsync(orderCode, amount, reference, cancellationToken);
                return;
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                _db.ChangeTracker.Clear();
            }
        }
    }

    private async Task RecordPayOsPaymentCoreAsync(long orderCode, long amount, string? reference, CancellationToken cancellationToken)
    {
        var invoice = await _db.SaasInvoices
            .Include(i => i.Subscription)
            .Include(i => i.Payments)
            .Include(i => i.PromotionDetails).ThenInclude(d => d.Promotion)
            .SingleOrDefaultAsync(i => i.PayosOrderCode == orderCode, cancellationToken);
        if (invoice is null)
        {
            // Gồm cả payload test PayOS gửi khi xác nhận webhook URL.
            _logger.LogInformation("Webhook PayOS cho orderCode {OrderCode} không khớp hóa đơn nào.", orderCode);
            return;
        }

        var paymentReference = string.IsNullOrWhiteSpace(reference) ? $"PAYOS-{orderCode}" : reference.Trim();
        if (invoice.Status == Paid || invoice.Payments.Any(p => p.PaymentReference == paymentReference)
            || await _db.SaasPayments.AnyAsync(p => p.PaymentReference == paymentReference, cancellationToken))
            return;

        var now = UtcNow;
        var payment = new SaasPayment
        {
            Invoice = invoice, PaymentReference = paymentReference, PaymentMethod = "PAYOS", PaymentProvider = "PAYOS",
            ProviderTransactionId = reference, Amount = amount, Currency = invoice.Currency, CreatedAt = now
        };
        _db.SaasPayments.Add(payment);

        if (amount != invoice.TotalAmount)
        {
            payment.Status = "AMOUNT_MISMATCH";
            payment.FailureReason = $"Expected {invoice.TotalAmount:0}, received {amount}";
            _logger.LogWarning("Số tiền PayOS lệch cho hóa đơn {InvoiceCode}.", invoice.InvoiceCode);
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        payment.Status = Success;
        payment.PaidAt = now;
        invoice.Status = Paid;
        invoice.PaidAt = now;
        invoice.UpdatedAt = now;
        if (invoice.Subscription is { } subscription)
        {
            subscription.IsPaid = true;
            // Tiền đã vào thì vẫn kích hoạt, kể cả khi đơn đã bị hủy/hết hạn phía mình ngay trước đó.
            if (subscription.Status is Cancelled or PaymentFailed) subscription.Status = Pending;
            subscription.UpdatedAt = now;
        }
        foreach (var detail in invoice.PromotionDetails)
            detail.Promotion.UsageCount++; // concurrency token: va chạm sẽ được thử lại ở RecordPayOsPaymentAsync

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Đối soát: hỏi PayOS trạng thái các hóa đơn UNPAID trong 24h (dùng được khi chạy localhost không nhận được webhook).
    /// </summary>
    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        var since = UtcNow.AddHours(-24);
        var invoices = await _db.SaasInvoices.AsNoTracking()
            .Where(i => i.Status == Unpaid && i.PayosOrderCode != null && i.CreatedAt >= since)
            .OrderBy(i => i.InvoiceId)
            .Select(i => new { i.InvoiceId, OrderCode = i.PayosOrderCode!.Value, i.InvoiceCode })
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var item in invoices)
        {
            try
            {
                var info = await _payOs.GetPaymentAsync(item.OrderCode, cancellationToken);
                if (info.Status == PaymentLinkStatus.Paid)
                    await RecordPayOsPaymentAsync(item.OrderCode, info.AmountPaid, info.Reference, cancellationToken);
                else if (info.Status is PaymentLinkStatus.Cancelled or PaymentLinkStatus.Expired)
                    await CloseUnpaidAsync(item.InvoiceId, info.Status == PaymentLinkStatus.Expired ? Expired : Cancelled, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning("Đối soát PayOS hóa đơn {InvoiceCode} lỗi: {ErrorType}", item.InvoiceCode, exception.GetType().Name);
            }
            finally
            {
                _db.ChangeTracker.Clear();
            }
        }
    }

    private async Task CloseUnpaidAsync(long invoiceId, string invoiceStatus, CancellationToken cancellationToken)
    {
        var invoice = await _db.SaasInvoices.Include(i => i.Subscription).SingleAsync(i => i.InvoiceId == invoiceId, cancellationToken);
        if (invoice.Status != Unpaid) return;
        var now = UtcNow;
        invoice.Status = invoiceStatus;
        invoice.UpdatedAt = now;
        if (invoice.Subscription is { Status: Pending, IsPaid: false } subscription)
        {
            subscription.Status = Cancelled;
            subscription.UpdatedAt = now;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Kích hoạt các đơn đã thanh toán; lỗi thì thử lại (giãn cách theo số lần), quá giới hạn thì PROVISIONING_FAILED.</summary>
    public async Task ProvisionPendingAsync(CancellationToken cancellationToken)
    {
        var now = UtcNow;
        var ids = await _db.Subscriptions.AsNoTracking()
            .Where(s => s.Status == Pending && s.IsPaid)
            .OrderBy(s => s.SubscriptionId)
            .Select(s => new { s.SubscriptionId, s.ProvisioningAttemptCount, s.UpdatedAt })
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var item in ids)
        {
            if (item.ProvisioningAttemptCount > 0 && item.UpdatedAt > now.AddMinutes(-item.ProvisioningAttemptCount))
                continue; // chờ 1, 2... phút giữa các lần thử lại
            await ProvisionAsync(item.SubscriptionId, cancellationToken);
            _db.ChangeTracker.Clear();
        }
    }

    private async Task ProvisionAsync(long subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await _db.Subscriptions
            .Include(s => s.Customer)
            .Include(s => s.PlanVersion)
            .SingleAsync(s => s.SubscriptionId == subscriptionId, cancellationToken);
        var customer = subscription.Customer!;
        var password = TenantPasswordGenerator.Generate();

        TenantProvisioningResult result;
        try
        {
            result = await _provisioning.CreateDedicatedAsync(new TenantProvisioningCommand(
                subscription.RequestedTenantCode,
                subscription.RequestedTenantName,
                new InitialManagerProvisioningCommand(
                    null,
                    customer.CustomerEmail,
                    password,
                    customer.CustomerDisplayName ?? customer.CustomerEmail,
                    customer.CustomerHandPhone,
                    customer.CustomerEmail),
                new SecurityOperationContext(null, null, null, $"subscription-{subscription.PublicId}")),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Tạo tenant cho subscription {SubscriptionId} thất bại (lần {Attempt}).",
                subscriptionId, subscription.ProvisioningAttemptCount + 1);
            _db.ChangeTracker.Clear();
            subscription = await _db.Subscriptions.SingleAsync(s => s.SubscriptionId == subscriptionId, cancellationToken);
            subscription.ProvisioningAttemptCount++;
            subscription.UpdatedAt = UtcNow;
            if (subscription.ProvisioningAttemptCount >= MaxProvisioningAttempts) subscription.Status = ProvisioningFailed;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        _db.ChangeTracker.Clear();
        subscription = await _db.Subscriptions.Include(s => s.Customer).Include(s => s.PlanVersion)
            .SingleAsync(s => s.SubscriptionId == subscriptionId, cancellationToken);
        var tenant = await _db.Tenants.SingleAsync(t => t.TenantId == result.TenantId, cancellationToken);
        var now = UtcNow;
        tenant.OwnerCustomerId = subscription.CustomerId;
        subscription.TenantId = result.TenantId;
        subscription.Status = Active;
        subscription.StartAt = now;
        subscription.EndAt = now.AddDays(DurationDays(subscription.PlanVersion.DurationDays, subscription.BillingCycle));
        subscription.ProvisioningAttemptCount++;
        subscription.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        await SendAccountEmailAsync(subscription, result.TenantCode, password, cancellationToken);
    }

    private async Task SendAccountEmailAsync(SaasSubscription subscription, string tenantCode, string password, CancellationToken cancellationToken)
    {
        var customer = subscription.Customer!;
        var loginUrl = $"{(_frontend.TenantFrontendBaseUrl ?? "http://localhost:3000").TrimEnd('/')}/";
        var message = SubscriptionWelcomeEmailTemplate.Build(customer.CustomerEmail,
            customer.CustomerDisplayName ?? customer.CustomerEmail, subscription.RequestedTenantName, tenantCode,
            loginUrl, subscription.EndAt, password);

        // shortcut: mật khẩu không được lưu nên nếu gửi lỗi cả 3 lần thì không gửi lại được; cần chức năng "cấp lại mật khẩu" khi có hỗ trợ.
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await _email.SendAsync(message, cancellationToken);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError("Gửi email tài khoản cho subscription {SubscriptionId} lỗi (lần {Attempt}): {ErrorType}",
                    subscription.SubscriptionId, attempt, exception.GetType().Name);
                if (attempt < 3) await Task.Delay(TimeSpan.FromSeconds(5 * attempt), _clock, cancellationToken);
            }
        }
    }
}
