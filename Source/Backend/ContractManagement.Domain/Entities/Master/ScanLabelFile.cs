using System;

namespace ContractManagement.Domain.Entities.Master;

/// <summary>A label put on a scan file (SCAN_LABEL_FILE, script 024). A scan may carry several labels.</summary>
public partial class ScanLabelFile
{
    public long ScanLabelId { get; set; }

    public long FileStorageFileid { get; set; }

    public long? AssignedByEmployeeId { get; set; }

    public DateTime AssignedAt { get; set; }
}
