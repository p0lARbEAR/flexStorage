namespace FlexStorage.Domain.ValueObjects;

/// <summary>
/// Value object representing file size with validation and conversion capabilities.
/// </summary>
public sealed class FileSize : IEquatable<FileSize>, IComparable<FileSize>
{
    private const long MaxSizeBytes = 5L * 1024 * 1024 * 1024; // 5 GB

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public long Bytes { get; }

    private FileSize(long bytes)
    {
        if (bytes <= 0)
            throw new ArgumentException("File size must be greater than zero", nameof(bytes));

        if (bytes > MaxSizeBytes)
            throw new ArgumentException($"File size cannot exceed {MaxSizeBytes} bytes ({MaxSizeBytes / (1024.0 * 1024 * 1024):F2} GB)", nameof(bytes));

        Bytes = bytes;
    }

    /// <summary>
    /// Creates a FileSize from bytes.
    /// </summary>
    /// <param name="bytes">The number of bytes</param>
    /// <returns>A new FileSize instance</returns>
    public static FileSize FromBytes(long bytes) => new(bytes);

    /// <summary>
    /// Converts the file size to kilobytes.
    /// </summary>
    /// <returns>File size in KB</returns>
    public double ToKilobytes() => Bytes / 1024.0;

    /// <summary>
    /// Converts the file size to megabytes.
    /// </summary>
    /// <returns>File size in MB</returns>
    public double ToMegabytes() => Bytes / (1024.0 * 1024.0);

    /// <summary>
    /// Converts the file size to gigabytes.
    /// </summary>
    /// <returns>File size in GB</returns>
    public double ToGigabytes() => Bytes / (1024.0 * 1024.0 * 1024.0);

    /// <summary>
    /// Returns a human-readable representation of the file size.
    /// </summary>
    /// <returns>Human-readable file size (e.g., "1.50 MB")</returns>
    public string ToHumanReadable()
    {
        return Bytes switch
        {
            < 1024 => $"{Bytes} B",
            < 1024 * 1024 => $"{Bytes / 1024.0:F2} KB",
            < 1024 * 1024 * 1024 => $"{Bytes / (1024.0 * 1024.0):F2} MB",
            _ => $"{Bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
        };
    }

    #region Equality

    public bool Equals(FileSize? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Bytes == other.Bytes;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is FileSize other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Bytes.GetHashCode();
    }

    public static bool operator ==(FileSize? left, FileSize? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(FileSize? left, FileSize? right)
    {
        return !Equals(left, right);
    }

    #endregion

    #region Comparison

    public int CompareTo(FileSize? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (ReferenceEquals(null, other)) return 1;
        return Bytes.CompareTo(other.Bytes);
    }

    public static bool operator <(FileSize? left, FileSize? right)
    {
        return Comparer<FileSize>.Default.Compare(left, right) < 0;
    }

    public static bool operator >(FileSize? left, FileSize? right)
    {
        return Comparer<FileSize>.Default.Compare(left, right) > 0;
    }

    public static bool operator <=(FileSize? left, FileSize? right)
    {
        return Comparer<FileSize>.Default.Compare(left, right) <= 0;
    }

    public static bool operator >=(FileSize? left, FileSize? right)
    {
        return Comparer<FileSize>.Default.Compare(left, right) >= 0;
    }

    #endregion

    public override string ToString() => ToHumanReadable();
}
