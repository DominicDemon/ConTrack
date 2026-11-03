namespace ContractManagement.Application.Common.Interfaces;

public interface ICentralDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

