using Contrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Contrack.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var masterConnection = configuration.GetConnectionString("MasterDb")
            ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:MasterDb'.");

        services.AddDbContext<MasterDbContext>(options => options.UseNpgsql(masterConnection));

        return services;
    }
}
