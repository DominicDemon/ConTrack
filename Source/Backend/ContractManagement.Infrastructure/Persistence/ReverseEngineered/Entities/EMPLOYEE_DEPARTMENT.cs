using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class EMPLOYEE_DEPARTMENT
{
    public long employee_id { get; set; }

    public long department_id { get; set; }

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }
}
