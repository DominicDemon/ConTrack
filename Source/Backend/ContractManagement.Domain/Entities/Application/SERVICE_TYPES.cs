using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.Application.Models;

public partial class SERVICE_TYPES
{
    public byte ServiceTypeId { get; set; }

    public string? ServiceTypeName { get; set; }

    public byte? LangId { get; set; }
}

