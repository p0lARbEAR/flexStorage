namespace FlexStorage.Domain.ValueObjects;

/// <summary>
/// Value object representing a file's MIME type and category.
/// </summary>
public sealed class FileType : IEquatable<FileType>
{
    private static readonly Dictionary<string, string> MimeTypeToExtension = new()
    {
        // Images
        { "image/jpeg", ".jpg" },
        { "image/jpg", ".jpg" },
        { "image/png", ".png" },
        { "image/gif", ".gif" },
        { "image/webp", ".webp" },
        { "image/heic", ".heic" },
        { "image/heif", ".heif" },
        { "image/bmp", ".bmp" },
        { "image/tiff", ".tiff" },
        { "image/svg+xml", ".svg" },

        // Videos
        { "video/mp4", ".mp4" },
        { "video/quicktime", ".mov" },
        { "video/x-msvideo", ".avi" },
        { "video/mpeg", ".mpeg" },
        { "video/webm", ".webm" },
        { "video/x-matroska", ".mkv" },

        // Documents
        { "application/pdf", ".pdf" },
        { "application/msword", ".doc" },
        { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx" },
        { "application/vnd.ms-excel", ".xls" },
        { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx" },

        // Archives
        { "application/zip", ".zip" },
        { "application/x-rar-compressed", ".rar" },
        { "application/x-7z-compressed", ".7z" },
        { "application/gzip", ".gz" },

        // Text
        { "text/plain", ".txt" },
        { "text/html", ".html" },
        { "text/css", ".css" },
        { "text/javascript", ".js" },
        { "application/json", ".json" },
        { "application/xml", ".xml" },

        // Other
        { "application/octet-stream", ".bin" }
    };

    private static readonly Dictionary<string, List<string>> ExtensionToMimeTypes = new()
    {
        { ".jpg", new List<string> { "image/jpeg", "image/jpg" } },
        { ".jpeg", new List<string> { "image/jpeg", "image/jpg" } },
        { ".png", new List<string> { "image/png" } },
        { ".gif", new List<string> { "image/gif" } },
        { ".webp", new List<string> { "image/webp" } },
        { ".heic", new List<string> { "image/heic" } },
        { ".heif", new List<string> { "image/heif" } },
        { ".mp4", new List<string> { "video/mp4" } },
        { ".mov", new List<string> { "video/quicktime" } },
        { ".avi", new List<string> { "video/x-msvideo" } },
        { ".pdf", new List<string> { "application/pdf" } },
        { ".txt", new List<string> { "text/plain" } }
    };

    /// <summary>
    /// Gets the MIME type of the file.
    /// </summary>
    public string MimeType { get; }

    /// <summary>
    /// Gets the category of the file.
    /// </summary>
    public FileCategory Category { get; }

    /// <summary>
    /// Gets whether this is a photo file.
    /// </summary>
    public bool IsPhoto => Category == FileCategory.Photo;

    /// <summary>
    /// Gets whether this is a video file.
    /// </summary>
    public bool IsVideo => Category == FileCategory.Video;

    /// <summary>
    /// Gets whether this is a miscellaneous file.
    /// </summary>
    public bool IsMisc => Category == FileCategory.Misc;

    private FileType(string mimeType, FileCategory category)
    {
        MimeType = mimeType;
        Category = category;
    }

    /// <summary>
    /// Creates a FileType from a MIME type string.
    /// </summary>
    /// <param name="mimeType">The MIME type (e.g., "image/jpeg")</param>
    /// <returns>A new FileType instance</returns>
    /// <exception cref="ArgumentException">Thrown when MIME type is invalid</exception>
    public static FileType FromMimeType(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            throw new ArgumentException("MIME type cannot be null or empty", nameof(mimeType));

        mimeType = mimeType.Trim().ToLowerInvariant();

        // Validate MIME type format (type/subtype)
        if (!mimeType.Contains('/'))
            throw new ArgumentException($"Invalid MIME type format: {mimeType}", nameof(mimeType));

        var category = DetermineCategory(mimeType);

        return new FileType(mimeType, category);
    }

    /// <summary>
    /// Gets the recommended file extension for this MIME type.
    /// </summary>
    /// <returns>File extension with leading dot (e.g., ".jpg")</returns>
    public string GetFileExtension()
    {
        if (MimeTypeToExtension.TryGetValue(MimeType, out var extension))
            return extension;

        // Fallback: extract from MIME type subtype
        var parts = MimeType.Split('/');
        if (parts.Length == 2)
            return $".{parts[1]}";

        return ".bin";
    }

    /// <summary>
    /// Gets the recommended storage tier for this file type.
    /// </summary>
    /// <returns>Storage tier identifier</returns>
    public string GetStorageTierRecommendation()
    {
        return Category switch
        {
            FileCategory.Photo => "glacier-deep-archive",  // Photos: rarely accessed, cheapest
            FileCategory.Video => "glacier-flexible-retrieval",  // Videos: larger, occasional access
            FileCategory.Misc => "glacier-flexible-retrieval",  // Misc: faster retrieval may be needed
            _ => "glacier-flexible-retrieval"
        };
    }

    /// <summary>
    /// Validates whether the given file extension matches this MIME type.
    /// </summary>
    /// <param name="extension">File extension with or without leading dot</param>
    /// <returns>True if the extension is valid for this MIME type</returns>
    public bool IsExtensionValid(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        // Normalize extension (ensure leading dot, lowercase)
        extension = extension.Trim().ToLowerInvariant();
        if (!extension.StartsWith('.'))
            extension = $".{extension}";

        // Check if this extension maps to our MIME type
        if (ExtensionToMimeTypes.TryGetValue(extension, out var validMimeTypes))
        {
            return validMimeTypes.Contains(MimeType);
        }

        // Fallback: check if the expected extension matches
        return GetFileExtension() == extension;
    }

    private static FileCategory DetermineCategory(string mimeType)
    {
        var primaryType = mimeType.Split('/')[0];

        return primaryType switch
        {
            "image" => FileCategory.Photo,
            "video" => FileCategory.Video,
            _ => FileCategory.Misc
        };
    }

    #region Equality

    public bool Equals(FileType? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return MimeType == other.MimeType;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is FileType other && Equals(other);
    }

    public override int GetHashCode()
    {
        return MimeType.GetHashCode();
    }

    public static bool operator ==(FileType? left, FileType? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(FileType? left, FileType? right)
    {
        return !Equals(left, right);
    }

    #endregion

    public override string ToString() => $"{Category}: {MimeType}";
}
