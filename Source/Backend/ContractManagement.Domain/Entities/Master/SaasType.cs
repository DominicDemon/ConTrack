using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class SaasType
{
    public long SaasTypeId { get; set; }

    public long ProductId { get; set; }

    public string SaasTypeCode { get; set; } = null!;

    public string SaasTypeName { get; set; } = null!;

    public string? SaasTypeDescription { get; set; }

    public string SaasTypeStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}
