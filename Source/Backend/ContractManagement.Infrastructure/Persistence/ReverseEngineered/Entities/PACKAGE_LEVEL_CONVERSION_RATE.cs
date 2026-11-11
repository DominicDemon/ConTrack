using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class PACKAGE_LEVEL_CONVERSION_RATE
{
    public long package_level_conversion_rate_id { get; set; }

    public long saas_type_id { get; set; }

    public long from_level_id { get; set; }

    public long to_level_id { get; set; }

    public decimal conversion_rate { get; set; }
}
