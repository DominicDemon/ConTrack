using ContractManagement.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ContractManagement.Infrastructure.DesignTime;

public sealed class MasterDbContextFactory : IDesignTimeDbContextFactory<MasterDbContext>
{
    public MasterDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfiguration.Build();
        var connectionString = configuration.GetConnectionString("MasterDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:MasterDatabase is not configured.");
        }
        return new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>()
            .UseSqlServer(connectionString).Options);
    }
}
