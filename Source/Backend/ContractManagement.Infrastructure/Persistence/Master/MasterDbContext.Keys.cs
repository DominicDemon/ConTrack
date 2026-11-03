using ContractManagement.Domain.Entities.Master;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

public partial class MasterDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // DDL defines a NOT NULL unique pair, but no physical primary key.
        // Use that existing uniqueness for EF tracking without changing the database.
        modelBuilder.Entity<EmployeeDepartment>()
            .HasKey(x => new { x.EmployeeId, x.DepartmentId });
    }
}
