using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractTerm
{
    public long ContractTermTermid { get; set; }

    public long ContractDocumentId { get; set; }

    public long ContractVersionId { get; set; }

    public long? ContractTermSourcetemplatetermid { get; set; }

    public string ContractTermTermcode { get; set; } = null!;

    public string ContractTermTermtitle { get; set; } = null!;

    public string? ContractTermTermtitleen { get; set; }

    public string? ContractTermTermcontent { get; set; }

    public string? ContractTermTermcontenten { get; set; }

    public bool ContractTermIsnegotiable { get; set; }

    public int ContractTermTermkind { get; set; }

    public int ContractTermBilingualcontentmode { get; set; }

    public long ContractTermDisplayorder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] Rowversion { get; set; } = null!;
}
