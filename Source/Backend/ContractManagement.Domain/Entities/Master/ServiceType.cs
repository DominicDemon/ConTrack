using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ServiceType
{
    public int ServicetypeId { get; set; }

    public string ServicetypeName { get; set; } = null!;
}
