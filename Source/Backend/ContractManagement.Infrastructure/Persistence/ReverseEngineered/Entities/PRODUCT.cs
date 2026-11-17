using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class PRODUCT
{
    public long product_id { get; set; }

    public string? product_code { get; set; }

    public string product_name { get; set; } = null!;

    public string? product_nameen { get; set; }

    public string? product_shortname { get; set; }

    public string? product_unitname { get; set; }

    public string? product_unitnameen { get; set; }

    public string? product_slogan { get; set; }

    public string? product_shortdesc { get; set; }

    public string? product_details { get; set; }

    public string? product_largeimage { get; set; }

    public string? product_smallimage { get; set; }

    public string? product_iconclass { get; set; }

    public string? product_document { get; set; }

    public string? product_tags { get; set; }

    public string? product_deployment { get; set; }

    public int? category_id { get; set; }

    public decimal? product_suggested_price { get; set; }

    public int? product_quantity { get; set; }

    public int? product_sortorder { get; set; }

    public string? product_rewrite { get; set; }

    public string? product_titlebrowser { get; set; }

    public string? product_metadescription { get; set; }

    public string? product_metakeyword { get; set; }

    public string? product_others { get; set; }

    public DateTime? product_created_at { get; set; }
}
