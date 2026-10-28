using ContractManagement.Infrastructure.Persistence.Central;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.API.Domains.Services.Subscription;

public sealed record InvoiceReadiness(string Status, bool IsPaid, string? TenantCode, string? ReturnTo);
public sealed record InvoiceListItem(Guid PublicId, string InvoiceCode, string? PlanName,
    int? DurationMonths, int? DurationDays, decimal TotalAmount, string Currency,
    DateTime? PaidAt, DateTime? StartAt, DateTime? EndAt);
public sealed record InvoiceListResponse(IReadOnlyList<InvoiceListItem> Items, int Total, int Page, int PageSize);
public sealed record PaymentConfirmation(Guid PublicId, string InvoiceCode, string Status,
    string BillingName, string? BuyerName, string? BillingEmail, string? BillingPhone,
    string? BillingAddress, string? BillingTaxIdentifier, string? PlanName,
    int? DurationMonths, int? DurationDays, decimal Subtotal, decimal DiscountAmount,
    decimal TotalAmount, string Currency, DateTime? PaidAt, string? PaymentMethod, string? PaymentReference);

public sealed class InvoiceService(CentralDbContext db)
{
    public async Task<InvoiceListResponse> ListAsync(int tenantId, int employeeId, int page, CancellationToken ct)
    {
        const int pageSize = 20;
        var query = db.SaasInvoices.AsNoTracking().Where(x => x.Status == "PAID" && x.TotalAmount > 0
            && x.Subscription != null && x.Subscription.TenantId == tenantId
            && x.Subscription.PurchaserEmployeeId == employeeId);
        var total = await query.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
        var items = await query.OrderByDescending(x => x.PaidAt).ThenByDescending(x => x.InvoiceId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new InvoiceListItem(x.PublicId, x.InvoiceCode, x.PlanNameSnapshot,
                x.DurationMonthsSnapshot, x.DurationDaysSnapshot, x.TotalAmount, x.Currency,
                x.PaidAt, x.Subscription!.StartAt, x.Subscription.EndAt)).ToListAsync(ct);
        return new(items, total, page, pageSize);
    }

    public async Task<InvoiceReadiness?> GetReadinessAsync(Guid publicId, CancellationToken ct)
    {
        var invoice = await db.SaasInvoices.AsNoTracking().Include(x => x.Subscription)
            .SingleOrDefaultAsync(x => x.PublicId == publicId, ct);
        if (invoice?.Subscription is not { } subscription) return null;
        var paid = invoice.Status == "PAID";
        var status = subscription.Status;
        if (paid && subscription.TenantId is { } tenantId && status is not ("PENDING" or "PROVISIONING_FAILED"))
        {
            var available = await db.Tenants.AnyAsync(t => t.TenantId == tenantId
                && t.Status == ContractManagement.Infrastructure.MultiTenancy.Enums.TenantStatus.Active, ct);
            status = !available ? "UNAVAILABLE" : subscription.PurchaserEmployeeId is null
                ? "RECONCILIATION_REQUIRED" : "READY";
        }
        return new(status, paid, status == "READY" ? subscription.RequestedTenantCode : null,
            status == "READY" ? $"/billing/invoices/{invoice.PublicId}" : null);
    }

    // Authentication/password state is checked by SessionAuthorize on EVERY request.
    public async Task<PaymentConfirmation?> GetAsync(Guid publicId, int tenantId, int employeeId, CancellationToken ct)
    {
        var invoice = await db.SaasInvoices.AsNoTracking()
            .Where(x => x.PublicId == publicId && x.Status == "PAID" && x.Subscription != null
                && x.Subscription.TenantId == tenantId && x.Subscription.PurchaserEmployeeId == employeeId)
            .Include(x => x.Payments).SingleOrDefaultAsync(ct);
        if (invoice is null) return null;
        var payment = invoice.Payments.Where(p => p.Status == "SUCCESS").OrderByDescending(p => p.PaidAt).FirstOrDefault();
        return new(invoice.PublicId, invoice.InvoiceCode, invoice.Status, invoice.BillingName,
            invoice.BuyerNameSnapshot, invoice.BillingEmail, invoice.BillingPhone,
            invoice.BillingAddress, invoice.BillingTaxIdentifier, invoice.PlanNameSnapshot,
            invoice.DurationMonthsSnapshot, invoice.DurationDaysSnapshot, invoice.Subtotal,
            invoice.DiscountAmount, invoice.TotalAmount, invoice.Currency, invoice.PaidAt,
            payment?.PaymentMethod, payment?.ProviderTransactionId ?? payment?.PaymentReference);
    }
}
