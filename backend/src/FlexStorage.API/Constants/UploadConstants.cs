namespace FlexStorage.API.Constants;

/// <summary>
/// Constants for file upload limits.
/// These must be compile-time constants to work with attributes.
/// The actual runtime limit is configured in appsettings.json (Upload:MaxFileSizeBytes).
/// </summary>
public static class UploadConstants
{
    /// <summary>
    /// Maximum file size for single-request uploads: 20 MB (20,971,520 bytes).
    /// Used in [RequestSizeLimit] attribute.
    /// IMPORTANT: Keep this in sync with appsettings.json Upload:MaxFileSizeBytes.
    /// </summary>
    public const long MaxFileSizeBytes = 20_971_520; // 20 MB
}
