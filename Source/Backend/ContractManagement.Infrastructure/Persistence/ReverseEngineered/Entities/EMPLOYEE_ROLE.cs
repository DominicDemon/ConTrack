using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class EMPLOYEE_ROLE
{
    public long employee_role_id { get; set; }

    public long employee_id { get; set; }

    public long system_role_id { get; set; }

    public DateTime system_member_role_assigned_at { get; set; }

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }

    public DateTime? updated_at { get; set; }
}
