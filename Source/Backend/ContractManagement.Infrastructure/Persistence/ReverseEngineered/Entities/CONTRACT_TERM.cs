using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT_TERM
{
    public long contract_term_termid { get; set; }

    public long contract_document_id { get; set; }

    public long contract_version_id { get; set; }

    public long? contract_term_sourcetemplatetermid { get; set; }

    public string contract_term_termcode { get; set; } = null!;

    public string contract_term_termtitle { get; set; } = null!;

    public string? contract_term_termtitleen { get; set; }

    public string? contract_term_termcontent { get; set; }

    public string? contract_term_termcontenten { get; set; }

    public bool contract_term_isnegotiable { get; set; }

    public int contract_term_termkind { get; set; }

    public int contract_term_bilingualcontentmode { get; set; }

    public long contract_term_displayorder { get; set; }

    public DateTime created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public byte[] rowversion { get; set; } = null!;
}
