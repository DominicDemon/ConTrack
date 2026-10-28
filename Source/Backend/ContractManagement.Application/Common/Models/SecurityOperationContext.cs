namespace ContractManagement.Infrastructure.MultiTenancy.Contracts;

/// <summary>
/// Non-secret request metadata used only for security audit records.
/// SystemAdminId is null when the tenant is created by self-service subscription.
/// </summary>
public sealed record SecurityOperationContext(
    int? SystemAdminId,
    string? IpAddress,
    string? UserAgent,
    string CorrelationId);
