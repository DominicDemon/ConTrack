using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CONTRACT_DOCUMENT_FILE
{
    public long contract_document_file_id { get; set; }

    public long? contract_id { get; set; }

    public long contract_document_id { get; set; }

    public long? contract_version_id { get; set; }

    public long file_storage_fileid { get; set; }

    public int contract_document_file_role { get; set; }

    public bool is_primary { get; set; }

    public DateTime created_date { get; set; }

    public long? created_employee_id { get; set; }
}
