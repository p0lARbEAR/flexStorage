namespace FlexStorage.Domain.ValueObjects;

/// <summary>
/// Value object representing where a file is stored.
/// </summary>
public sealed class StorageLocation : IEquatable<StorageLocation>
{
    /// <summary>
    /// Gets the name of the storage provider.
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    /// Gets the path/URI to the file in the storage provider.
    /// </summary>
    public string Path { get; }

    private StorageLocation(string providerName, string path)
    {
        ProviderName = providerName;
        Path = path;
    }

    /// <summary>
    /// Creates a new StorageLocation.
    /// </summary>
    /// <param name="providerName">The name of the storage provider</param>
    /// <param name="path">The path/URI to the file</param>
    /// <returns>A new StorageLocation instance</returns>
    /// <exception cref="ArgumentException">Thrown when provider name or path is invalid</exception>
    public static StorageLocation Create(string providerName, string path)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));

        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Storage path cannot be null or empty", nameof(path));

        return new StorageLocation(providerName.Trim(), path.Trim());
    }

    #region Equality

    public bool Equals(StorageLocation? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return ProviderName == other.ProviderName && Path == other.Path;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is StorageLocation other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ProviderName, Path);
    }

    public static bool operator ==(StorageLocation? left, StorageLocation? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(StorageLocation? left, StorageLocation? right)
    {
        return !Equals(left, right);
    }

    #endregion

    public override string ToString() => $"{ProviderName}: {Path}";
}
