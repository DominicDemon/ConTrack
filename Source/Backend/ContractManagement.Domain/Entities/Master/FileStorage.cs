using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class FileStorage
{
    public long FileStorageFileid { get; set; }

    public string? FileStorageTenantcode { get; set; }

    public long? FileStorageObjectid { get; set; }

    public string FileStorageObjecttype { get; set; } = null!;

    public string FileStorageFiletype { get; set; } = null!;

    public string FileStorageFilename { get; set; } = null!;

    public string FileStorageFilepath { get; set; } = null!;

    public string FileStorageStoragekey { get; set; } = null!;

    public string FileStorageContenttype { get; set; } = null!;

    public long FileStorageFilesize { get; set; }

    public string FileStorageSha256 { get; set; } = null!;

    public long? FileStorageUploadedbyemployeeid { get; set; }

    public long? FileStorageUploadedbycustomerid { get; set; }

    public DateTime FileStorageUploadeddate { get; set; }
}
