using ContractManagement.Domain.Internal.Scans;
namespace ContractManagement.Application.Features.Internal.Scans;

/// <summary>
/// The plain copy of a just-uploaded scan, kept only until OCR has read it: the stored copy is encrypted and the
/// background reader holds no employee's key. Kept on the API machine, never in the file store.
/// </summary>
public interface IOcrStagingStore
{
    Task SaveAsync(long fileId, byte[] content, CancellationToken cancellationToken = default);

    Task<byte[]?> ReadAsync(long fileId, CancellationToken cancellationToken = default);

    Task DeleteAsync(long fileId, CancellationToken cancellationToken = default);
}
