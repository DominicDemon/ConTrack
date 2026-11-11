namespace ContractManagement.Infrastructure.Persistence.Application.Models;

/// <summary>
/// Server-persisted public customer session. Its cookie contains an opaque secret only.
/// </summary>
public sealed class CONTRACT_CUSTOMER_ACCESS_SESSION
{
    public int CustomerAccessSessionId { get; set; }

    public int TenantId { get; set; }

    public int LinkId { get; set; }

    public int ContractId { get; set; }

    public int VersionId { get; set; }

    public int VerificationPhoneId { get; set; }

    public string SessionTokenHash { get; set; } = null!;

    public DateTime IssuedAt { get; set; }

    public DateTime LastActivityAt { get; set; }

    public DateTime IdleExpiresAt { get; set; }

    public DateTime HardExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? RevocationReason { get; set; }

    public byte[] RowVersion { get; set; } = null!;
}


