using ContractManagement.Domain.Common;

namespace ContractManagement.Domain.Events;

public sealed record ContractCreatedDomainEvent(
    int ContractId,
    int CustomerId,
    int EmployeeId,
    DateTime OccurredOnUtc) : IDomainEvent;

public sealed record ContractStatusChangedDomainEvent(
    int ContractId,
    byte PreviousStatus,
    byte NewStatus,
    DateTime OccurredOnUtc) : IDomainEvent;

