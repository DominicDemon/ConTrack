namespace ContractManagement.Domain.Entities.Master;

/// <summary>A keyword OCR read from a customer's vault document (022), kept apart from staff scan keywords.</summary>
public partial class CustomerVaultKeyword
{
    public long CustomerVaultKeywordId { get; set; }

    public long CustomerVaultFileId { get; set; }

    public int CustomerVaultKeywordType { get; set; }

    public string CustomerVaultKeywordValue { get; set; } = null!;

    /// <summary>1 read by OCR, 2 typed by the customer (kept when OCR reads again).</summary>
    public int CustomerVaultKeywordSource { get; set; }
}
