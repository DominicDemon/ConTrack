namespace ContractManagement.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}

