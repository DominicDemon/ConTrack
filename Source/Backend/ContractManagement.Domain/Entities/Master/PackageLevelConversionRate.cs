using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class PackageLevelConversionRate
{
    public long PackageLevelConversionRateId { get; set; }

    public long SaasTypeId { get; set; }

    public long FromLevelId { get; set; }

    public long ToLevelId { get; set; }

    public decimal ConversionRate { get; set; }
}
