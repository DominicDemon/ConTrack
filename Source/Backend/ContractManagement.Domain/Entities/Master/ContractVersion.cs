using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractVersion
{
    public long ContractVersionId { get; set; }

    public long ContractDocumentId { get; set; }

    public long ContractVersionVersionNo { get; set; }

    public long ContractTemplateId { get; set; }

    public string? ContractVersionChangenote { get; set; }

    public decimal ContractVersionSubtotal { get; set; }

    public decimal ContractVersionTotaldiscount { get; set; }

    public decimal ContractVersionTotalvat { get; set; }

    public decimal ContractVersionTotalamount { get; set; }

    public string ContractVersionCurrencycode { get; set; } = null!;

    public bool ContractVersionIslocked { get; set; }

    public DateTime? ContractVersionLockeddate { get; set; }

    public int? ContractVersionLockedbyemployeeid { get; set; }

    public string? ContractVersionSnapshothash { get; set; }

    public DateTime? ContractVersionSigndate { get; set; }

    public DateTime? ContractVersionEffectiveDate { get; set; }

    public DateTime? ContractVersionExpireDate { get; set; }

    public DateTime ContractVersionCreatedDate { get; set; }

    public byte[] ContractVersionRowversion { get; set; } = null!;
}
