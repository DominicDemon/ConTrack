using ContractManagement.Domains.Interfaces.ContractTemplate;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContractManagement.Domains.Services.ContractTemplate;

public sealed class ContractPlaceholderCatalog(
    DbDtctechContext db, IContractPlaceholderSourceRegistry registry,
    IOptions<CustomContractPlaceholderOptions>? options = null,
    ICurrentTenant? tenant = null) : IContractPlaceholderCatalog
{
    public bool CustomEnabled => options?.Value.Enabled == true &&
        (options.Value.TenantCodes.Length == 0 ||
         tenant?.Value is { } current && options.Value.TenantCodes.Contains(current.TenantCode, StringComparer.OrdinalIgnoreCase));

    public async Task<IReadOnlyList<SoftwareSupplyPlaceholderDefinition>> GetAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var result = SystemDefinitions.ToList();
        if (!CustomEnabled) return result;
        var custom = await db.TblContractPlaceholderDefinitions.AsNoTracking()
            .Where(x => includeInactive || x.IsActive).OrderBy(x => x.PlaceholderKey).ToListAsync(cancellationToken);
        var reserved = SystemDefinitions.Concat(AcceptanceRecordPlaceholderCatalog.All).Concat(HandoverRecordPlaceholderCatalog.All)
            .Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        var collision = custom.FirstOrDefault(x => reserved.Contains(x.PlaceholderKey));
        if (collision is not null)
            throw new PlaceholderOperationException("PlaceholderKeyCollision",
                $"Trường tùy chỉnh {collision.PlaceholderKey} trùng marker hệ thống. Hãy cấu hình mẫu bằng một khóa tùy chỉnh khác; hệ thống không tự đổi nguồn dữ liệu.");
        result.AddRange(custom.Select(ToDefinition));
        return result;
    }

    public SoftwareSupplyPlaceholderDefinition ToDefinition(TblContractPlaceholderDefinition item)
    {
        var field = registry.GetRequired(item.SourceFieldKey);
        return new(item.PlaceholderKey, item.FieldLabel, false, TemplatePlaceholderDataKind.Scalar,
            TemplatePlaceholderMultiplicity.ZeroOrOne, $"{field.ModuleLabel} / {field.FieldLabel}")
        {
            Id = item.PlaceholderDefinitionId, IsSystem = false, IsActive = item.IsActive,
            SourceFieldKey = item.SourceFieldKey, ModuleKey = field.ModuleKey, ValueType = field.ValueType,
            DefaultValue = item.DefaultValue, FormatString = item.FormatString,
            RowVersion = Convert.ToBase64String(item.RowVersion)
        };
    }

    public static IReadOnlyList<SoftwareSupplyPlaceholderDefinition> SystemDefinitions =>
        ContractPlaceholderCatalogDefaults.SystemDefinitions;

    public static SoftwareSupplyPlaceholderDefinition FromSnapshot(TblContractTemplateField f) =>
        ContractPlaceholderCatalogDefaults.FromSnapshot(f);

    public static string Fingerprint(IEnumerable<SoftwareSupplyPlaceholderDefinition> definitions) =>
        ContractPlaceholderCatalogDefaults.Fingerprint(definitions);
}
