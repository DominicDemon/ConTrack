using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class SystemRole
{
    public long SystemRoleId { get; set; }

    public string SystemRoleCode { get; set; } = null!;

    public string SystemRoleName { get; set; } = null!;

    public string SystemRoleScope { get; set; } = null!;

    public string? SystemRoleDescription { get; set; }

    public string SystemRoleStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}
