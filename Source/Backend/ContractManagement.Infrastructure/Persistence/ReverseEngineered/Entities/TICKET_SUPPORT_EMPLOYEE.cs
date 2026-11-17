using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class TICKET_SUPPORT_EMPLOYEE
{
    public long ticket_support_employee_id { get; set; }

    public long? ticket_support_id { get; set; }

    public long? employee_id { get; set; }

    public string? ticket_support_employee_assignment_role { get; set; }

    public DateTime? ticket_support_employee_assigned_at { get; set; }

    public DateTime? ticket_support_employee_unassigned_at { get; set; }

    public long? ticket_support_employee_assigned_by_employee_id { get; set; }
}
