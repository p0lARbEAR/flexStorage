using FlexStorage.Application.Interfaces.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace FlexStorage.Infrastructure.Services;

/// <summary>
/// Service for generating image thumbnails using ImageSharp.
/// </summary>
public class ThumbnailService : IThumbnailService
{
    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/gif",
        "image/bmp",
        "image/webp"
    };

    /// <summary>
    /// Generates a thumbnail from an image stream.
    /// </summary>
    public async Task<Stream> GenerateThumbnailAsync(
        Stream imageStream,
        int width = 200,
        int height = 200,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        if (width <= 0 || width > 5000)
            throw new ArgumentException("Width must be between 1 and 5000", nameof(width));

        if (height <= 0 || height > 5000)
            throw new ArgumentException("Height must be between 1 and 5000", nameof(height));

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
                Size = new Size(width, height),
                Mode = ResizeMode.Max // Maintains aspect ratio, fits within bounds
            }));

            // Save as JPEG to memory stream
            var outputStream = new MemoryStream();
            await image.SaveAsync(outputStream, new JpegEncoder
            {
                Quality = 85 // Good quality/size balance
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
