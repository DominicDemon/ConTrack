using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT_ELECTRONIC_SIGNATURE
{
    public long contract_electronic_signature_id { get; set; }

    public long contract_document_id { get; set; }

    public long contract_version_id { get; set; }

    public int contract_electronic_partytype { get; set; }

    public string contract_electronic_signertype { get; set; } = null!;

    public long? employee_id { get; set; }

    public string contract_electronic_signername { get; set; } = null!;

    public string? contract_electronic_signertitle { get; set; }

    public string contract_electronic_signerphone { get; set; } = null!;

    public string? contract_electronic_signeremail { get; set; }

    public string contract_electronic_signaturetype { get; set; } = null!;

    public string? contract_electronic_signatureimagestoragekey { get; set; }

    public int? contract_electronic_customerotpchallengeid { get; set; }

    public int contract_electronic_status { get; set; }

    public DateTime? contract_electronic_signedat { get; set; }

    public string? contract_electronic_ipaddress { get; set; }

    public string? contract_electronic_useragent { get; set; }

    public string? contract_electronic_document_hash_at_signing { get; set; }

    public int? contract_electronic_pagenumber { get; set; }

    public decimal? contract_electronic_positionx { get; set; }

    public decimal? contract_electronic_positiony { get; set; }

    public decimal? contract_electronic_width { get; set; }

    public decimal? contract_electronic_height { get; set; }

    public string? contract_electronic_reject_reason { get; set; }

    public DateTime created_at { get; set; }

    public byte[] rowversion { get; set; } = null!;
}
