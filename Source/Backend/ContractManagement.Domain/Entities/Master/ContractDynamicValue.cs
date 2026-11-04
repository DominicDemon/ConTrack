using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractDynamicValue
{
    public long ContractDynamicValueId { get; set; }

    public long ContractDocumentId { get; set; }

    public long ContractVersionId { get; set; }

    public string FieldKey { get; set; } = null!;

    public string FieldValue { get; set; } = null!;
}
