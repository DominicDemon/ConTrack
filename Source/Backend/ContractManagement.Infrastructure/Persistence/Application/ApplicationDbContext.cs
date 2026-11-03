using ContractManagement.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Application;

public partial class DbDtctechContext : IApplicationDbContext
{
}

public sealed class ApplicationDbContext(DbContextOptions<DbDtctechContext> options)
    : DbDtctechContext(options)
{
}

