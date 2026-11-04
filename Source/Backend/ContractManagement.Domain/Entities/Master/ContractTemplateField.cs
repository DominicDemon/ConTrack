using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractTemplateField
{
    public long ContractTemplateFieldId { get; set; }

    public long ContractTemplateId { get; set; }

    public string ContractTemplateFieldKey { get; set; } = null!;

    public string ContractTemplateFieldLabel { get; set; } = null!;

    public string ContractTemplateFieldDataType { get; set; } = null!;

    public bool IsRequired { get; set; }

    public string? ExtraOptions { get; set; }
}
