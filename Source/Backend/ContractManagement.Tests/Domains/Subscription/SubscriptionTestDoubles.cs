using ContractManagement.API.Domains.Email;
using ContractManagement.API.Domains.Payments;
using ContractManagement.Infrastructure.MultiTenancy.Contracts;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using Microsoft.EntityFrameworkCore;
using PayOS.Exceptions;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace ContractManagement.Tests.Domains.Subscription;

internal static class SubscriptionTestDb
{
    public static CentralDbContext Create(string? name = null)
    {
        var db = new CentralDbContext(new DbContextOptionsBuilder<CentralDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString()).Options);
        db.Database.EnsureCreated(); // nạp seed HasData (gói TRIAL/STANDARD, voucher TEST10)
        return db;
    }
}

internal sealed class FixedClock(DateTime now) : TimeProvider
{
    public DateTime Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => new(Now);
}

/// <summary>PayOS giả: webhook có Signature == "valid" mới qua kiểm tra chữ ký.</summary>
internal sealed class FakePayOs : IPayOsGateway
{
    public bool FailCreate { get; init; }
    public List<CreatePaymentLinkRequest> Created { get; } = [];
    public List<long> Cancelled { get; } = [];
    public Dictionary<long, PayOsPaymentInfo> Payments { get; } = [];

    public Task<PayOsLink> CreatePaymentLinkAsync(CreatePaymentLinkRequest request, CancellationToken cancellationToken)
    {
        if (FailCreate) throw new HttpRequestException("down");
        Created.Add(request);
        return Task.FromResult(new PayOsLink($"link-{Created.Count}", $"https://pay.test/{Created.Count}"));
    }

    public Task<PayOsPaymentInfo> GetPaymentAsync(long orderCode, CancellationToken cancellationToken) =>
        Task.FromResult(Payments.GetValueOrDefault(orderCode, new PayOsPaymentInfo(PaymentLinkStatus.Pending, 0, null)));

    public Task CancelAsync(long orderCode, string reason, CancellationToken cancellationToken)
    {
        Cancelled.Add(orderCode);
        return Task.CompletedTask;
    }

    public Task<WebhookData> VerifyWebhookAsync(Webhook webhook) =>
        webhook.Signature == "valid" ? Task.FromResult(webhook.Data) : throw new WebhookException("Invalid signature");
}

internal sealed class RecordingEmail : IEmailTransport
{
    public List<EmailMessage> Sent { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

/// <summary>Provisioning giả: thêm Tenant vào Central DB như service thật, hoặc ném lỗi khi Fail = true.</summary>
internal sealed class FakeProvisioning(CentralDbContext db) : ITenantProvisioningService
{
    public bool Fail { get; set; }
    public List<TenantProvisioningCommand> Commands { get; } = [];

    public Task<IReadOnlyList<TenantProvisioningResult>> GetAllAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public async Task<TenantProvisioningResult> CreateDedicatedAsync(TenantProvisioningCommand command, CancellationToken cancellationToken = default)
    {
        Commands.Add(command);
        if (Fail) throw new InvalidOperationException("database down");
        var tenant = new Tenant
        {
            TenantCode = command.TenantCode, TenantName = command.TenantName, Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow,
            TenantDatabase = new TenantDatabase
            {
                DatabaseKey = command.TenantCode, DatabaseName = command.TenantCode, ConnectionString = "c",
                Mode = TenantDatabaseMode.Dedicated, CreatedAt = DateTime.UtcNow
            }
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
        return new TenantProvisioningResult(tenant.TenantId, tenant.TenantCode, tenant.TenantName, "d",
            TenantDatabaseMode.Dedicated, TenantStatus.Active, 1);
    }
}
