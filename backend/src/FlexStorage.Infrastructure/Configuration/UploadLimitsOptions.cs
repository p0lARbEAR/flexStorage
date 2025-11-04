namespace FlexStorage.Infrastructure.Configuration;

/// <summary>
/// Configuration options for file upload size limits.
/// </summary>
public class UploadLimitsOptions
{
    public const string SectionName = "UploadLimits";

    /// <summary>
    /// Maximum file size in bytes for single-request uploads (default: 20 MB = 20,971,520 bytes).
    /// Files larger than this must use chunked upload.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 20_971_520; // 20 MB

    /// <summary>
    /// Maximum file size in megabytes (human-readable, default: 20 MB).
    /// Used for documentation and error messages.
    /// </summary>
    public int MaxFileSizeMB { get; set; } = 20;
}
