namespace ContractManagement.Domain.Entities.Master;

public sealed class ContractCustomerOtpChallenge
{
    public int ContractCustomerOtpChallengeId { get; set; }
    public long ContractAccessLinkId { get; set; }
    public long ContractElectronicSignatureId { get; set; }
    public string PublicChallengeId { get; set; } = null!;
    public string ProtectedOtp { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public int FailedAttemptCount { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] Rowversion { get; set; } = null!;
}
