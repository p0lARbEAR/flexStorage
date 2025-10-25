using FluentAssertions;
using FlexStorage.Domain.ValueObjects;
using Xunit;

namespace FlexStorage.Domain.Tests.ValueObjects;

public class FileTypeTests
{
    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/jpg")]
    [InlineData("image/png")]
    [InlineData("image/gif")]
    [InlineData("image/webp")]
    [InlineData("image/heic")]
    [InlineData("image/heif")]
    public void Should_CreateValidPhotoType_FromMimeType(string mimeType)
    {
        // Act
        var fileType = FileType.FromMimeType(mimeType);

        // Assert
        fileType.Should().NotBeNull();
        fileType.MimeType.Should().Be(mimeType);
        fileType.Category.Should().Be(FileCategory.Photo);
    }

    [Theory]
    [InlineData("video/mp4")]
    [InlineData("video/quicktime")]  // MOV
    [InlineData("video/x-msvideo")]  // AVI
    [InlineData("video/mpeg")]
    [InlineData("video/webm")]
    public void Should_CreateValidVideoType_FromMimeType(string mimeType)
    {
        // Act
        var fileType = FileType.FromMimeType(mimeType);

        // Assert
        fileType.Should().NotBeNull();
        fileType.MimeType.Should().Be(mimeType);
        fileType.Category.Should().Be(FileCategory.Video);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("application/zip")]
    [InlineData("application/json")]
    [InlineData("text/plain")]
    [InlineData("application/octet-stream")]
    public void Should_CreateValidMiscType_FromMimeType(string mimeType)
    {
        // Act
        var fileType = FileType.FromMimeType(mimeType);

        // Assert
        fileType.Should().NotBeNull();
        fileType.MimeType.Should().Be(mimeType);
        fileType.Category.Should().Be(FileCategory.Misc);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Should_RejectInvalidMimeType_NullOrEmpty(string? mimeType)
    {
        // Act
        Action act = () => FileType.FromMimeType(mimeType!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*MIME type*");
    }

    [Fact]
    public void Should_RejectInvalidMimeType_MalformedFormat()
    {
        // Arrange
        var invalidMimeType = "not-a-mime-type";

        // Act
        Action act = () => FileType.FromMimeType(invalidMimeType);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid MIME type format*");
    }

    [Theory]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/png", ".png")]
    [InlineData("image/gif", ".gif")]
    [InlineData("image/heic", ".heic")]
    [InlineData("video/mp4", ".mp4")]
    [InlineData("video/quicktime", ".mov")]
    [InlineData("application/pdf", ".pdf")]
    [InlineData("text/plain", ".txt")]
    public void Should_MapMimeTypeToFileExtension(string mimeType, string expectedExtension)
    {
        // Act
        var fileType = FileType.FromMimeType(mimeType);

        // Assert
        fileType.GetFileExtension().Should().Be(expectedExtension);
    }

    [Fact]
    public void Should_RecommendDeepArchive_ForPhotos()
    {
        // Arrange
        var photoType = FileType.FromMimeType("image/jpeg");

        // Act
        var recommendation = photoType.GetStorageTierRecommendation();

        // Assert
        recommendation.Should().Be("glacier-deep-archive");
    }

    [Fact]
    public void Should_RecommendFlexibleRetrieval_ForVideos()
    {
        // Arrange
        var videoType = FileType.FromMimeType("video/mp4");

        // Act
        var recommendation = videoType.GetStorageTierRecommendation();

        // Assert
        recommendation.Should().Be("glacier-flexible-retrieval");
    }

    [Fact]
    public void Should_RecommendFlexibleRetrieval_ForMiscFiles()
    {
        // Arrange
        var miscType = FileType.FromMimeType("application/pdf");

        // Act
        var recommendation = miscType.GetStorageTierRecommendation();

        // Assert
        recommendation.Should().Be("glacier-flexible-retrieval");
    }

    [Fact]
    public void Should_CategorizeAsPhoto_Correctly()
    {
        // Arrange
        var fileType = FileType.FromMimeType("image/png");

        // Act & Assert
        fileType.IsPhoto.Should().BeTrue();
        fileType.IsVideo.Should().BeFalse();
        fileType.IsMisc.Should().BeFalse();
    }

    [Fact]
    public void Should_CategorizeAsVideo_Correctly()
    {
        // Arrange
        var fileType = FileType.FromMimeType("video/mp4");

        // Act & Assert
        fileType.IsPhoto.Should().BeFalse();
        fileType.IsVideo.Should().BeTrue();
        fileType.IsMisc.Should().BeFalse();
    }

    [Fact]
    public void Should_CategorizeAsMisc_Correctly()
    {
        // Arrange
        var fileType = FileType.FromMimeType("application/pdf");

        // Act & Assert
        fileType.IsPhoto.Should().BeFalse();
        fileType.IsVideo.Should().BeFalse();
        fileType.IsMisc.Should().BeTrue();
    }

    [Theory]
    [InlineData("image/jpeg", ".jpg", true)]
    [InlineData("image/jpeg", ".jpeg", true)]
    [InlineData("image/jpeg", ".png", false)]
    [InlineData("image/png", ".png", true)]
    [InlineData("video/mp4", ".mp4", true)]
    [InlineData("video/mp4", ".mov", false)]
    public void Should_ValidateFileExtensionMatchesMimeType(string mimeType, string extension, bool shouldMatch)
    {
        // Arrange
        var fileType = FileType.FromMimeType(mimeType);

        // Act
        var matches = fileType.IsExtensionValid(extension);

        // Assert
        matches.Should().Be(shouldMatch);
    }

    [Fact]
    public void Should_CompareFileTypes_Equality()
    {
        // Arrange
        var fileType1 = FileType.FromMimeType("image/jpeg");
        var fileType2 = FileType.FromMimeType("image/jpeg");

        // Act & Assert
        fileType1.Should().Be(fileType2);
        (fileType1 == fileType2).Should().BeTrue();
        (fileType1 != fileType2).Should().BeFalse();
    }

    [Fact]
    public void Should_CompareFileTypes_Inequality()
    {
        // Arrange
        var fileType1 = FileType.FromMimeType("image/jpeg");
        var fileType2 = FileType.FromMimeType("image/png");

        // Act & Assert
        fileType1.Should().NotBe(fileType2);
        (fileType1 == fileType2).Should().BeFalse();
        (fileType1 != fileType2).Should().BeTrue();
    }
}
