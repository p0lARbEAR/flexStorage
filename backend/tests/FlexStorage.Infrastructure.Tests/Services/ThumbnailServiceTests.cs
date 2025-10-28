using FluentAssertions;
using FlexStorage.Infrastructure.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using Xunit;

namespace FlexStorage.Infrastructure.Tests.Services;

public class ThumbnailServiceTests
{
    private readonly ThumbnailService _sut;

    public ThumbnailServiceTests()
    {
        _sut = new ThumbnailService();
    }

    [Fact]
    public async Task GenerateThumbnailAsync_WithValidJpegImage_ShouldCreateThumbnail()
    {
        // Arrange
        var imageBytes = CreateTestJpegImage();
        using var imageStream = new MemoryStream(imageBytes);

        // Act
        using var thumbnailStream = await _sut.GenerateThumbnailAsync(imageStream, 200, 200);

        // Assert
        thumbnailStream.Should().NotBeNull();
        thumbnailStream.Length.Should().BeGreaterThan(0);
        thumbnailStream.Position.Should().Be(0); // Should be reset for reading
    }

    [Fact]
    public async Task GenerateThumbnailAsync_WithValidPngImage_ShouldCreateThumbnail()
    {
        // Arrange
        var imageBytes = CreateTestPngImage();
        using var imageStream = new MemoryStream(imageBytes);

        // Act
        using var thumbnailStream = await _sut.GenerateThumbnailAsync(imageStream, 200, 200);

        // Assert
        thumbnailStream.Should().NotBeNull();
        thumbnailStream.Length.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/jpg")]
    [InlineData("image/png")]
    [InlineData("image/gif")]
    [InlineData("image/bmp")]
    [InlineData("image/webp")]
    public void IsThumbnailSupported_WithSupportedMimeTypes_ShouldReturnTrue(string mimeType)
    {
        // Act
        var result = _sut.IsThumbnailSupported(mimeType);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("video/mp4")]
    [InlineData("text/plain")]
    [InlineData("application/octet-stream")]
    [InlineData("")]
    [InlineData(null)]
    public void IsThumbnailSupported_WithUnsupportedMimeTypes_ShouldReturnFalse(string? mimeType)
    {
        // Act
        var result = _sut.IsThumbnailSupported(mimeType!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateThumbnailAsync_WithInvalidImageData_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invalidData = new byte[] { 1, 2, 3, 4, 5 };
        using var invalidStream = new MemoryStream(invalidData);

        // Act
        Func<Task> act = async () => await _sut.GenerateThumbnailAsync(invalidStream, 200, 200);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Unsupported image format*");
    }

    [Theory]
    [InlineData(0, 200)]
    [InlineData(-1, 200)]
    [InlineData(5001, 200)]
    [InlineData(200, 0)]
    [InlineData(200, -1)]
    [InlineData(200, 5001)]
    public async Task GenerateThumbnailAsync_WithInvalidDimensions_ShouldThrowArgumentException(int width, int height)
    {
        // Arrange
        var imageBytes = CreateTestJpegImage();
        using var imageStream = new MemoryStream(imageBytes);

        // Act
        Func<Task> act = async () => await _sut.GenerateThumbnailAsync(imageStream, width, height);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateThumbnailAsync_WithNullStream_ShouldThrowArgumentNullException()
    {
        // Act
        Func<Task> act = async () => await _sut.GenerateThumbnailAsync(null!, 200, 200);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GenerateThumbnailAsync_WithLargeImage_ShouldResizeToMaxDimensions()
    {
        // Arrange - Create a large test image
        var largeImageBytes = CreateTestJpegImage(1920, 1080);
        using var imageStream = new MemoryStream(largeImageBytes);

        // Act
        using var thumbnailStream = await _sut.GenerateThumbnailAsync(imageStream, 200, 200);

        // Assert
        thumbnailStream.Should().NotBeNull();
        thumbnailStream.Length.Should().BeGreaterThan(0);
        // Thumbnail should be smaller than original
        thumbnailStream.Length.Should().BeLessThan(imageStream.Length);
    }

    /// <summary>
    /// Creates a minimal valid JPEG image for testing (1x1 pixel)
    /// </summary>
    private byte[] CreateTestJpegImage(int width = 1, int height = 1)
    {
        // Use SixLabors.ImageSharp to create a valid test image
        using var image = new Image<Rgba32>(width, height);
        using var ms = new MemoryStream();
        image.Save(ms, new JpegEncoder());
        return ms.ToArray();
    }

    /// <summary>
    /// Creates a minimal valid PNG image for testing (1x1 pixel)
    /// </summary>
    private byte[] CreateTestPngImage(int width = 1, int height = 1)
    {
        using var image = new Image<Rgba32>(width, height);
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }
}
