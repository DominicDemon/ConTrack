using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractAccessLink
{
    public long ContractAccessLinkId { get; set; }

    public long? TenantId { get; set; }

    public long ContractDocumentId { get; set; }

    public long ContractVersionId { get; set; }

    public long ContractAccessLinkVerificationPhoneId { get; set; }

    public string ContractAccessLinkTokenhash { get; set; } = null!;

    public string? ContractAccessLinkRevocationReason { get; set; }

    public long CreatedByEmployeeId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public long? RevokedByEmployeeId { get; set; }

    public byte[] Rowversion { get; set; } = null!;
}
