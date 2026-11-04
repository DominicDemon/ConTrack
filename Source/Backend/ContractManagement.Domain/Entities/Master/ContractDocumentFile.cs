using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractDocumentFile
{
    public long ContractDocumentFileId { get; set; }

    public long ContractDocumentId { get; set; }

    public long? ContractVersionId { get; set; }

    public long FileStorageFileid { get; set; }

    public int ContractDocumentFileRole { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime CreatedDate { get; set; }

    public long? CreatedEmployeeId { get; set; }
}
