using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractDocument
{
    public long ContractDocumentId { get; set; }

    public long? ContractDocumentCurrentVersionId { get; set; }

    public string? TenantId { get; set; }

    public long CustomerId { get; set; }

    public long EmployeeId { get; set; }

    public long ContractTemplateId { get; set; }

    public string? ContractDocumentCode { get; set; }

    public string ContractDocumentName { get; set; } = null!;

    public int ContractDocumentType { get; set; }

    public int ContractDocumentStatus { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public byte[] RowVersion { get; set; } = null!;
}
