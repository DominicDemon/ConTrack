using System.Globalization;
using System.Net;
using ContractManagement.API.Domains.Email;
using ContractManagement.API.Domains.Recovery;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Saas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContractManagement.API.Domains.Services.Subscription;

public sealed class InvoiceEmailOptions
{
    // Enable SMTP only after validating the message through the test provider.
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Test";
}

public static class InvoiceEmailTemplate
{
    public static EmailMessage Build(SaasInvoice invoice, string frontendUrl)
    {
        var link = $"{frontendUrl.TrimEnd('/')}/billing/receipts/{invoice.PublicId}";
        var amount = invoice.TotalAmount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " " + invoice.Currency;
        var duration = invoice.DurationMonthsSnapshot is { } months ? $"{months} tháng" : $"{invoice.DurationDaysSnapshot} ngày";
        string H(string? value) => WebUtility.HtmlEncode(value ?? "");
        return new(invoice.BillingEmail!, $"Xác nhận thanh toán {invoice.InvoiceCode} — eContract",
            $"Đã nhận thanh toán {amount} cho {invoice.BillingName}. Gói {invoice.PlanNameSnapshot}, {duration}.\nMã: {invoice.InvoiceCode}\nXem xác nhận thanh toán: {link}\nĐăng nhập bằng tài khoản mua gói ban đầu. Đây là xác nhận thanh toán, không phải hóa đơn GTGT.",
            $"""
            <!doctype html><html lang="vi"><body style="margin:0;background:#f5f7fa;font-family:Arial,sans-serif;color:#142033">
            <main style="max-width:560px;margin:24px auto;padding:32px;background:white;border-radius:16px">
            <p style="color:#005a9c;font-weight:bold">eContract · DTC</p><h1 style="font-size:24px">Xác nhận thanh toán</h1>
            <p>Đã nhận thanh toán cho <strong>{H(invoice.BillingName)}</strong>.</p>
            <p>Mã chứng từ: <strong>{H(invoice.InvoiceCode)}</strong><br>Gói: {H(invoice.PlanNameSnapshot)} · {H(duration)}</p>
            <p style="font-size:24px;font-weight:bold">{H(amount)}</p>
            <p><a href="{H(link)}" style="display:inline-block;padding:14px 20px;background:#005a9c;color:white;text-decoration:none;border-radius:8px">Xem xác nhận thanh toán</a></p>
            <p>Đăng nhập bằng tài khoản mua gói ban đầu. Thông tin đăng nhập được gửi trong email tài khoản riêng.</p>
            <p style="font-size:12px;color:#596579">Đây là xác nhận thanh toán, không phải hóa đơn GTGT.</p>
            </main></body></html>
            """);
    }
}

public sealed class InvoiceEmailWorker(IServiceScopeFactory scopes, IOptions<InvoiceEmailOptions> options,
    IOptions<AccountRecoveryOptions> frontend, IEmailTransport transport, RecoveryTestMailbox mailbox,
    TimeProvider clock, ILogger<InvoiceEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled) return;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await ProcessAsync(scope.ServiceProvider.GetRequiredService<CentralDbContext>(), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Payment confirmation email worker failed; check database/schema availability."); }
            try { await Task.Delay(TimeSpan.FromSeconds(10), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }

    public async Task ProcessAsync(CentralDbContext db, CancellationToken ct, Guid? invoicePublicId = null)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var ids = await db.InvoiceEmailOutbox.AsNoTracking()
            .Where(x => invoicePublicId == null || x.Invoice.PublicId == invoicePublicId)
            .Where(x =>
            (x.Status == "PENDING" && x.NextAttemptAt <= now) || (x.Status == "LEASED" && x.LeaseUntil <= now))
            .OrderBy(x => x.OutboxId).Select(x => x.OutboxId).Take(20).ToListAsync(ct);
        foreach (var id in ids)
        {
            db.ChangeTracker.Clear();
            var row = await db.InvoiceEmailOutbox.Include(x => x.Invoice).ThenInclude(x => x.Subscription)
                .SingleAsync(x => x.OutboxId == id, ct);
            now = clock.GetUtcNow().UtcDateTime;
            if (row.Status is "SENT" or "FAILED" || row.NextAttemptAt > now || row.LeaseUntil > now) continue;
            row.Revision = Guid.NewGuid();
            row.LeaseId = Guid.NewGuid();
            row.LeaseUntil = now.AddMinutes(2);
            row.Status = "LEASED";
            if (row.AttemptCount >= 5) { row.Status = "FAILED"; row.FailureCode = "AttemptsExhausted"; }
            else row.AttemptCount++;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { continue; }
            if (row.Status == "FAILED") continue;
            try
            {
                if (row.Invoice.Status != "PAID" || row.Invoice.TotalAmount <= 0 ||
                    row.Invoice.Subscription?.PurchaserEmployeeId is null || string.IsNullOrWhiteSpace(row.Invoice.BillingEmail))
                    throw new InvalidOperationException("Invoice is not eligible for delivery.");
                var mail = InvoiceEmailTemplate.Build(row.Invoice, frontend.Value.TenantFrontendBaseUrl ?? "http://localhost:3000");
                // Bound the transport call to less than the lease, including a hung SMTP server.
                using var send = CancellationTokenSource.CreateLinkedTokenSource(ct);
                send.CancelAfter(TimeSpan.FromSeconds(60));
                if (options.Value.Provider == "Test") mailbox.Add(mail);
                else await transport.SendAsync(mail, send.Token).WaitAsync(send.Token);
                row.Status = "SENT"; row.SentAt = clock.GetUtcNow().UtcDateTime; row.FailureCode = null;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                row.FailureCode = "DeliveryFailed";
                row.Status = row.AttemptCount >= 5 ? "FAILED" : "PENDING";
                row.NextAttemptAt = clock.GetUtcNow().UtcDateTime.AddSeconds(30 * Math.Pow(2, row.AttemptCount - 1));
                logger.LogWarning("Payment confirmation {OutboxId} delivery failed, attempt {Attempt}.", id, row.AttemptCount);
            }
            row.LeaseId = null; row.LeaseUntil = null; row.Revision = Guid.NewGuid();
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { /* Never overwrite a newer lease. */ }
        }
    }
}
