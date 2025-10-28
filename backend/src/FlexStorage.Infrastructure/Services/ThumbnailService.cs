using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Webp;

namespace FlexStorage.Infrastructure.Services;

/// <summary>
/// Service for generating image thumbnails using ImageSharp.
/// </summary>
public class ThumbnailService : IThumbnailService
{
    private readonly ThumbnailOptions _options;

    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/gif",
        "image/bmp",
        "image/webp"
    };

    public ThumbnailService(IOptions<ThumbnailOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Generates a thumbnail from an image stream.
    /// Uses configured defaults from ThumbnailOptions (300×300 @ 80% quality).
    /// </summary>
    public async Task<Stream> GenerateThumbnailAsync(
        Stream imageStream,
        int? width = null,
        int? height = null,
        int? quality = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        // Use configured defaults if parameters not provided
        var actualWidth = width ?? _options.Width;
        var actualHeight = height ?? _options.Height;
        var actualQuality = quality ?? _options.Quality;

        if (actualWidth <= 0 || actualWidth > 5000)
            throw new ArgumentException("Width must be between 1 and 5000", nameof(width));

        if (actualHeight <= 0 || actualHeight > 5000)
            throw new ArgumentException("Height must be between 1 and 5000", nameof(height));

        if (actualQuality <= 0 || actualQuality > 100)
            throw new ArgumentException("Quality must be between 1 and 100", nameof(quality));

        // Reset stream position if seekable
        if (imageStream.CanSeek)
            imageStream.Position = 0;

        try
        {
            // Load image
            using var image = await Image.LoadAsync(imageStream, cancellationToken);

            // Resize to thumbnail dimensions (maintain aspect ratio)
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(actualWidth, actualHeight),
                Mode = ResizeMode.Max // Maintains aspect ratio, fits within bounds
            }));

            // Save as WebP to memory stream (WebP @ 80% ≈ JPEG @ 85-90%)
            var outputStream = new MemoryStream();
            await image.SaveAsync(outputStream, new WebpEncoder
            {
                Quality = actualQuality,
                FileFormat = WebpFileFormatType.Lossy // Use lossy compression for smaller files
            }, cancellationToken);

            outputStream.Position = 0;
            return outputStream;
        }
        catch (UnknownImageFormatException ex)
        {
            throw new InvalidOperationException("Unsupported image format", ex);
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Failed to generate thumbnail", ex);
        }
    }

    /// <summary>
    /// Checks if the given MIME type is supported for thumbnail generation.
    /// </summary>
    public bool IsThumbnailSupported(string mimeType)
    {
        return !string.IsNullOrWhiteSpace(mimeType) && SupportedMimeTypes.Contains(mimeType);
    }
}
