using System.Security.Cryptography;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.DTOs.Requests.Subscription;
using ContractManagement.API.Domains.DTOs.Responses.Subscription;
using ContractManagement.API.Domains.Email;
using ContractManagement.API.Domains.Interfaces.Subscription;
using ContractManagement.API.Domains.Payments;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Saas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PayOS.Models.V2.PaymentRequests;
using static ContractManagement.API.Domains.Services.Subscription.SubscriptionRules;

namespace ContractManagement.API.Domains.Services.Subscription;

/// <summary>
/// API công khai cho luồng đăng ký gói: danh sách gói, kiểm tra mã tổ chức, voucher,
/// đặt đơn (checkout) với PayOS, xem trạng thái và hủy.
/// </summary>
public sealed class PublicSubscriptionService : IPublicSubscriptionService
{
    private const string InvoiceSuffixAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly CentralDbContext _db;
    private readonly TimeProvider _clock;
    private readonly IPayOsGateway _payOs;
    private readonly PayOsOptions _payOsOptions;
    private readonly ILogger<PublicSubscriptionService> _logger;

    public PublicSubscriptionService(
        CentralDbContext db,
        TimeProvider clock,
        IPayOsGateway payOs,
        IOptions<PayOsOptions> payOsOptions,
        ILogger<PublicSubscriptionService> logger)
    {
        _db = db;
        _clock = clock;
        _payOs = payOs;
        _payOsOptions = payOsOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PublicPlanResponse>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        var now = UtcNow;
        var plans = await _db.Plans.AsNoTracking()
            .Where(p => p.PlanStatus == Active && p.SaasType.SaasTypeStatus == Active)
            .Select(p => new
            {
                Plan = p,
                Versions = p.Versions.Where(v => v.Status == Active
                        && v.EffectiveFrom <= now && (v.EffectiveTo == null || v.EffectiveTo > now))
                    .OrderBy(v => v.DurationDays).ThenBy(v => v.Price).ToList()
            })
            .ToListAsync(cancellationToken);

        return plans
            .Where(x => x.Versions.Count > 0)
            .OrderBy(x => x.Plan.IsPaid).ThenBy(x => x.Plan.PlanId)
            .Select(x => new PublicPlanResponse(
                x.Plan.PlanId, x.Plan.PlanCode, x.Plan.PlanName, x.Plan.PlanDescription, x.Plan.IsPaid,
                x.Plan.AllowsSupportTickets, x.Plan.MaxUsers, x.Plan.MaxContracts, x.Plan.MaxScanPages,
                x.Versions.Select(v => new PublicPlanVersionResponse(
                    v.PlanVersionId, v.VersionNumber, v.Price, v.Currency, v.BillingCycle, v.DurationDays)).ToList()))
            .ToList();
    }

    public async Task<TenantCodeCheckResponse> CheckTenantCodeAsync(string? code, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeTenantCode(code);
        var error = await GetTenantCodeErrorAsync(normalized, cancellationToken);
        return new TenantCodeCheckResponse(normalized, error is null, error, error is null ? null : Message(error));
    }

    public async Task<EmailCheckResponse> CheckEmailAsync(string? email, string? tenantCode, CancellationToken cancellationToken = default)
    {
        var normalized = email?.Trim().ToLowerInvariant() ?? string.Empty;
        var error = normalized.Length > MaxEmailLength || !SmtpEmailOptions.IsEmailAddress(normalized)
            ? EmailInvalid
            : await GetEmailErrorAsync(normalized, NormalizeTenantCode(tenantCode), cancellationToken);
        return new EmailCheckResponse(normalized, error is null, error, error is null ? null : Message(error));
    }

    public async Task<VoucherPreviewResponse> PreviewVoucherAsync(VoucherPreviewRequest request, CancellationToken cancellationToken = default)
    {
        var version = await GetActivePlanVersionAsync(request.PlanVersionId, cancellationToken);
        var (promotion, discount) = await PriceAsync(version.Price, request.VoucherCode, cancellationToken);
        return new VoucherPreviewResponse(version.PlanVersionId, promotion?.PromotionCode,
            version.Price, discount, version.Price - discount, version.Currency);
    }

    public async Task<SubscriptionCheckoutResponse> CheckoutAsync(SubscriptionCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        var tenantCode = NormalizeTenantCode(request.TenantCode);
        var tenantName = request.TenantName?.Trim() ?? string.Empty;
        var fullName = request.FullName?.Trim() ?? string.Empty;
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var phone = NormalizePhone(request.Phone);
        if (fullName.Length is 0 or > MaxFullNameLength) throw Fail(StatusCodes.Status400BadRequest, FullNameInvalid);
        if (tenantName.Length is 0 or > MaxTenantNameLength) throw Fail(StatusCodes.Status400BadRequest, TenantNameInvalid);
        if (email.Length > MaxEmailLength || !SmtpEmailOptions.IsEmailAddress(email)) throw Fail(StatusCodes.Status400BadRequest, EmailInvalid);
        if (phone is null) throw Fail(StatusCodes.Status400BadRequest, PhoneInvalid);
        if (!IsValidTenantCode(tenantCode)) throw Fail(StatusCodes.Status400BadRequest, TenantCodeInvalid);

        var version = await GetActivePlanVersionAsync(request.PlanVersionId, cancellationToken);
        var (promotion, discount) = await PriceAsync(version.Price, request.VoucherCode, cancellationToken);
        var total = version.Price - discount;
        var now = UtcNow;

        // Gửi lặp (cùng email + mã tổ chức): cùng gói & số tiền thì trả lại đơn cũ, khác thì hủy đơn cũ để đặt lại.
        var existing = await _db.Subscriptions.Include(s => s.Invoices)
            .Where(s => s.Customer!.CustomerEmail == email && s.RequestedTenantCode == tenantCode && s.Status == Pending
                && (s.IsPaid || s.Invoices.Any(i => i.Status == Unpaid && i.DueAt > now)))
            .OrderByDescending(s => s.SubscriptionId)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            var existingInvoice = LatestInvoice(existing);
            if (existing.IsPaid || (existing.PlanVersionId == version.PlanVersionId && existingInvoice.TotalAmount == total))
                return new SubscriptionCheckoutResponse(existing.PublicId, existingInvoice.InvoiceCode,
                    existingInvoice.TotalAmount, existingInvoice.Currency, existing.IsPaid ? null : existingInvoice.PayosCheckoutUrl);
            await CancelUnpaidAsync(existing, existingInvoice, "Khách đặt lại đơn", cancellationToken);
        }

        var emailError = await GetEmailErrorAsync(email, tenantCode, cancellationToken);
        if (emailError is not null) throw Fail(StatusCodes.Status409Conflict, emailError);
        var tenantCodeError = await GetTenantCodeErrorAsync(tenantCode, cancellationToken);
        if (tenantCodeError is not null) throw Fail(StatusCodes.Status409Conflict, tenantCodeError);

        var customer = await _db.Customers.SingleOrDefaultAsync(c => c.CustomerEmail == email, cancellationToken);
        if (customer is null)
        {
            customer = new SaasCustomer { CustomerEmail = email, CreatedAt = now };
            _db.Customers.Add(customer);
        }
        else
        {
            customer.UpdatedAt = now;
        }
        customer.CustomerDisplayName = fullName;
        customer.CustomerHandPhone = phone;

        var invoiceSuffix = RandomNumberGenerator.GetString(InvoiceSuffixAlphabet, 6);
        var subscription = new SaasSubscription
        {
            Customer = customer, PlanVersionId = version.PlanVersionId, Status = Pending,
            PriceSnapshot = version.Price, Currency = version.Currency, BillingCycle = version.BillingCycle,
            RequestedTenantCode = tenantCode, RequestedTenantName = tenantName, PublicId = Guid.NewGuid(), CreatedAt = now
        };
        var invoice = new SaasInvoice
        {
            InvoiceCode = $"INV-{now:yyyyMM}-{invoiceSuffix}", Customer = customer, Subscription = subscription,
            BillingName = tenantName, BillingEmail = email, BillingPhone = phone,
            Subtotal = version.Price, DiscountAmount = discount, TotalAmount = total, Currency = version.Currency,
            Status = Unpaid, IssuedAt = now, DueAt = now.AddMinutes(_payOsOptions.PaymentLinkExpiryMinutes),
            PayosOrderCode = total > 0 ? NewOrderCode() : null, CreatedAt = now
        };
        _db.SaasInvoices.Add(invoice);

        if (promotion is not null)
        {
            _db.InvoicePromotionDetails.Add(new SaasInvoicePromotionDetail
            {
                Invoice = invoice, PromotionId = promotion.PromotionId, DiscountTypeApplied = promotion.DiscountType,
                DiscountValueApplied = promotion.DiscountValue, ActualMoneyDeducted = discount, CreatedAt = now
            });
            _db.SubscriptionPromotionDetails.Add(new SaasSubscriptionPromotionDetail
            {
                Subscription = subscription, PromotionId = promotion.PromotionId, DiscountTypeApplied = promotion.DiscountType,
                DiscountValueApplied = promotion.DiscountValue, ActualMoneyDeducted = discount, CreatedAt = now
            });
        }

        if (total == 0)
        {
            // Gói 0đ (dùng thử / voucher 100%): ghi nhận thanh toán ngay, job kích hoạt sẽ tạo tenant.
            _db.SaasPayments.Add(new SaasPayment
            {
                Invoice = invoice, PaymentReference = $"FREE-{invoice.InvoiceCode}", PaymentMethod = "FREE",
                Amount = 0, Currency = invoice.Currency, Status = Success, PaidAt = now, CreatedAt = now
            });
            invoice.Status = Paid;
            invoice.PaidAt = now;
            subscription.IsPaid = true;
            if (promotion is not null) promotion.UsageCount++; // UsageCount là concurrency token
        }

        // Một SaveChanges = một transaction: customer, subscription, invoice, voucher detail (và payment 0đ).
        // shortcut: hai checkout đồng thời cùng mã tổ chức đều có thể lọt qua; đơn thứ hai sẽ PROVISIONING_FAILED. Thêm khóa (sp_getapplock) nếu gặp thực tế.
        await _db.SaveChangesAsync(cancellationToken);

        if (total == 0)
            return new SubscriptionCheckoutResponse(subscription.PublicId, invoice.InvoiceCode, 0, invoice.Currency, null);

        try
        {
            var resultUrl = $"{_payOsOptions.ResultPageUrl}?subscription={subscription.PublicId}"; // PayOS tự thêm id, code, cancel, status, orderCode
            var link = await _payOs.CreatePaymentLinkAsync(new CreatePaymentLinkRequest
            {
                OrderCode = invoice.PayosOrderCode!.Value,
                Amount = (long)total,
                // Ngắn (≤ 9 ký tự) để hợp lệ cả với tài khoản ngân hàng không liên kết PayOS.
                Description = $"CT{invoiceSuffix}",
                ReturnUrl = resultUrl,
                CancelUrl = resultUrl,
                BuyerName = fullName,
                BuyerEmail = email,
                BuyerPhone = phone,
                ExpiredAt = new DateTimeOffset(invoice.DueAt!.Value, TimeSpan.Zero).ToUnixTimeSeconds()
            }, cancellationToken);
            invoice.PayosPaymentLinkId = link.PaymentLinkId;
            invoice.PayosCheckoutUrl = link.CheckoutUrl;
            invoice.UpdatedAt = UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return new SubscriptionCheckoutResponse(subscription.PublicId, invoice.InvoiceCode, total, invoice.Currency, link.CheckoutUrl);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Tạo link PayOS thất bại cho hóa đơn {InvoiceCode}.", invoice.InvoiceCode);
            invoice.Status = Cancelled;
            invoice.UpdatedAt = subscription.UpdatedAt = UtcNow;
            subscription.Status = PaymentFailed;
            await _db.SaveChangesAsync(CancellationToken.None);
            throw Fail(StatusCodes.Status502BadGateway, PaymentGatewayError);
        }
    }

    public async Task<SubscriptionStatusResponse> GetStatusAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        ToStatus(await FindAsync(publicId, cancellationToken));

    public async Task<SubscriptionStatusResponse> CancelAsync(Guid publicId, CancellationToken cancellationToken = default)
    {
        var subscription = await FindAsync(publicId, cancellationToken);
        if (subscription.Status == Pending && !subscription.IsPaid)
            await CancelUnpaidAsync(subscription, LatestInvoice(subscription), "Khách hủy thanh toán", cancellationToken);
        return ToStatus(subscription);
    }

    /// <summary>
    /// Hủy đơn chưa thanh toán. Hỏi PayOS trước: nếu link đã/đang thanh toán thì giữ nguyên để webhook/đối soát xử lý;
    /// nếu còn mở thì hủy link bên PayOS trước, tránh khách trả tiền cho đơn đã hủy.
    /// </summary>
    private async Task CancelUnpaidAsync(SaasSubscription subscription, SaasInvoice invoice, string reason, CancellationToken cancellationToken)
    {
        if (invoice.PayosOrderCode is { } orderCode)
        {
            try
            {
                var status = (await _payOs.GetPaymentAsync(orderCode, cancellationToken)).Status;
                if (status is PaymentLinkStatus.Paid or PaymentLinkStatus.Processing or PaymentLinkStatus.Underpaid) return;
                if (status == PaymentLinkStatus.Pending) await _payOs.CancelAsync(orderCode, reason, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Không hủy được link PayOS của hóa đơn {InvoiceCode}.", invoice.InvoiceCode);
                throw Fail(StatusCodes.Status502BadGateway, PaymentGatewayError);
            }
        }

        var now = UtcNow;
        subscription.Status = Cancelled;
        subscription.UpdatedAt = now;
        invoice.Status = Cancelled;
        invoice.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<SaasSubscription> FindAsync(Guid publicId, CancellationToken cancellationToken) =>
        await _db.Subscriptions.Include(s => s.Invoices).SingleOrDefaultAsync(s => s.PublicId == publicId, cancellationToken)
        ?? throw Fail(StatusCodes.Status404NotFound, SubscriptionNotFound);

    private SubscriptionStatusResponse ToStatus(SaasSubscription subscription)
    {
        var invoice = LatestInvoice(subscription);
        var canPay = subscription.Status == Pending && !subscription.IsPaid && invoice.Status == Unpaid && invoice.DueAt > UtcNow;
        return new SubscriptionStatusResponse(subscription.PublicId, subscription.Status, subscription.IsPaid,
            StatusMessage(subscription.Status, subscription.IsPaid), subscription.RequestedTenantCode,
            subscription.RequestedTenantName, invoice.InvoiceCode, invoice.TotalAmount, invoice.Currency,
            canPay ? invoice.PayosCheckoutUrl : null);
    }

    private static SaasInvoice LatestInvoice(SaasSubscription subscription) =>
        subscription.Invoices.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.InvoiceId).First();

    /// <summary>Null nếu mã tổ chức hợp lệ và còn trống (chưa có tenant, chưa bị đơn PENDING còn hạn giữ chỗ).</summary>
    internal async Task<string?> GetTenantCodeErrorAsync(string normalizedCode, CancellationToken cancellationToken)
    {
        if (!IsValidTenantCode(normalizedCode)) return TenantCodeInvalid;

        var now = UtcNow;
        var taken = await _db.Tenants.AnyAsync(t => t.TenantCode.ToLower() == normalizedCode, cancellationToken)
            || await _db.Subscriptions.AnyAsync(s => s.RequestedTenantCode == normalizedCode
                && s.Status == Pending
                && (s.IsPaid || s.Invoices.Any(i => i.Status == Unpaid && i.DueAt > now)), cancellationToken);
        return taken ? TenantCodeTaken : null;
    }

    /// <summary>
    /// Email đã gắn với một tổ chức: có đơn đã thanh toán (đang tạo / ACTIVE / PROVISIONING_FAILED),
    /// hoặc đơn chưa thanh toán còn hạn cho mã tổ chức khác. Đơn cùng mã do luồng "gửi lặp" xử lý.
    /// </summary>
    internal async Task<string?> GetEmailErrorAsync(string normalizedEmail, string normalizedTenantCode, CancellationToken cancellationToken)
    {
        var now = UtcNow;
        var taken = await _db.Subscriptions.AnyAsync(s => s.Customer!.CustomerEmail == normalizedEmail
            && (s.IsPaid
                || (s.Status == Pending && s.RequestedTenantCode != normalizedTenantCode
                    && s.Invoices.Any(i => i.Status == Unpaid && i.DueAt > now))), cancellationToken);
        return taken ? EmailTaken : null;
    }

    internal async Task<SaasPlanVersion> GetActivePlanVersionAsync(long planVersionId, CancellationToken cancellationToken)
    {
        var now = UtcNow;
        return await _db.PlanVersions.Include(v => v.Plan)
            .Where(v => v.PlanVersionId == planVersionId && v.Status == Active
                && v.Plan.PlanStatus == Active
                && v.EffectiveFrom <= now && (v.EffectiveTo == null || v.EffectiveTo > now))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Fail(StatusCodes.Status404NotFound, PlanNotFound);
    }

    /// <summary>Tính giảm giá ở server; voucher rỗng thì không giảm. Không đổi usage_count.</summary>
    internal async Task<(SaasPromotion? Promotion, decimal Discount)> PriceAsync(
        decimal subtotal, string? voucherCode, CancellationToken cancellationToken)
    {
        var code = NormalizeVoucherCode(voucherCode);
        if (code.Length == 0) return (null, 0m);

        var promotion = await _db.Promotions.SingleOrDefaultAsync(p => p.PromotionCode == code, cancellationToken)
            ?? throw Fail(StatusCodes.Status400BadRequest, VoucherNotFound);
        var error = ValidatePromotion(promotion, subtotal, UtcNow);
        if (error is not null) throw Fail(StatusCodes.Status400BadRequest, error);
        return (promotion, CalculateDiscount(subtotal, promotion));
    }

    /// <summary>orderCode PayOS phải duy nhất vĩnh viễn trên merchant, kể cả khi reset DB: dùng thời gian (ms) + 3 số ngẫu nhiên.</summary>
    private long NewOrderCode() =>
        _clock.GetUtcNow().ToUnixTimeMilliseconds() * 1000 + RandomNumberGenerator.GetInt32(1000);

    private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

    private static BusinessRuleException Fail(int status, string code) => new(status, code, Message(code));
}
