using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class FileEncryptionKey
{
    public long FileEncryptionKeyId { get; set; }

    public long FileStorageFileid { get; set; }

    public long? EmployeeEncryptionKeyId { get; set; }

    public byte[] FileEncryptionKeyWrappedkey { get; set; } = null!;

    public string? FileEncryptionKeyRecoverykeyid { get; set; }

    public long? GrantedByEmployeeId { get; set; }

    public DateTime GrantedAt { get; set; }
}
