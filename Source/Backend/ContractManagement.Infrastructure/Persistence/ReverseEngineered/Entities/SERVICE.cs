using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class SERVICE
{
    public long service_id { get; set; }

    public string service_name { get; set; } = null!;

    public string? service_nameen { get; set; }

    public string? service_unitname { get; set; }

    public string? service_unitnameen { get; set; }

    public decimal? service_price { get; set; }

    public int? servicetype_id { get; set; }

    public int? service_region { get; set; }

    public string? service_imageicon { get; set; }

    public long? service_fptaccounts { get; set; }

    public int? service_mysql { get; set; }

    public bool service_haschild { get; set; }

    public string? service_rewrite { get; set; }

    public string? service_titlebrowser { get; set; }

    public string? service_metadescription { get; set; }

    public string? service_metakeyword { get; set; }

    public string? service_others { get; set; }

    public int service_status { get; set; }

    public DateTime? service_datecreated { get; set; }

    public DateTime? service_datemodified { get; set; }
}
