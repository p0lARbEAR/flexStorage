using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Domain.DomainServices;

/// <summary>
/// Interface for storage providers (S3, Backblaze, etc.).
/// Implements the Strategy pattern - providers are interchangeable.
/// </summary>
public interface IStorageProvider
{
    /// <summary>
    /// Gets the unique name of this storage provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the capabilities of this provider.
    /// </summary>
    ProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Uploads a file to the storage provider.
    /// </summary>
    /// <param name="fileStream">The file content stream</param>
    /// <param name="options">Upload options (filename, content type, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload result with storage location</returns>
    Task<UploadResult> UploadAsync(
        Stream fileStream,
        UploadOptions options,
        CancellationToken cancellationToken);

    /// <summary>
    /// Downloads a file from the storage provider.
    /// </summary>
    /// <param name="location">The storage location</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content stream</returns>
    Task<Stream> DownloadAsync(
        StorageLocation location,
        CancellationToken cancellationToken);

    /// <summary>
    /// Initiates retrieval of a file from cold storage (e.g., Glacier).
    /// </summary>
    /// <param name="location">The storage location</param>
    /// <param name="tier">Retrieval tier (speed vs cost)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Retrieval result with job ID and estimated time</returns>
    Task<RetrievalResult> InitiateRetrievalAsync(
        StorageLocation location,
        RetrievalTier tier,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the status of a retrieval request.
    /// </summary>
    /// <param name="retrievalId">The retrieval job ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current retrieval status</returns>
    Task<RetrievalStatus> GetRetrievalStatusAsync(
        string retrievalId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    /// <param name="location">The storage location</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteAsync(
        StorageLocation location,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks the health of this storage provider.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health status</returns>
    Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Capabilities of a storage provider.
/// </summary>
public class ProviderCapabilities
{
    /// <summary>
    /// Whether files can be accessed instantly (true) or require retrieval (false).
    /// </summary>
    public bool SupportsInstantAccess { get; init; }

    /// <summary>
    /// Whether this provider supports retrieval requests (Glacier-style).
    /// </summary>
    public bool SupportsRetrieval { get; init; }

    /// <summary>
    /// Whether files can be deleted.
    /// </summary>
    public bool SupportsDeletion { get; init; }

    /// <summary>
    /// Minimum retrieval time (for cold storage).
    /// </summary>
    public TimeSpan MinRetrievalTime { get; init; }

    /// <summary>
    /// Maximum retrieval time (for cold storage).
    /// </summary>
    public TimeSpan MaxRetrievalTime { get; init; }
}

/// <summary>
/// Options for uploading a file.
/// </summary>
public class UploadOptions
{
    /// <summary>
    /// The filename (optional, will be generated if not provided).
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// The content type / MIME type.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Additional metadata to store with the file.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Result of an upload operation.
/// </summary>
public class UploadResult
{
    /// <summary>
    /// Where the file was stored.
    /// </summary>
    public required StorageLocation Location { get; init; }

    /// <summary>
    /// When the upload completed.
    /// </summary>
    public DateTime UploadedAt { get; init; }
}

/// <summary>
/// Retrieval tier for cold storage (speed vs cost tradeoff).
/// </summary>
public enum RetrievalTier
{
    /// <summary>
    /// Bulk retrieval: 5-12 hours, cheapest.
    /// </summary>
    Bulk,

    /// <summary>
    /// Standard retrieval: 3-5 hours, moderate cost.
    /// </summary>
    Standard,

    /// <summary>
    /// Expedited retrieval: 1-5 minutes, expensive (if available).
    /// </summary>
    Expedited
}

/// <summary>
/// Result of initiating a retrieval request.
/// </summary>
public class RetrievalResult
{
    /// <summary>
    /// The unique ID of this retrieval job.
    /// </summary>
    public required string RetrievalId { get; init; }

    /// <summary>
    /// Estimated time until retrieval completes.
    /// </summary>
    public TimeSpan EstimatedCompletionTime { get; init; }

    /// <summary>
    /// Current status of the retrieval.
    /// </summary>
    public RetrievalStatus Status { get; init; }
}

/// <summary>
/// Status of a retrieval request.
/// </summary>
public enum RetrievalStatus
{
    /// <summary>
    /// Retrieval has been requested but not started.
    /// </summary>
    Requested,

    /// <summary>
    /// Retrieval is in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// File is ready for download.
    /// </summary>
    Ready,

    /// <summary>
    /// Retrieval request has expired.
    /// </summary>
    Expired,

    /// <summary>
    /// Retrieval failed.
    /// </summary>
    Failed
}

/// <summary>
/// Health status of a storage provider.
/// </summary>
public class HealthStatus
{
    /// <summary>
    /// Whether the provider is healthy.
    /// </summary>
    public bool IsHealthy { get; init; }

    /// <summary>
    /// Response time of the health check.
    /// </summary>
    public TimeSpan ResponseTime { get; init; }

    /// <summary>
    /// Additional health check details.
    /// </summary>
    public string? Message { get; init; }
}
