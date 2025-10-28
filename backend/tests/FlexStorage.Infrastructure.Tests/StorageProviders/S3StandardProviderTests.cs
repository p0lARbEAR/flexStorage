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

public class S3StandardProviderTests
{
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly S3StandardProvider _provider;
    private const string TestBucketName = "test-thumbnails";

    public S3StandardProviderTests()
    {
        _mockS3Client = new Mock<IAmazonS3>();
        _provider = new S3StandardProvider(_mockS3Client.Object, TestBucketName);
    }

    [Fact]
    public void ProviderName_ShouldReturnCorrectName()
    {
        // Act
        var name = _provider.ProviderName;

        // Assert
        name.Should().Be("s3-standard");
    }

    [Fact]
    public void Capabilities_ShouldReturnCorrectCapabilities()
    {
        // Act
        var capabilities = _provider.Capabilities;

        // Assert
        capabilities.SupportsInstantAccess.Should().BeTrue();
        capabilities.SupportsRetrieval.Should().BeFalse(); // No retrieval needed
        capabilities.SupportsDeletion.Should().BeTrue();
        capabilities.SupportsDeepArchive.Should().BeFalse();
        capabilities.SupportsFlexibleRetrieval.Should().BeFalse();
        capabilities.MinRetrievalTime.Should().Be(TimeSpan.Zero);
        capabilities.MaxRetrievalTime.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task UploadAsync_WithValidFile_ShouldUploadToS3Standard()
    {
        // Arrange
        var fileContent = "thumbnail image data";
        var fileStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
        var options = new UploadOptions
        {
            FileName = "thumb_test.jpg",
            ContentType = "image/jpeg",
            Metadata = new Dictionary<string, string>
            {
                { "original-file-id", "123e4567-e89b-12d3-a456-426614174000" },
                { "thumbnail-size", "200x200" }
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
        result.Location!.ProviderName.Should().Be("s3-standard");
        result.Location.Path.Should().StartWith($"s3://{TestBucketName}/thumbnails/");
        result.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify S3 call with Standard storage class
        _mockS3Client.Verify(x => x.PutObjectAsync(
            It.Is<PutObjectRequest>(req =>
                req.BucketName == TestBucketName &&
                req.StorageClass == S3StorageClass.Standard &&
                req.ContentType == "image/jpeg" &&
                req.Key.StartsWith("thumbnails/") &&
                req.Metadata["x-amz-meta-original-file-id"] == "123e4567-e89b-12d3-a456-426614174000" &&
                req.Metadata["x-amz-meta-thumbnail-size"] == "200x200"),
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
            .ThrowsAsync(new AmazonS3Exception("S3 error"));

        // Act
        var result = await _provider.UploadAsync(fileStream, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("S3 error");
    }

    [Fact]
    public async Task DownloadAsync_WithValidLocation_ShouldReturnStream()
    {
        // Arrange
        var location = StorageLocation.Create("s3-standard", $"s3://{TestBucketName}/thumbnails/2025/01/28/abcd1234_test.jpg");
        var expectedContent = "thumbnail data";
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
                req.Key == "thumbnails/2025/01/28/abcd1234_test.jpg"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_WithException_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var location = StorageLocation.Create("s3-standard", $"s3://{TestBucketName}/thumbnails/test.jpg");

        _mockS3Client
            .Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Thumbnail not found"));

        // Act
        Func<Task> act = async () => await _provider.DownloadAsync(location, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to download from S3 Standard*");
    }

    [Fact]
    public async Task DeleteAsync_WithValidLocation_ShouldDeleteFromS3()
    {
        // Arrange
        var location = StorageLocation.Create("s3-standard", $"s3://{TestBucketName}/thumbnails/test.jpg");

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
                req.Key == "thumbnails/test.jpg"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WithException_ShouldReturnFalse()
    {
        // Arrange
        var location = StorageLocation.Create("s3-standard", $"s3://{TestBucketName}/thumbnails/test.jpg");

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
        var location = StorageLocation.Create("s3-standard", $"s3://{TestBucketName}/thumbnails/test.jpg");

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
    }

    [Fact]
    public async Task CheckHealthAsync_WhenUnhealthy_ShouldReturnUnhealthyStatus()
    {
        // Arrange
        _mockS3Client
            .Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Connection failed"));

        // Act
        var result = await _provider.CheckHealthAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsHealthy.Should().BeFalse();
        result.Message.Should().Contain("unhealthy");
        result.Message.Should().Contain("Connection failed");
    }

    [Fact]
    public async Task GenerateS3Key_ShouldCreateKeyWithDateAndUniqueId()
    {
        // Arrange
        var options = new UploadOptions
        {
            FileName = "test_image.jpg",
            ContentType = "image/jpeg"
        };

        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse());

        // Act
        var result1 = await _provider.UploadAsync(
            new MemoryStream(new byte[] { 1, 2, 3 }),
            options,
            CancellationToken.None);

        // Assert
        // Date format uses dashes: 2025-10-28 instead of slashes 2025/10/28
        result1.Location!.Path.Should().MatchRegex(@"s3://.+/thumbnails/\d{4}-\d{2}-\d{2}/[a-f0-9]{8}_test_image\.jpg");
    }
}
