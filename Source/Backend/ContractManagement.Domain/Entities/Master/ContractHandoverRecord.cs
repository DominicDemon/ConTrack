using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractHandoverRecord
{
    public long ContractHandoverRecordId { get; set; }

    public long ContractDocumentId { get; set; }

    public long ContractVersionId { get; set; }

    public long ContractTemplateId { get; set; }

    public string ContractHandoverRecordHandovercode { get; set; } = null!;

    public DateOnly ContractHandoverRecordHandoverAt { get; set; }

    public DateOnly ContractHandoverRecordContractsigndate { get; set; }

    public string ContractHandoverRecordLocation { get; set; } = null!;

    public string? ContractHandoverRecordLocationen { get; set; }

    public int ContractHandoverRecordStatus { get; set; }

    public string? ContractHandoverRecordSnapshothash { get; set; }

    public long CreatedEmployeeId { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? UpdatedEmployeeId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? FinalizedByEmployeeId { get; set; }

    public DateTime? FinalizedAt { get; set; }

    public long? SignedByEmployeeId { get; set; }

    public DateTime? SignedAt { get; set; }

    public long? CancelledByEmployeeId { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? ContractHandoverRecordCancellationreason { get; set; }

    public byte[] Rowversion { get; set; } = null!;
}
