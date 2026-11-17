namespace ContractManagement.Domain.Entities.Master;

public sealed class ContractChecklist
{
    public long Id { get; set; }
    public long TemplateId { get; set; }
    public long? VersionId { get; set; }
    public string Rules { get; set; } = "[]";
    public bool Configured { get; set; }
    public string? ContentHash { get; set; }
    public string Resolutions { get; set; } = "{}";
    public byte[] RowVersion { get; set; } = null!;
}
