using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class FILE_STORAGE
{
    public long file_storage_fileid { get; set; }

    public string? file_storage_tenantcode { get; set; }

    public long? file_storage_objectid { get; set; }

    public string file_storage_objecttype { get; set; } = null!;

    public string file_storage_filetype { get; set; } = null!;

    public string file_storage_filename { get; set; } = null!;

    public string file_storage_filepath { get; set; } = null!;

    public string file_storage_storagekey { get; set; } = null!;

    public string file_storage_contenttype { get; set; } = null!;

    public long file_storage_filesize { get; set; }

    public string file_storage_sha256 { get; set; } = null!;

    public long? file_storage_uploadedbyemployeeid { get; set; }

    public long? file_storage_uploadedbycustomerid { get; set; }

    public DateTime file_storage_uploadeddate { get; set; }
}
