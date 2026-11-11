using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT_PAYMENT_MILESTONE
{
    public long contract_payment_milestone_id { get; set; }

    public long contract_document_id { get; set; }

    public long contract_version_id { get; set; }

    public long contract_term_id { get; set; }

    public long? invoice_id { get; set; }

    public string contract_payment_milestone_code { get; set; } = null!;

    public string contract_payment_milestone_titlevi { get; set; } = null!;

    public string? contract_payment_milestone_titleen { get; set; }

    public decimal contract_payment_milestone_paymentpercent { get; set; }

    public decimal contract_payment_milestone_amount { get; set; }

    public int contract_payment_milestone_dueoffsetdays { get; set; }

    public int contract_payment_milestone_dueanchor { get; set; }

    public int contract_payment_milestone_daycountmode { get; set; }

    public DateOnly? contract_payment_milestone_anchordate { get; set; }

    public DateOnly? contract_payment_milestone_duedate { get; set; }

    public int contract_payment_milestone_paymentstatus { get; set; }

    public DateTime? contract_payment_milestone_paidat { get; set; }

    public int? contract_payment_milestone_paidbyemployeeid { get; set; }

    public string? contract_payment_milestone_conditionvi { get; set; }

    public string? contract_payment_milestone_conditionen { get; set; }

    public long contract_payment_milestone_displayorder { get; set; }

    public DateTime created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public byte[] rowversion { get; set; } = null!;
}
