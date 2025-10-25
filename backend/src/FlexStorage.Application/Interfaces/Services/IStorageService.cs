using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Application.Interfaces.Services;

/// <summary>
/// Application service for storage operations.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Uploads a file to the appropriate storage provider.
    /// </summary>
    Task<UploadResult> UploadAsync(
        Stream fileStream,
        UploadOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file from storage.
    /// </summary>
    Task<Stream> DownloadAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates retrieval from cold storage (Glacier).
    /// </summary>
    Task<RetrievalResult> InitiateRetrievalAsync(
        StorageLocation location,
        RetrievalTier tier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the status of a retrieval request.
    /// </summary>
    Task<RetrievalStatus> GetRetrievalStatusAsync(
        string retrievalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    Task<bool> DeleteAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default);
}
