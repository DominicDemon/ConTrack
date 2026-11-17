using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class TENANT_DATABASE
{
    public long tenant_database_id { get; set; }

    public long tenant_id { get; set; }

    public string tenant_database_provider { get; set; } = null!;

    public string tenant_database_host { get; set; } = null!;

    public int tenant_database_port { get; set; }

    public string tenant_database_db_name { get; set; } = null!;

    public string tenant_database_user { get; set; } = null!;

    public string? tenant_database_password_encrypted { get; set; }

    public string? tenant_schema_version { get; set; }

    public string tenant_database_provisioning_status { get; set; } = null!;

    public DateTime created_at { get; set; }

    public DateTime? updated_at { get; set; }
}
