using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractTemplate
{
    public long ContractTemplateId { get; set; }

    public string ContractTemplateCode { get; set; } = null!;

    public string ContractTemplateName { get; set; } = null!;

    public string? ContractTemplateNameen { get; set; }

    public string? ContractTemplateDescription { get; set; }

    public int ContractTemplateDocumentType { get; set; }

    public int ContractTemplateLanguageMode { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? CreatedEmployeeId { get; set; }

    public int? ModifiedEmployeeId { get; set; }
}
