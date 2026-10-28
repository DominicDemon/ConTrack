namespace ContractManagement.Infrastructure.Persistence.Central.Saas;

public sealed class InvoiceEmailOutbox
{
    public long OutboxId { get; set; }
    public long InvoiceId { get; set; }
    public string Kind { get; set; } = "PaymentConfirmation";
    public string Status { get; set; } = "PENDING";
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime? SentAt { get; set; }
    public string? FailureCode { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public SaasInvoice Invoice { get; set; } = null!;
}
