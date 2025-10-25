using System.Text.RegularExpressions;

namespace FlexStorage.Domain.Entities;

/// <summary>
/// Entity representing metadata about a file.
/// </summary>
public class FileMetadata
{
    private static readonly Regex InvalidFileCharsRegex = new(@"[<>:""/\\|?*\x00-\x1F]", RegexOptions.Compiled);
    private readonly HashSet<string> _tags = new();

    /// <summary>
    /// Gets the original filename as provided by the user.
    /// </summary>
    public string OriginalFileName { get; private set; }

    /// <summary>
    /// Gets the sanitized filename safe for file systems.
    /// </summary>
    public string SanitizedFileName { get; private set; }

    /// <summary>
    /// Gets the SHA256 hash of the file content.
    /// </summary>
    public string Hash { get; private set; }

    /// <summary>
    /// Gets when the file was originally captured/created (from EXIF or user-provided).
    /// </summary>
    public DateTime CapturedAt { get; private set; }

    /// <summary>
    /// Gets when this metadata was created in the system.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Gets when this metadata was last modified.
    /// </summary>
    public DateTime ModifiedAt { get; private set; }

    /// <summary>
    /// Gets optional user-provided or auto-generated tags.
    /// </summary>
    public IReadOnlyCollection<string> Tags => _tags;

    /// <summary>
    /// Gets optional user-provided description.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Gets optional GPS latitude.
    /// </summary>
    public double? Latitude { get; private set; }

    /// <summary>
    /// Gets optional GPS longitude.
    /// </summary>
    public double? Longitude { get; private set; }

    /// <summary>
    /// Gets optional device model that captured the file.
    /// </summary>
    public string? DeviceModel { get; private set; }

    // EF Core constructor
    private FileMetadata()
    {
        OriginalFileName = string.Empty;
        SanitizedFileName = string.Empty;
        Hash = string.Empty;
    }

    private FileMetadata(string originalFileName, string hash, DateTime capturedAt)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("Filename cannot be null or empty", nameof(originalFileName));

        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("Hash cannot be null or empty", nameof(hash));

        if (!hash.ToLowerInvariant().StartsWith("sha256:"))
            throw new ArgumentException("Hash must be in format 'sha256:...'", nameof(hash));

        OriginalFileName = originalFileName.Trim();
        SanitizedFileName = SanitizeFilename(originalFileName);
        Hash = hash.ToLowerInvariant();
        CapturedAt = capturedAt;
        CreatedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates new file metadata.
    /// </summary>
    /// <param name="originalFileName">The original filename</param>
    /// <param name="hash">The SHA256 hash (format: "sha256:...")</param>
    /// <param name="capturedAt">When the file was originally captured</param>
    /// <returns>A new FileMetadata instance</returns>
    public static FileMetadata Create(string originalFileName, string hash, DateTime capturedAt)
    {
        return new FileMetadata(originalFileName, hash, capturedAt);
    }

    /// <summary>
    /// Adds a tag to the file.
    /// </summary>
    /// <param name="tag">The tag to add</param>
    public void AddTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return;

        var normalizedTag = tag.Trim().ToLowerInvariant();
        if (_tags.Add(normalizedTag))
        {
            ModifiedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Removes a tag from the file.
    /// </summary>
    /// <param name="tag">The tag to remove</param>
    public void RemoveTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return;

        var normalizedTag = tag.Trim().ToLowerInvariant();
        if (_tags.Remove(normalizedTag))
        {
            ModifiedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Sets the description of the file.
    /// </summary>
    /// <param name="description">The description</param>
    public void SetDescription(string? description)
    {
        Description = description?.Trim();
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the GPS location where the file was captured.
    /// </summary>
    /// <param name="latitude">Latitude in decimal degrees</param>
    /// <param name="longitude">Longitude in decimal degrees</param>
    public void SetGPSLocation(double latitude, double longitude)
    {
        if (latitude < -90 || latitude > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90");

        if (longitude < -180 || longitude > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180");

        Latitude = latitude;
        Longitude = longitude;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the device model that captured the file.
    /// </summary>
    /// <param name="deviceModel">The device model (e.g., "iPhone 15 Pro")</param>
    public void SetDeviceModel(string? deviceModel)
    {
        DeviceModel = deviceModel?.Trim();
        ModifiedAt = DateTime.UtcNow;
    }

    private static string SanitizeFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return "file";

        // Replace invalid characters with underscores
        var sanitized = InvalidFileCharsRegex.Replace(filename, "_");

        // Remove leading/trailing spaces and dots
        sanitized = sanitized.Trim().Trim('.');

        // Replace multiple consecutive underscores with single underscore
        sanitized = Regex.Replace(sanitized, @"_{2,}", "_");

        // If the result is empty, use a default name
        if (string.IsNullOrWhiteSpace(sanitized))
            return "file";

        return sanitized;
    }
}
