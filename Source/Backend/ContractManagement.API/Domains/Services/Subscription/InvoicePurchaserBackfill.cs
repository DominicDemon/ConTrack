using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.Persistence.Central;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.API.Domains.Services.Subscription;

/// <summary>Conservative, repeatable legacy reconciliation. Never queues historical mail.</summary>
public sealed class InvoicePurchaserBackfill(CentralDbContext db, ITenantDbContextFactory tenants)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var subscriptions = await db.Subscriptions.Where(s => s.PurchaserEmployeeId == null && s.TenantId != null).ToListAsync(ct);
        var changed = 0;
        foreach (var subscription in subscriptions)
        {
            var tenant = await db.Tenants.Include(t => t.TenantDatabase).SingleOrDefaultAsync(t => t.TenantId == subscription.TenantId, ct);
            if (tenant is null || tenant.OwnerCustomerId != subscription.CustomerId) continue;
            var correlationId = $"subscription-{subscription.PublicId}";
            if (await db.SecurityAudits.CountAsync(a => a.TenantId == tenant.TenantId && a.CorrelationId == correlationId
                && a.Action == "TenantProvisioned" && a.Result == "Success", ct) != 1) continue;
            await using var tenantDb = tenants.Create(tenant.TenantDatabase.ConnectionString);
            var correlation = $"subscription-{subscription.PublicId}";
            var audits = await tenantDb.TblAuthorizationAudits.AsNoTracking().Where(a => a.TenantId == tenant.TenantId
                && a.CorrelationId == correlation && a.Action == "EmployeeCreated" && a.Result == "Success"
                && a.TargetType == "Employee" && a.ActorEmployeeId == null).ToListAsync(ct);
            if (audits.Count != 1 || string.IsNullOrWhiteSpace(audits[0].TargetId)) continue;
            var audit = audits[0];
            // Matching an email/account alone is insufficient: it could have been reassigned.
            var employees = await tenantDb.TblEmployees.AsNoTracking().Where(e => e.EmployeeAccount == audit.TargetId).ToListAsync(ct);
            if (employees.Count != 1 || employees[0].DateCreated is not { } created
                || Math.Abs((created - audit.OccurredAt).TotalSeconds) > 2) continue;
            subscription.PurchaserEmployeeId = employees[0].EmployeeId;
            changed++;
        }
        await db.SaveChangesAsync(ct);
        return changed;
    }
}
