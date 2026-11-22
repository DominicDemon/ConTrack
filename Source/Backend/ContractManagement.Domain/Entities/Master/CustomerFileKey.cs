namespace ContractManagement.Domain.Entities.Master;

/// <summary>The key of one scan, wrapped for one customer's key (017-customer-file-keys.sql).</summary>
public partial class CustomerFileKey
{
    public long CustomerFileKeyId { get; set; }

    public long FileStorageFileid { get; set; }

    public long CustomerEncryptionKeyId { get; set; }

    public byte[] CustomerFileKeyWrappedkey { get; set; } = null!;

    public DateTime GrantedAt { get; set; }
}
