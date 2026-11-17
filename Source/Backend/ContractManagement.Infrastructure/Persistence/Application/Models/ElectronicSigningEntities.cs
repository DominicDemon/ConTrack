namespace ContractManagement.Infrastructure.Persistence.Application.Models;

public sealed class ContractSigningSession
{
    public long Id { get; set; }
    public int ContractId { get; set; }
    public int VersionId { get; set; }
    public byte Status { get; set; }
    public bool IsLocked { get; set; }
    public int LinkLifetimeHours { get; set; }
    public DateTime? LockedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? RejectionReason { get; set; }
    public long? RejectedByEmployeeId { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;
    public ICollection<ContractSigningSigner> Signers { get; set; } = [];
    public ICollection<ContractSigningAuditLog> AuditLogs { get; set; } = [];
    public ICollection<ContractSigningArtifact> Artifacts { get; set; } = [];
}

public sealed class ContractSigningArtifact
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public byte Kind { get; set; }
    public string TenantCode { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";
    public string Sha256 { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public long? CreatedByEmployeeId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ContractSigningSession Session { get; set; } = null!;
}

public sealed class ContractSigningSigner
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public byte Role { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public byte Status { get; set; }
    public int SigningOrder { get; set; }
    public DateTime? OtpWindowStartedAt { get; set; }
    public DateTime? OtpLastRequestedAt { get; set; }
    public int OtpRequestCount { get; set; }
    public DateTime? SignedAt { get; set; }
    public string? SignatureStorageKey { get; set; }
    public string? SignatureSha256 { get; set; }
    public int? PageNumber { get; set; }
    public decimal? PositionX { get; set; }
    public decimal? PositionY { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public byte[] RowVersion { get; set; } = null!;
    public ContractSigningSession Session { get; set; } = null!;
    public ICollection<ContractSigningLink> Links { get; set; } = [];
    public ICollection<ContractSigningOtp> Otps { get; set; } = [];
}

public sealed class ContractSigningLink
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public long SignerId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public ContractSigningSession Session { get; set; } = null!;
    public ContractSigningSigner Signer { get; set; } = null!;
}

public sealed class ContractSigningOtp
{
    public long Id { get; set; }
    public long SignerId { get; set; }
    public string ChallengeId { get; set; } = string.Empty;
    public string OtpHash { get; set; } = string.Empty;
    public byte[]? OtpCiphertext { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int FailedAttemptCount { get; set; }
    public string DeliveryStatus { get; set; } = "Pending";
    public int DeliveryAttemptCount { get; set; }
    public DateTime? DeliveryNextAttemptAt { get; set; }
    public string? DeliveryLeaseId { get; set; }
    public DateTime? DeliveryLeaseUntil { get; set; }
    public string? DeliveryLastFailure { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;
    public ContractSigningSigner Signer { get; set; } = null!;
}

public sealed class ContractSigningAuditLog
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public long? SignerId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? EventDataJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public ContractSigningSession Session { get; set; } = null!;
}
