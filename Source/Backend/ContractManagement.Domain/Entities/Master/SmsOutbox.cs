namespace ContractManagement.Domain.Entities.Master;

public partial class SmsOutbox
{
    public long SmsOutboxId { get; set; }
    public string SmsOutboxPurpose { get; set; } = null!;
    public long? CustomerId { get; set; }
    public long? ContractDocumentId { get; set; }
    public byte[]? SmsOutboxPhoneCiphertext { get; set; }
    public byte[]? SmsOutboxContentCiphertext { get; set; }
    public string SmsOutboxPhoneMasked { get; set; } = null!;
    public DateTime SmsOutboxDeliveryExpiresAt { get; set; }
    public string SmsOutboxStatus { get; set; } = null!;
    public int SmsOutboxAttemptCount { get; set; }
    public DateTime? SmsOutboxNextAttemptAt { get; set; }
    public Guid? SmsOutboxLeaseId { get; set; }
    public DateTime? SmsOutboxLeaseUntil { get; set; }
    public DateTime? SmsOutboxSentAt { get; set; }
    public DateTime? SmsOutboxFailedAt { get; set; }
    public string? SmsOutboxLastFailure { get; set; }
    public string SmsOutboxProvider { get; set; } = null!;
    public string SmsOutboxRequestId { get; set; } = null!;
    public string? SmsOutboxBrandname { get; set; }
    public bool SmsOutboxIsSandbox { get; set; }
    public string? SmsOutboxCodeResult { get; set; }
    public string? SmsOutboxProviderSmsId { get; set; }
    public string? SmsOutboxSendStatus { get; set; }
    public int? SmsOutboxSendSuccess { get; set; }
    public int? SmsOutboxSendFailed { get; set; }
    public string? SmsOutboxTelcoId { get; set; }
    public decimal? SmsOutboxTotalPrice { get; set; }
    public DateTime? SmsOutboxDeliveredAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] Rowversion { get; set; } = null!;
}
