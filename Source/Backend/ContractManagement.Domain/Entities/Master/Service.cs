using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class Service
{
    public long ServiceId { get; set; }

    public string ServiceName { get; set; } = null!;

    public string? ServiceNameen { get; set; }

    public string? ServiceUnitname { get; set; }

    public string? ServiceUnitnameen { get; set; }

    public decimal? ServicePrice { get; set; }

    public int? ServicetypeId { get; set; }

    public int? ServiceRegion { get; set; }

    public string? ServiceImageicon { get; set; }

    public long? ServiceFptaccounts { get; set; }

    public int? ServiceMysql { get; set; }

    public bool ServiceHaschild { get; set; }

    public string? ServiceRewrite { get; set; }

    public string? ServiceTitlebrowser { get; set; }

    public string? ServiceMetadescription { get; set; }

    public string? ServiceMetakeyword { get; set; }

    public string? ServiceOthers { get; set; }

    public int ServiceStatus { get; set; }

    public DateTime? ServiceDatecreated { get; set; }

    public DateTime? ServiceDatemodified { get; set; }
}
