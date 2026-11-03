using ContractManagement.Domain.Internal;
namespace ContractManagement.Application.Features.Internal;

public sealed record TemplateFileInput(string FileName, string Base64);
public sealed record TemplateImportInput(string FileName, string Base64, string Name, string Content);
public sealed record TemplateExtraction(string Name, string Content, string? Warning);
public interface IInternalTemplateImport
{
    TemplateExtraction Extract(TemplateFileInput input);
    Task<TemplateDefinition> Save(TemplateImportInput input, long actor, CancellationToken ct);
}
