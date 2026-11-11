using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT_VERSION
{
    public long contract_version_id { get; set; }

    public long contract_document_id { get; set; }

    public long contract_version_version_no { get; set; }

    public long contract_template_id { get; set; }

    public string? contract_version_changenote { get; set; }

    public decimal contract_version_subtotal { get; set; }

    public decimal contract_version_totaldiscount { get; set; }

    public decimal contract_version_totalvat { get; set; }

    public decimal contract_version_totalamount { get; set; }

    public string contract_version_currencycode { get; set; } = null!;

    public bool contract_version_islocked { get; set; }

    public DateTime? contract_version_lockeddate { get; set; }

    public int? contract_version_lockedbyemployeeid { get; set; }

    public string? contract_version_snapshothash { get; set; }

    public DateTime? contract_version_signdate { get; set; }

    public DateTime? contract_version_effective_date { get; set; }

    public DateTime? contract_version_expire_date { get; set; }

    public DateTime contract_version_created_date { get; set; }

    public byte[] contract_version_rowversion { get; set; } = null!;
}
