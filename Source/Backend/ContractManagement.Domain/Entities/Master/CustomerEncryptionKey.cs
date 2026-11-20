namespace ContractManagement.Domain.Entities.Master;

/// <summary>A customer's own key for opening their contract scans (017-customer-file-keys.sql).</summary>
public partial class CustomerEncryptionKey
{
    public long CustomerEncryptionKeyId { get; set; }

    public long CustomerId { get; set; }

    public byte[] CustomerEncryptionKeyPublickey { get; set; } = null!;

    public byte[] CustomerEncryptionKeyPrivatekey { get; set; } = null!;

    public byte[] CustomerEncryptionKeySalt { get; set; } = null!;

    public int CustomerEncryptionKeyIterations { get; set; }

    public string CustomerEncryptionKeyStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? RevokedAt { get; set; }
}
