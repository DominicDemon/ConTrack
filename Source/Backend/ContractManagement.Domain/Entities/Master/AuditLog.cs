using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class AuditLog
{
    public long AuditLogId { get; set; }

    public string ActionType { get; set; } = null!;

    public string EntityName { get; set; } = null!;

    public string EntityId { get; set; } = null!;

    public string ActorType { get; set; } = null!;

    public long? ActorId { get; set; }

    public string? ActorIpAddress { get; set; }

    public string? OldData { get; set; }

    public string? NewData { get; set; }

    public DateTime CreatedAt { get; set; }
}
