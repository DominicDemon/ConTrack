using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class TENANT
{
    public long tenant_id { get; set; }

    public long? customer_id { get; set; }

    public long saas_type_id { get; set; }

    public long package_level_id { get; set; }

    public string tenant_code { get; set; } = null!;

    public string tenant_name { get; set; } = null!;

    public string tenant_subdomain { get; set; } = null!;

    public string tenant_status { get; set; } = null!;

    public DateTime? tenant_expired_at { get; set; }

    public DateTime? activated_at { get; set; }

    public DateTime created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public DateTime? suspended_at { get; set; }
}
