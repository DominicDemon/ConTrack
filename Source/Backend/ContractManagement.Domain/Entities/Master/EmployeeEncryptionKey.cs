using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class EmployeeEncryptionKey
{
    public long EmployeeEncryptionKeyId { get; set; }

    public long EmployeeId { get; set; }

    public byte[] EmployeeEncryptionKeyPublickey { get; set; } = null!;

    public byte[] EmployeeEncryptionKeyPrivatekey { get; set; } = null!;

    public byte[] EmployeeEncryptionKeySalt { get; set; } = null!;

    public int EmployeeEncryptionKeyIterations { get; set; }

    public string EmployeeEncryptionKeyStatus { get; set; } = null!;

    public long IssuedByEmployeeId { get; set; }

    public DateTime IssuedAt { get; set; }

    public DateTime? RevokedAt { get; set; }
}
