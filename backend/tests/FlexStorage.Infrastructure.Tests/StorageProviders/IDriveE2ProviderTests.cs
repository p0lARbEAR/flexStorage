using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.ValueObjects;
using FlexStorage.Infrastructure.Storage;
using FluentAssertions;
using Moq;
using Xunit;

namespace FlexStorage.Infrastructure.Tests.StorageProviders;

/// <summary>
/// Tests for iDrive e2 S3-compatible storage provider.
/// iDrive e2 offers $4/TB/month with 3x free egress, ideal for photo/video storage.
/// </summary>
public class IDriveE2ProviderTests
{
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly IDriveE2Provider _provider;
    private const string TestBucketName = "test-idrive-bucket";

    public IDriveE2ProviderTests()
    {
        _mockS3Client = new Mock<IAmazonS3>();
        _provider = new IDriveE2Provider(_mockS3Client.Object, TestBucketName);
    }

    [Fact]
    public void ProviderName_ShouldReturnCorrectName()
    {
        // Act
        var name = _provider.ProviderName;

        // Assert
        name.Should().Be("idrive-e2");
    }

    [Fact]
    public void Capabilities_ShouldReturnCorrectCapabilities()
    {
        // Act
        var capabilities = _provider.Capabilities;

        // Assert
        capabilities.SupportsInstantAccess.Should().BeTrue();
        capabilities.SupportsRetrieval.Should().BeFalse(); // Instant access, no retrieval
        capabilities.SupportsDeletion.Should().BeTrue();
        capabilities.SupportsDeepArchive.Should().BeFalse();
        capabilities.SupportsFlexibleRetrieval.Should().BeFalse();
        capabilities.MinRetrievalTime.Should().Be(TimeSpan.Zero);
        capabilities.MaxRetrievalTime.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task UploadAsync_WithValidFile_ShouldUploadToIDriveE2()
    {
        // Arrange
        var fileContent = "test photo data";
        var fileStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
        var options = new UploadOptions
        {
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            Metadata = new Dictionary<string, string>
            {
                { "user-id", "test-user-123" },
                { "captured-at", "2025-01-15T10:30:00Z" }
            }
        };

        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse());

        // Act
        var result = await _provider.UploadAsync(fileStream, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Location.Should().NotBeNull();
        result.Location!.ProviderName.Should().Be("idrive-e2");
        result.Location.Path.Should().StartWith($"s3://{TestBucketName}/files/");
        result.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify S3 call with Standard storage class (iDrive e2 default)
        _mockS3Client.Verify(x => x.PutObjectAsync(
            It.Is<PutObjectRequest>(req =>
                req.BucketName == TestBucketName &&
                req.StorageClass == S3StorageClass.Standard &&
                req.ContentType == "image/jpeg" &&
                req.Key.StartsWith("files/") &&
                req.Metadata["x-amz-meta-user-id"] == "test-user-123" &&
                req.Metadata["x-amz-meta-captured-at"] == "2025-01-15T10:30:00Z"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadAsync_WithException_ShouldReturnFailureResult()
    {
        // Arrange
        var fileStream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var options = new UploadOptions
        {
            FileName = "test.jpg",
            ContentType = "image/jpeg"
        };

        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("iDrive e2 connection error"));

        // Act
        var result = await _provider.UploadAsync(fileStream, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("iDrive e2 connection error");
    }

    [Fact]
    public async Task DownloadAsync_WithValidLocation_ShouldReturnStream()
    {
        // Arrange
        var location = StorageLocation.Create("idrive-e2", $"s3://{TestBucketName}/files/2025/01/15/abcd1234_photo.jpg");
        var expectedContent = "photo data";
        var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(expectedContent));

        var getObjectResponse = new GetObjectResponse
        {
            ResponseStream = responseStream
        };

        _mockS3Client
            .Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(getObjectResponse);

        // Act
        var resultStream = await _provider.DownloadAsync(location, CancellationToken.None);

        // Assert
        resultStream.Should().NotBeNull();
        resultStream.Should().BeSameAs(responseStream);

        _mockS3Client.Verify(x => x.GetObjectAsync(
            It.Is<GetObjectRequest>(req =>
                req.BucketName == TestBucketName &&
                req.Key == "files/2025/01/15/abcd1234_photo.jpg"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_WithException_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var location = StorageLocation.Create("idrive-e2", $"s3://{TestBucketName}/files/missing.jpg");

        _mockS3Client
            .Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("File not found"));

        // Act
        Func<Task> act = async () => await _provider.DownloadAsync(location, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to download from iDrive e2*");
    }

    [Fact]
    public async Task DeleteAsync_WithValidLocation_ShouldDeleteFromIDriveE2()
    {
        // Arrange
        var location = StorageLocation.Create("idrive-e2", $"s3://{TestBucketName}/files/test.jpg");

        _mockS3Client
            .Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse());

        // Act
        var result = await _provider.DeleteAsync(location, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        _mockS3Client.Verify(x => x.DeleteObjectAsync(
            It.Is<DeleteObjectRequest>(req =>
                req.BucketName == TestBucketName &&
                req.Key == "files/test.jpg"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WithException_ShouldReturnFalse()
    {
        // Arrange
        var location = StorageLocation.Create("idrive-e2", $"s3://{TestBucketName}/files/test.jpg");

        _mockS3Client
            .Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Delete failed"));

        // Act
        var result = await _provider.DeleteAsync(location, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task InitiateRetrievalAsync_ShouldThrowNotSupportedException()
    {
        // Arrange
        var location = StorageLocation.Create("idrive-e2", $"s3://{TestBucketName}/files/test.jpg");

        // Act
        Func<Task> act = async () => await _provider.InitiateRetrievalAsync(
            location,
            RetrievalTier.Standard,
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*does not require retrieval*");
    }

    [Fact]
    public async Task GetRetrievalStatusAsync_ShouldThrowNotSupportedException()
    {
        // Act
        Func<Task> act = async () => await _provider.GetRetrievalStatusAsync(
            "test-retrieval-id",
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*does not require retrieval*");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenHealthy_ShouldReturnHealthyStatus()
    {
        // Arrange
        _mockS3Client
            .Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response());

        // Act
        var result = await _provider.CheckHealthAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsHealthy.Should().BeTrue();
        result.Message.Should().Contain("healthy");
        result.Message.Should().Contain(TestBucketName);
        result.ResponseTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenUnhealthy_ShouldReturnUnhealthyStatus()
    {
        // Arrange
        _mockS3Client
            .Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("iDrive e2 connection failed"));

        // Act
        var result = await _provider.CheckHealthAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsHealthy.Should().BeFalse();
        result.Message.Should().Contain("unhealthy");
        result.Message.Should().Contain("iDrive e2 connection failed");
        result.ResponseTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task GenerateS3Key_ShouldCreateKeyWithDateAndUniqueId()
    {
        // Arrange
        var options = new UploadOptions
        {
            FileName = "beach_sunset.jpg",
            ContentType = "image/jpeg"
        };

        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse());

        // Act
        var result = await _provider.UploadAsync(
            new MemoryStream(new byte[] { 1, 2, 3 }),
            options,
            CancellationToken.None);

        // Assert - Verify key format: files/YYYY-MM-DD or files/YYYY/MM/DD (depends on culture)
        // DateTime format "/" gets replaced by system date separator (- or /)
        result.Location!.Path.Should().MatchRegex(@"s3://.+/files/\d{4}[-/]\d{2}[-/]\d{2}/[a-f0-9]{8}_beach_sunset\.jpg");
    }

    [Fact]
    public void Constructor_WithNullS3Client_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new IDriveE2Provider(null!, TestBucketName);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("s3Client");
    }

    [Fact]
    public void Constructor_WithNullBucketName_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new IDriveE2Provider(_mockS3Client.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("bucketName");
    }
}
