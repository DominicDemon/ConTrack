using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class TicketSupportEmployee
{
    public long TicketSupportEmployeeId { get; set; }

    public long? TicketSupportId { get; set; }

    public long? EmployeeId { get; set; }

    public string? TicketSupportEmployeeAssignmentRole { get; set; }

    public DateTime? TicketSupportEmployeeAssignedAt { get; set; }

    public DateTime? TicketSupportEmployeeUnassignedAt { get; set; }

    public long? TicketSupportEmployeeAssignedByEmployeeId { get; set; }
}
