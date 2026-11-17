using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractScanKeyword
{
    public long ContractScanKeywordId { get; set; }

    public long FileStorageFileid { get; set; }

    public int ContractScanKeywordType { get; set; }

    public string ContractScanKeywordValue { get; set; } = null!;

    public int? ContractScanKeywordPagenumber { get; set; }

    public int ContractScanKeywordSource { get; set; }

    public long? CreatedByEmployeeId { get; set; }

    public DateTime CreatedAt { get; set; }
}
