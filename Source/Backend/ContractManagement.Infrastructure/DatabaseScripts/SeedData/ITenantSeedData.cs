using ContractManagement.Infrastructure.MultiTenancy.Contracts;

namespace ContractManagement.Infrastructure.DatabaseScripts.SeedData;

public interface ITenantSeedData
{
    Task<int> InitializeAsync(
        string connectionString,
        int tenantId,
        InitialManagerProvisioningCommand initialManager,
        SecurityOperationContext securityContext,
        CancellationToken cancellationToken = default);
}
