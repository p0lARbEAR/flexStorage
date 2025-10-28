namespace FlexStorage.Application.Interfaces.Services;

/// <summary>
/// Service interface for generating image thumbnails.
/// </summary>
public interface IThumbnailService
{
    /// <summary>
    /// Generates a thumbnail from an image stream.
    /// </summary>
    /// <param name="imageStream">The source image stream</param>
    /// <param name="width">Thumbnail width in pixels (null = use configured default)</param>
    /// <param name="height">Thumbnail height in pixels (null = use configured default)</param>
    /// <param name="quality">WebP quality 1-100 (null = use configured default)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream containing the thumbnail image (WebP format)</returns>
    Task<Stream> GenerateThumbnailAsync(
        Stream imageStream,
        int? width = null,
        int? height = null,
        int? quality = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the given MIME type is supported for thumbnail generation.
    /// </summary>
    /// <param name="mimeType">The MIME type to check</param>
    /// <returns>True if thumbnails can be generated for this type</returns>
    bool IsThumbnailSupported(string mimeType);
}
