using ContractManagement.Domain.Internal.Scans;
namespace ContractManagement.Application.Features.Internal.Scans;

/// <summary>Keeps uploaded files outside the database; <c>FILE_STORAGE</c> holds only their storage key.</summary>
public interface IFileStore
{
    Task SaveAsync(string storageKey, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>Null when no file is stored under the key.</summary>
    Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>Removes a file whose database rows were never saved.</summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
