using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractSignedEvidence
{
    public long ContractSignedEvidenceId { get; set; }

    public long ContractDocumentFileId { get; set; }

    public int ContractSignedEvidenceStatus { get; set; }

    public DateOnly ContractSignedEvidenceSigndate { get; set; }

    public long UploadedByEmployeeid { get; set; }

    public DateTime UploadedAt { get; set; }

    public long? SupersededByEmployeeId { get; set; }

    public DateTime? SupersededAt { get; set; }

    public long? SupersedesEvidenceId { get; set; }

    public string? ContractSignedEvidenceSupersedereason { get; set; }

    public byte[] Rowversion { get; set; } = null!;
}
