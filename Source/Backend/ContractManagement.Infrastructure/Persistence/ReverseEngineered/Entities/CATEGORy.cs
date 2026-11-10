using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CATEGORy
{
    public int category_id { get; set; }

    public string category_name { get; set; } = null!;

    public string? category_short_desc { get; set; }

    public string? category_desc { get; set; }

    public string? category_image { get; set; }

    public int? category_parentid { get; set; }

    public int? category_sortorder { get; set; }
}
