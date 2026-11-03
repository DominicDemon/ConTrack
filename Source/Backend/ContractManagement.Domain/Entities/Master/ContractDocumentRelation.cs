using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractDocumentRelation
{
    public long ContractDocumentRelationId { get; set; }

    public long ParentDocumentId { get; set; }

    public long ChildDocumentId { get; set; }

    public string ContractDocumentRelationType { get; set; } = null!;

    public DateTime CreatedDate { get; set; }
}
