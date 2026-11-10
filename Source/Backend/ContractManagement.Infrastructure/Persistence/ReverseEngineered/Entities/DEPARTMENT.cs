using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class DEPARTMENT
{
    public long department_id { get; set; }

    public string department_code { get; set; } = null!;

    public string department_name { get; set; } = null!;

    public string? department_description { get; set; }

    public string department_status { get; set; } = null!;

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }

    public DateTime? updated_at { get; set; }

    public long? updated_by { get; set; }
}
