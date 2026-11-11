using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT_NEGOTIATION_COMMENT
{
    public long contract_negotiation_comment_id { get; set; }

    public long contract_document_id { get; set; }

    public long contract_version_id { get; set; }

    public int? contract_negotiation_comment_termid { get; set; }

    public int? contract_negotiation_comment_parentcommentid { get; set; }

    public int? contract_negotiation_comment_carriedforwardfromcommentid { get; set; }

    public int? contract_negotiation_comment_carriedforwardfromversionid { get; set; }

    public string contract_negotiation_comment_source { get; set; } = null!;

    public string contract_negotiation_comment_content { get; set; } = null!;

    public long? recorded_by_employee_id { get; set; }

    public long? customer_access_session_id { get; set; }

    public int contract_negotiation_comment_state { get; set; }

    public DateTime created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public byte[] rowversion { get; set; } = null!;
}
