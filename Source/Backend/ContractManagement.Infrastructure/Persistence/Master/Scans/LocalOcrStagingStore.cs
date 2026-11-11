using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal.Scans;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

public sealed class OcrStagingOptions
{
    public const string SectionName = "OcrStaging";

    /// <summary>Folder on the API machine; a relative path is taken from the API's content root.</summary>
    public string RootPath { get; set; } = "../private-storage/ocr-staging";
}

/// <summary>Plain copies of scans waiting for OCR, one file per FILE_STORAGE id, deleted once read.</summary>
public sealed class LocalOcrStagingStore(IOptions<OcrStagingOptions> options, IHostEnvironment environment) : IOcrStagingStore
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath, environment.ContentRootPath);

    public async Task SaveAsync(long fileId, byte[] content, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllBytesAsync(PathOf(fileId), content, cancellationToken);
    }

    public async Task<byte[]?> ReadAsync(long fileId, CancellationToken cancellationToken = default)
    {
        var path = PathOf(fileId);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
    }

    public Task DeleteAsync(long fileId, CancellationToken cancellationToken = default)
    {
        var path = PathOf(fileId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string PathOf(long fileId) => Path.Combine(_root, $"{fileId}.scan");
}
