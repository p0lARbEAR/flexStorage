using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.TestPlugin;

/// <summary>
/// Test storage provider for PluginLoader unit tests.
/// This is a minimal implementation for testing plugin loading functionality.
/// </summary>
public class TestStorageProvider : IStorageProvider
{
    public string ProviderName => "test-storage";

    public ProviderCapabilities Capabilities => new()
    {
        SupportsInstantAccess = true,
        SupportsRetrieval = false,
        SupportsDeletion = true,
        SupportsDeepArchive = false,
        SupportsFlexibleRetrieval = false,
        MinRetrievalTime = TimeSpan.Zero,
        MaxRetrievalTime = TimeSpan.Zero
    };

    public Task<UploadResult> UploadAsync(
        Stream fileStream,
        UploadOptions options,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new UploadResult
        {
            Success = true,
            Location = StorageLocation.Create("test-storage", "test://test-file")
        });
    }

    public Task<Stream> DownloadAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Stream>(new MemoryStream());
    }

    public Task<RetrievalResult> InitiateRetrievalAsync(
        StorageLocation location,
        RetrievalTier tier,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new RetrievalResult
        {
            Success = false,
            Status = RetrievalStatus.Failed,
            ErrorMessage = "Test provider does not support retrieval"
        });
    }

    public Task<RetrievalStatusDetail> GetRetrievalStatusAsync(
        string retrievalId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new RetrievalStatusDetail
        {
            RetrievalId = retrievalId,
            Status = RetrievalStatus.Failed,
            ProgressPercentage = 0,
            ErrorMessage = "Test provider does not support retrieval"
        });
    }

    public Task<bool> DeleteAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new HealthStatus
        {
            IsHealthy = true,
            ResponseTime = TimeSpan.FromMilliseconds(1),
            Message = "Test provider is healthy"
        });
    }
}
