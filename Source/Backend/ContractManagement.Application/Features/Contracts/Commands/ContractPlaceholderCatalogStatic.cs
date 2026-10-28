using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Infrastructure.Persistence.Application.Models;

namespace ContractManagement.Domains.Services.ContractTemplate;

public static class ContractPlaceholderCatalogDefaults
{
    public static IReadOnlyList<SoftwareSupplyPlaceholderDefinition> SystemDefinitions =>
        SoftwareSupplyPlaceholderCatalog.All.Select(x => x with { SourceFieldKey = "system." + x.Key }).ToArray();

    public static SoftwareSupplyPlaceholderDefinition FromSnapshot(TblContractTemplateField f) =>
        new(f.PlaceholderKey, f.FieldLabel, f.IsRequired,
            (TemplatePlaceholderDataKind)f.DataKind,
            (TemplatePlaceholderMultiplicity)f.Multiplicity, f.DataSource)
        {
            IsSystem = f.IsSystem,
            SourceFieldKey = f.SourceFieldKey,
            DefaultValue = f.DefaultValue,
            FormatString = f.FormatString,
            RowVersion = f.DefinitionRowVersion is null
                ? null
                : Convert.ToBase64String(f.DefinitionRowVersion)
        };

    public static string Fingerprint(IEnumerable<SoftwareSupplyPlaceholderDefinition> definitions) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            definitions.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new
            {
                x.Key, x.SourceFieldKey, x.DefaultValue, x.FormatString, x.DataKind,
                x.Multiplicity, x.IsRequired, x.IsSystem, x.IsActive, x.RowVersion
            }))))).ToLowerInvariant();
}

