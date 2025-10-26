using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace FlexStorage.Infrastructure.Services;

/// <summary>
/// Implementation of storage service that coordinates with storage providers.
/// </summary>
public class StorageService : IStorageService
{
    private readonly StorageProviderSelector _providerSelector;
    private readonly IServiceProvider _serviceProvider;

    public StorageService(StorageProviderSelector providerSelector, IServiceProvider serviceProvider)
    {
        _providerSelector = providerSelector;
        _serviceProvider = serviceProvider;
    }

    public async Task<UploadResult> UploadAsync(
        Stream fileStream,
        UploadOptions options,
        CancellationToken cancellationToken = default)
    {
        // Use preferred provider if specified, otherwise use default
        var provider = !string.IsNullOrEmpty(options.PreferredProvider) 
            ? GetProviderByName(options.PreferredProvider)
            : _serviceProvider.GetRequiredService<IStorageProvider>();
        
        return await provider.UploadAsync(fileStream, options, cancellationToken);
    }

    public async Task<Stream> DownloadAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProviderByName(location.ProviderName);
        return await provider.DownloadAsync(location, cancellationToken);
    }

    public async Task<RetrievalResult> InitiateRetrievalAsync(
        StorageLocation location,
        RetrievalTier tier,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProviderByName(location.ProviderName);
        return await provider.InitiateRetrievalAsync(location, tier, cancellationToken);
    }

    public async Task<RetrievalStatusDetail> GetRetrievalStatusAsync(
        string retrievalId,
        CancellationToken cancellationToken = default)
    {
        // For now, we'll need to determine which provider to use based on the retrieval ID
        // This is a simplified implementation
        var providers = new[]
        {
            _serviceProvider.GetKeyedService<IStorageProvider>("s3-glacier-deep"),
            _serviceProvider.GetKeyedService<IStorageProvider>("s3-glacier-flexible")
        };

        foreach (var provider in providers.Where(p => p != null))
        {
            try
            {
                var status = await provider!.GetRetrievalStatusAsync(retrievalId, cancellationToken);
                if (status != null)
                    return status;
            }
            catch
            {
                // Continue to next provider
            }
        }

        return new RetrievalStatusDetail
        {
            RetrievalId = retrievalId,
            Status = RetrievalStatus.Failed,
            ErrorMessage = "Retrieval ID not found"
        };
    }

    public async Task<bool> DeleteAsync(
        StorageLocation location,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProviderByName(location.ProviderName);
        return await provider.DeleteAsync(location, cancellationToken);
    }

    private IStorageProvider GetProviderByName(string providerName)
    {
        return providerName switch
        {
            "s3-glacier-deep" => _serviceProvider.GetKeyedService<IStorageProvider>("s3-glacier-deep")!,
            "s3-glacier-flexible" => _serviceProvider.GetKeyedService<IStorageProvider>("s3-glacier-flexible")!,
            _ => _serviceProvider.GetRequiredService<IStorageProvider>() // Default provider
        };
    }
}

