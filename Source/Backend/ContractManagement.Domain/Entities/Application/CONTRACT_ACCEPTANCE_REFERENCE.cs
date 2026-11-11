namespace ContractManagement.Infrastructure.Persistence.Application.Models;
public sealed class CONTRACT_ACCEPTANCE_REFERENCE
{
    public int AcceptanceReferenceId { get; set; }
    public int AcceptanceRecordId { get; set; }
    public byte ReferenceType { get; set; }
    public int? AppendixId { get; set; }
    public int DisplayOrder { get; set; }
}


