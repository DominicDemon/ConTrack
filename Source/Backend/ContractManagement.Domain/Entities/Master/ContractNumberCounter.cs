namespace ContractManagement.Domain.Entities.Master;

public sealed class ContractNumberCounter
{
    public int ContractYear { get; set; }
    public long LastNumber { get; set; }
}
