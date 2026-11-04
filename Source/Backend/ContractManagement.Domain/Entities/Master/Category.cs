using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class Category
{
    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public string? CategoryShortDesc { get; set; }

    public string? CategoryDesc { get; set; }

    public string? CategoryImage { get; set; }

    public int? CategoryParentid { get; set; }

    public int? CategorySortorder { get; set; }
}
