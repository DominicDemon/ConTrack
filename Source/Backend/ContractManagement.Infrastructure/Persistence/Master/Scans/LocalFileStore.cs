using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal.Scans;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

public sealed class FileStoreOptions
{
    public const string SectionName = "ScanFileStore";

    /// <summary>Folder the files are kept in; a relative path is taken from the API's content root.</summary>
    public string RootPath { get; set; } = "../private-storage/internal-scans";
}

/// <summary>Keeps files in a folder on the API machine, at the path their storage key names.</summary>
public sealed class LocalFileStore(IOptions<FileStoreOptions> options, IHostEnvironment environment) : IFileStore
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath, environment.ContentRootPath);

    public async Task SaveAsync(string storageKey, byte[] content, CancellationToken cancellationToken = default)
    {
        var path = PathOf(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
    }

    public async Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = PathOf(storageKey);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = PathOf(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    // A key is made by the application, but it must still never point outside the root.
    private string PathOf(string storageKey)
    {
        var path = Path.GetFullPath(Path.Combine(_root, storageKey));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Storage key points outside the file store.");
        }

        return path;
    }
}
