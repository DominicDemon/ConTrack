using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class OCR_BATCH
{
    public long ocr_patch_id { get; set; }

    public long uploaded_by_employee_id { get; set; }

    public int total_files { get; set; }

    public string batch_status { get; set; } = null!;

    public DateTime created_at { get; set; }

    public long? updated_by_employee_id { get; set; }

    public DateTime? updated_at { get; set; }
}
