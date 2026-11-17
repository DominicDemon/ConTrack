using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT_DOCUMENT_RELATION
{
    public long contract_document_relation_id { get; set; }

    public long parent_document_id { get; set; }

    public long child_document_id { get; set; }

    public string contract_document_relation_type { get; set; } = null!;

    public DateTime created_date { get; set; }
}
