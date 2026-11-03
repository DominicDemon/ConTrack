using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractPhoneVerify
{
    public long ContractPhoneVerifyId { get; set; }

    public long ContractDocumentId { get; set; }

    public string ContractPhoneVerifyPhoneSource { get; set; } = null!;

    public string ContractPhoneVerifyPhoneNumberNormalized { get; set; } = null!;

    public string ContractPhoneVerifyReason { get; set; } = null!;

    public long CreatedByEmployeeid { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] Rowversion { get; set; } = null!;
}
