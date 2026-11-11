using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class DOCUMENT_TYPE
{
    public int document_type_id { get; set; }

    public long employee_id { get; set; }

    public string document_type_name { get; set; } = null!;

    public string document_type_name_en { get; set; } = null!;

    public int document_type_status { get; set; }

    public DateTime created_date { get; set; }

    public DateTime? updated_date { get; set; }

    public byte[] row_version { get; set; } = null!;
}
