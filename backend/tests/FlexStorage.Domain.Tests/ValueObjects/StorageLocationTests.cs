using FluentAssertions;
using FlexStorage.Domain.ValueObjects;
using Xunit;

namespace FlexStorage.Domain.Tests.ValueObjects;

public class StorageLocationTests
{
    [Fact]
    public void Should_CreateValidStorageLocation_WithProviderAndPath()
    {
        // Arrange
        var providerName = "S3 Glacier Deep Archive";
        var path = "s3://bucket/2025/10/25/file.jpg";

        // Act
        var location = StorageLocation.Create(providerName, path);

        // Assert
        location.Should().NotBeNull();
        location.ProviderName.Should().Be(providerName);
        location.Path.Should().Be(path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Should_RejectNullOrEmptyProviderName(string? providerName)
    {
        // Arrange
        var path = "s3://bucket/file.jpg";

        // Act
        Action act = () => StorageLocation.Create(providerName!, path);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*provider name*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Should_RejectNullOrEmptyStoragePath(string? path)
    {
        // Arrange
        var providerName = "S3 Glacier";

        // Act
        Action act = () => StorageLocation.Create(providerName, path!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*path*");
    }

    [Theory]
    [InlineData("s3://bucket/file.jpg")]
    [InlineData("s3-glacier-deep://bucket/2025/10/25/file.jpg")]
    [InlineData("backblaze://bucket-name/path/to/file.mp4")]
    [InlineData("r2://cloudflare-bucket/file.png")]
    public void Should_ValidatePathFormat_WithProviderScheme(string path)
    {
        // Arrange
        var providerName = "Test Provider";

        // Act
        var location = StorageLocation.Create(providerName, path);

        // Assert
        location.Path.Should().Be(path);
        location.Path.Should().Contain("://");
    }

    [Fact]
    public void Should_SupportEqualityComparison_SameProviderAndPath()
    {
        // Arrange
        var location1 = StorageLocation.Create("S3 Glacier", "s3://bucket/file.jpg");
        var location2 = StorageLocation.Create("S3 Glacier", "s3://bucket/file.jpg");

        // Act & Assert
        location1.Should().Be(location2);
        (location1 == location2).Should().BeTrue();
        (location1 != location2).Should().BeFalse();
    }

    [Fact]
    public void Should_SupportEqualityComparison_DifferentPath()
    {
        // Arrange
        var location1 = StorageLocation.Create("S3 Glacier", "s3://bucket/file1.jpg");
        var location2 = StorageLocation.Create("S3 Glacier", "s3://bucket/file2.jpg");

        // Act & Assert
        location1.Should().NotBe(location2);
        (location1 == location2).Should().BeFalse();
        (location1 != location2).Should().BeTrue();
    }

    [Fact]
    public void Should_SupportEqualityComparison_DifferentProvider()
    {
        // Arrange
        var location1 = StorageLocation.Create("S3 Glacier", "s3://bucket/file.jpg");
        var location2 = StorageLocation.Create("Backblaze", "s3://bucket/file.jpg");

        // Act & Assert
        location1.Should().NotBe(location2);
    }

    [Fact]
    public void Should_ParseLocationString_Correctly()
    {
        // Arrange
        var providerName = "S3 Glacier Deep Archive";
        var path = "s3-deep://my-bucket/2025/10/25/abc123.jpg";

        // Act
        var location = StorageLocation.Create(providerName, path);
        var locationString = location.ToString();

        // Assert
        locationString.Should().Contain(providerName);
        locationString.Should().Contain(path);
    }

    [Fact]
    public void Should_GenerateLocationString_WithCorrectFormat()
    {
        // Arrange
        var providerName = "Backblaze B2";
        var path = "b2://bucket-name/path/file.mp4";
        var location = StorageLocation.Create(providerName, path);

        // Act
        var locationString = location.ToString();

        // Assert
        locationString.Should().Be($"{providerName}: {path}");
    }
}
