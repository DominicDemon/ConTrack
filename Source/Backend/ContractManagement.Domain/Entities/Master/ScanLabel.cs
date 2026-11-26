using System;

namespace ContractManagement.Domain.Entities.Master;

/// <summary>A label shared by the whole company to sort scanned contracts (SCAN_LABEL, script 024).</summary>
public partial class ScanLabel
{
    public long ScanLabelId { get; set; }

    public string ScanLabelName { get; set; } = null!;

    public string ScanLabelColor { get; set; } = null!;

    public long? CreatedByEmployeeId { get; set; }

    public DateTime CreatedAt { get; set; }
}
