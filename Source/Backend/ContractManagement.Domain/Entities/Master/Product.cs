using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class Product
{
    public long ProductId { get; set; }

    public string? ProductCode { get; set; }

    public string ProductName { get; set; } = null!;

    public string? ProductNameen { get; set; }

    public string? ProductShortname { get; set; }

    public string? ProductUnitname { get; set; }

    public string? ProductUnitnameen { get; set; }

    public string? ProductSlogan { get; set; }

    public string? ProductShortdesc { get; set; }

    public string? ProductLargeimage { get; set; }

    public string? ProductSmallimage { get; set; }

    public string? ProductIconclass { get; set; }

    public string? ProductDocument { get; set; }

    public string? ProductTags { get; set; }

    public string? ProductDeployment { get; set; }

    public int? CategoryId { get; set; }

    public decimal? ProductSuggestedPrice { get; set; }

    public int? ProductQuantity { get; set; }

    public int? ProductSortorder { get; set; }

    public string? ProductRewrite { get; set; }

    public string? ProductTitlebrowser { get; set; }

    public string? ProductMetadescription { get; set; }

    public string? ProductMetakeyword { get; set; }

    public string? ProductOthers { get; set; }

    public DateTime? ProductCreatedAt { get; set; }
}
