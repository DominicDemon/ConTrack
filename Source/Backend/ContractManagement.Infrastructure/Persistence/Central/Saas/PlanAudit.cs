namespace ContractManagement.Infrastructure.Persistence.Central.Saas;

public sealed class PlanAudit
{
    public long Id { get; set; }
    public long PlanId { get; set; }
    public int ActorId { get; set; }
    public string Action { get; set; } = "";
    public string BeforeJson { get; set; } = "";
    public string AfterJson { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string? IpAddress { get; set; }
    public string CorrelationId { get; set; } = "";
}
