using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Domain.DomainServices;

/// <summary>
/// Domain service that selects the appropriate storage provider based on file characteristics.
/// </summary>
public class StorageProviderSelector
{
    private readonly IReadOnlyList<IStorageProvider> _providers;

    public StorageProviderSelector(IEnumerable<IStorageProvider> providers)
    {
        _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
    }

    /// <summary>
    /// Selects the appropriate storage provider for a file.
    /// </summary>
    /// <param name="fileType">The type of file</param>
    /// <param name="fileSize">The size of the file</param>
    /// <param name="userPreference">Optional user-specified provider name</param>
    /// <returns>The selected storage provider</returns>
    /// <exception cref="InvalidOperationException">Thrown when no providers are available</exception>
    public IStorageProvider SelectProvider(
        FileType fileType,
        FileSize fileSize,
        string? userPreference = null)
    {
        if (!_providers.Any())
            throw new InvalidOperationException("No storage providers available");

        // If user specified a provider, try to use it
        if (!string.IsNullOrWhiteSpace(userPreference))
        {
            var preferredProvider = _providers.FirstOrDefault(
                p => p.ProviderName.Equals(userPreference, StringComparison.OrdinalIgnoreCase));

            if (preferredProvider != null)
                return preferredProvider;

            // User preference not found, log warning (in real implementation)
            // Fall through to automatic selection
        }

        // Automatic selection based on file characteristics
        return SelectBasedOnFileCharacteristics(fileType, fileSize);
    }

    private IStorageProvider SelectBasedOnFileCharacteristics(FileType fileType, FileSize fileSize)
    {
        // Strategy:
        // - Photos: S3 Glacier Deep Archive (cheapest, rarely accessed)
        // - Videos: S3 Glacier Flexible Retrieval (larger files, occasional access)
        // - Misc: S3 Glacier Flexible Retrieval (may need faster retrieval)

        var recommendedTier = fileType.GetStorageTierRecommendation();

        // Try to find provider matching the recommended tier
        var provider = recommendedTier switch
        {
            "glacier-deep-archive" => FindProviderByName("S3 Glacier Deep Archive"),
            "glacier-flexible-retrieval" => FindProviderByName("S3 Glacier Flexible Retrieval"),
            _ => null
        };

        // If no provider found for recommended tier, use first available provider
        return provider ?? _providers.First();
    }

    private IStorageProvider? FindProviderByName(string name)
    {
        return _providers.FirstOrDefault(
            p => p.ProviderName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
