namespace ContractManagement.Domain.Entities.Master;

/// <summary>A document a customer keeps in their own vault (022-customer-vault.sql). Staff never see it.</summary>
public partial class CustomerVaultFile
{
    public long CustomerVaultFileId { get; set; }

    public long CustomerId { get; set; }

    public long FileStorageFileid { get; set; }

    public string CustomerVaultFileTitle { get; set; } = null!;

    public string? CustomerVaultFileNote { get; set; }

    /// <summary>QUEUED, PROCESSING, SUCCESS, FAILED, INTERRUPTED.</summary>
    public string CustomerVaultFileOcrStatus { get; set; } = null!;

    public int? CustomerVaultFilePageCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    /// <summary>Folder or kind of document the customer chose (023).</summary>
    public string? CustomerVaultFileCategory { get; set; }

    /// <summary>When the customer's contract ends; reminders go out before it (023).</summary>
    public DateOnly? CustomerVaultFileExpiresOn { get; set; }

    /// <summary>Days before the end to start reminding; 30 when not set.</summary>
    public int? CustomerVaultFileRemindDays { get; set; }

    /// <summary>The last reminder sent, as days left then; null when none was sent for the current end date.</summary>
    public int? CustomerVaultFileRemindedDays { get; set; }

    /// <summary>In the bin since then; removed for good 30 days later.</summary>
    public DateTime? TrashedAt { get; set; }
}
