using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractPaymentMilestone
{
    public long ContractPaymentMilestoneId { get; set; }

    public long ContractDocumentId { get; set; }

    public long ContractVersionId { get; set; }

    public long ContractTermId { get; set; }

    public long? InvoiceId { get; set; }

    public string ContractPaymentMilestoneCode { get; set; } = null!;

    public string ContractPaymentMilestoneTitlevi { get; set; } = null!;

    public string? ContractPaymentMilestoneTitleen { get; set; }

    public decimal ContractPaymentMilestonePaymentpercent { get; set; }

    public decimal ContractPaymentMilestoneAmount { get; set; }

    public int ContractPaymentMilestoneDueoffsetdays { get; set; }

    public int ContractPaymentMilestoneDueanchor { get; set; }

    public int ContractPaymentMilestoneDaycountmode { get; set; }

    public DateOnly? ContractPaymentMilestoneAnchordate { get; set; }

    public DateOnly? ContractPaymentMilestoneDuedate { get; set; }

    public int ContractPaymentMilestonePaymentstatus { get; set; }

    public DateTime? ContractPaymentMilestonePaidat { get; set; }

    public int? ContractPaymentMilestonePaidbyemployeeid { get; set; }

    public string? ContractPaymentMilestoneConditionvi { get; set; }

    public string? ContractPaymentMilestoneConditionen { get; set; }

    public long ContractPaymentMilestoneDisplayorder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] Rowversion { get; set; } = null!;
}
