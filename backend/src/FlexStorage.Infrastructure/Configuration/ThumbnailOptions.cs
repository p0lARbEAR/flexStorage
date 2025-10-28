namespace FlexStorage.Infrastructure.Configuration;

/// <summary>
/// Configuration options for thumbnail generation.
/// </summary>
public class ThumbnailOptions
{
    public const string SectionName = "Thumbnail";

    /// <summary>
    /// Thumbnail width in pixels (default: 300).
    /// </summary>
    public int Width { get; set; } = 300;

    /// <summary>
    /// Thumbnail height in pixels (default: 300).
    /// </summary>
    public int Height { get; set; } = 300;

    /// <summary>
    /// WebP quality 1-100 (default: 80, equivalent to JPEG 85-90%).
    /// </summary>
    public int Quality { get; set; } = 80;
}
