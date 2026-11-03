using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractNegotiationComment
{
    public long ContractNegotiationCommentId { get; set; }

    public long ContractDocumentId { get; set; }

    public long ContractVersionId { get; set; }

    public int? ContractNegotiationCommentTermid { get; set; }

    public int? ContractNegotiationCommentParentcommentid { get; set; }

    public int? ContractNegotiationCommentCarriedforwardfromcommentid { get; set; }

    public int? ContractNegotiationCommentCarriedforwardfromversionid { get; set; }

    public string ContractNegotiationCommentSource { get; set; } = null!;

    public string ContractNegotiationCommentContent { get; set; } = null!;

    public long? RecordedByEmployeeId { get; set; }

    public long? CustomerAccessSessionId { get; set; }

    public int ContractNegotiationCommentState { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] Rowversion { get; set; } = null!;
}
