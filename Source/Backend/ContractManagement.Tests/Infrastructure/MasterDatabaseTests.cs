using ContractManagement.Domain.Entities.Master;
using ContractManagement.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ContractManagement.Tests.Infrastructure;

public class MasterDatabaseTests
{
    [Fact]
    public void ModelMapsAllMasterTablesAndColumnsWithoutInventingRelationships()
    {
        using var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=True").Options);
        var entities = db.Model.GetEntityTypes().ToList();
        Assert.Equal(60, entities.Count);
        Assert.Equal(659, entities.Sum(entity => entity.GetProperties().Count()));
        Assert.All(entities, entity => Assert.NotNull(entity.FindPrimaryKey()));
        Assert.Empty(entities.SelectMany(entity => entity.GetForeignKeys()));
        Assert.Equal(new[] { "EmployeeId", "DepartmentId" },
            db.Model.FindEntityType(typeof(EmployeeDepartment))!.FindPrimaryKey()!
                .Properties.Select(property => property.Name));
    }

    [Fact]
    public async Task MissingConfigurationDoesNotSilentlyUseLegacyDatabase()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMasterDatabase(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.Null(scope.ServiceProvider.GetService<MasterDbContext>());
        var report = await scope.ServiceProvider.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(registration => registration.Tags.Contains("master"));
        Assert.Empty(report.Entries);
    }

    [MasterDatabaseFact]
    public async Task LiveSchemaMatchesModelAndWritesCanBeRolledBack()
    {
        using var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable("CONTRACK_MASTER_TEST_CONNECTION")).Options);
        await db.Database.OpenConnectionAsync();
        var connection = db.Database.GetDbConnection();
        var columns = new HashSet<string>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT t.name, c.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id WHERE s.name='dbo' AND t.is_ms_shipped=0";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns.Add($"{reader.GetString(0)}.{reader.GetString(1)}");
        }
        var mapped = db.Model.GetEntityTypes().SelectMany(entity =>
        {
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            return entity.GetProperties().Select(property => $"{table.Name}.{property.GetColumnName(table)}");
        }).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(659, columns.Count);
        Assert.True(columns.SetEquals(mapped), "EF mappings must match all live table and column names.");

        // Every SELECT is compiled and executed by SQL Server, including column types.
        foreach (var entity in db.Model.GetEntityTypes())
        {
            using var command = connection.CreateCommand();
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            var names = entity.GetProperties().Select(property => $"[{property.GetColumnName(table)}]");
            command.CommandText = $"SELECT TOP (0) {string.Join(",", names)} FROM [dbo].[{table.Name}]";
            using var reader = await command.ExecuteReaderAsync();
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var code = "VERIFY_" + Guid.NewGuid().ToString("N")[..16];
        var department = new Department { DepartmentCode = code, DepartmentName = code };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        var stored = await db.Departments.AsNoTracking().SingleAsync(x => x.DepartmentId == department.DepartmentId);
        Assert.Equal("ACTIVE", stored.DepartmentStatus);
        Assert.True(stored.CreatedAt > DateTime.MinValue);
        await transaction.RollbackAsync();
        db.ChangeTracker.Clear();
        Assert.False(await db.Departments.AnyAsync(x => x.DepartmentCode == code));
    }
}

internal sealed class MasterDatabaseFactAttribute : FactAttribute
{
    public MasterDatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CONTRACK_MASTER_TEST_CONNECTION")))
            Skip = "Set CONTRACK_MASTER_TEST_CONNECTION to test an initialized MASTER database.";
    }
}
