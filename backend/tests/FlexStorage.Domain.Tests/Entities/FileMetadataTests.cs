using FluentAssertions;
using FlexStorage.Domain.Entities;
using Xunit;

namespace FlexStorage.Domain.Tests.Entities;

public class FileMetadataTests
{
    [Fact]
    public void Should_CreateMetadata_WithRequiredProperties()
    {
        // Arrange
        var originalFilename = "My Photo.jpg";
        var hash = "sha256:abc123def456";
        var capturedAt = DateTime.UtcNow;

        // Act
        var metadata = FileMetadata.Create(originalFilename, hash, capturedAt);

        // Assert
        metadata.Should().NotBeNull();
        metadata.OriginalFileName.Should().Be(originalFilename);
        metadata.Hash.Should().Be(hash);
        metadata.CapturedAt.Should().Be(capturedAt);
        metadata.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Should_ValidateFilename_IsNotNullOrEmpty(string? filename)
    {
        // Act
        Action act = () => FileMetadata.Create(filename!, "sha256:abc123", DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*filename*");
    }

    [Fact]
    public void Should_SanitizeFilename_RemoveSpecialCharacters()
    {
        // Arrange
        var originalFilename = "My<>Photo|File?.jpg";

        // Act
        var metadata = FileMetadata.Create(originalFilename, "sha256:abc123", DateTime.UtcNow);

        // Assert
        metadata.SanitizedFileName.Should().NotContain("<");
        metadata.SanitizedFileName.Should().NotContain(">");
        metadata.SanitizedFileName.Should().NotContain("|");
        metadata.SanitizedFileName.Should().NotContain("?");
        metadata.SanitizedFileName.Should().EndWith(".jpg");
    }

    [Fact]
    public void Should_StoreOriginalFilename_Separately()
    {
        // Arrange
        var originalFilename = "My Photo (2024).jpg";

        // Act
        var metadata = FileMetadata.Create(originalFilename, "sha256:abc123", DateTime.UtcNow);

        // Assert
        metadata.OriginalFileName.Should().Be(originalFilename);
        metadata.SanitizedFileName.Should().NotBe(originalFilename);
        metadata.SanitizedFileName.Should().NotContain("(");
        metadata.SanitizedFileName.Should().NotContain(")");
    }

    [Theory]
    [InlineData("sha256:abc123")]
    [InlineData("SHA256:DEF456")]
    [InlineData("sha256:0123456789abcdef")]
    public void Should_ValidateAndStore_SHA256Hash(string hash)
    {
        // Act
        var metadata = FileMetadata.Create("file.jpg", hash, DateTime.UtcNow);

        // Assert
        metadata.Hash.Should().Be(hash.ToLowerInvariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-hash")]
    [InlineData("md5:abc123")]
    public void Should_RejectInvalidHashFormat(string invalidHash)
    {
        // Act
        Action act = () => FileMetadata.Create("file.jpg", invalidHash, DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*hash*");
    }

    [Fact]
    public void Should_StoreCreationTimestamp_Automatically()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var metadata = FileMetadata.Create("file.jpg", "sha256:abc123", DateTime.UtcNow);

        // Assert
        metadata.CreatedAt.Should().BeOnOrAfter(beforeCreation);
        metadata.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Should_UpdateModificationTimestamp_WhenChanged()
    {
        // Arrange
        var metadata = FileMetadata.Create("file.jpg", "sha256:abc123", DateTime.UtcNow);
        var originalModifiedAt = metadata.ModifiedAt;

        System.Threading.Thread.Sleep(10); // Ensure time difference

        // Act
        metadata.AddTag("vacation");

        // Assert
        metadata.ModifiedAt.Should().BeAfter(originalModifiedAt);
    }

    [Fact]
    public void Should_StoreOptionalUserTags()
    {
        // Arrange
        var metadata = FileMetadata.Create("file.jpg", "sha256:abc123", DateTime.UtcNow);

        // Act
        metadata.AddTag("vacation");
        metadata.AddTag("beach");

        // Assert
        metadata.Tags.Should().Contain("vacation");
        metadata.Tags.Should().Contain("beach");
        metadata.Tags.Should().HaveCount(2);
    }

    [Fact]
    public void Should_StoreOptionalDescription()
    {
        // Arrange
        var metadata = FileMetadata.Create("file.jpg", "sha256:abc123", DateTime.UtcNow);
        var description = "Beautiful sunset at the beach";

        // Act
        metadata.SetDescription(description);

        // Assert
        metadata.Description.Should().Be(description);
    }

    [Fact]
    public void Should_StoreOptionalGPSCoordinates()
    {
        // Arrange
        var metadata = FileMetadata.Create("file.jpg", "sha256:abc123", DateTime.UtcNow);
        var latitude = 37.7749;
        var longitude = -122.4194;

        // Act
        metadata.SetGPSLocation(latitude, longitude);

        // Assert
        metadata.Latitude.Should().Be(latitude);
        metadata.Longitude.Should().Be(longitude);
    }

    [Fact]
    public void Should_StoreOptionalDeviceMetadata()
    {
        // Arrange
        var metadata = FileMetadata.Create("file.jpg", "sha256:abc123", DateTime.UtcNow);
        var deviceModel = "iPhone 15 Pro";

        // Act
        metadata.SetDeviceModel(deviceModel);

        // Assert
        metadata.DeviceModel.Should().Be(deviceModel);
    }

    [Fact]
    public void Should_PreventDuplicateTags()
    {
        // Arrange
        var metadata = FileMetadata.Create("file.jpg", "sha256:abc123", DateTime.UtcNow);

        // Act
        metadata.AddTag("vacation");
        metadata.AddTag("vacation"); // Duplicate

        // Assert
        metadata.Tags.Should().ContainSingle("vacation");
    }

    [Fact]
    public void Should_AllowRemovingTags()
    {
        // Arrange
        var metadata = FileMetadata.Create("file.jpg", "sha256:abc123", DateTime.UtcNow);
        metadata.AddTag("vacation");
        metadata.AddTag("beach");

        // Act
        metadata.RemoveTag("vacation");

        // Assert
        metadata.Tags.Should().NotContain("vacation");
        metadata.Tags.Should().Contain("beach");
    }
}
