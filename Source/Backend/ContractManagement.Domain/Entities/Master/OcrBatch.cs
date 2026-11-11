using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class OcrBatch
{
    public long OcrPatchId { get; set; }

    public long UploadedByEmployeeId { get; set; }

    public int TotalFiles { get; set; }

    public string BatchStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public long? UpdatedByEmployeeId { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
