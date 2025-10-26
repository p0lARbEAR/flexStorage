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

public class S3GlacierDeepArchiveProviderTests
{
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly S3GlacierDeepArchiveProvider _provider;
    private const string TestBucketName = "test-bucket";

    public S3GlacierDeepArchiveProviderTests()
    {
        _mockS3Client = new Mock<IAmazonS3>();
        _provider = new S3GlacierDeepArchiveProvider(_mockS3Client.Object, TestBucketName);
    }

    [Fact]
    public void ProviderName_ShouldReturnCorrectName()
    {
        // Act
        var name = _provider.ProviderName;

        // Assert
        name.Should().Be("s3-glacier-deep");
    }

    [Fact]
    public void Capabilities_ShouldReturnCorrectCapabilities()
    {
        // Act
        var capabilities = _provider.Capabilities;

        // Assert
        capabilities.SupportsInstantAccess.Should().BeFalse();
        capabilities.SupportsRetrieval.Should().BeTrue();
        capabilities.SupportsDeletion.Should().BeTrue();
        capabilities.SupportsDeepArchive.Should().BeTrue();
        capabilities.SupportsFlexibleRetrieval.Should().BeFalse();
        capabilities.MinRetrievalTime.Should().Be(TimeSpan.FromHours(12));
        capabilities.MaxRetrievalTime.Should().Be(TimeSpan.FromHours(48));
    }

    [Fact]
    public async Task UploadAsync_WithValidFile_ShouldUploadToS3DeepArchive()
    {
        // Arrange
        var fileContent = "test file content";
        var fileStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
        var options = new UploadOptions
        {
            FileName = "test.txt",
            ContentType = "text/plain"
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
        result.Location!.ProviderName.Should().Be("s3-glacier-deep");
        result.Location.Path.Should().StartWith($"s3://{TestBucketName}/");
        result.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify S3 call
        _mockS3Client.Verify(x => x.PutObjectAsync(
            It.Is<PutObjectRequest>(req =>
                req.BucketName == TestBucketName &&
                req.StorageClass == S3StorageClass.DeepArchive &&
                req.ContentType == "text/plain" &&
                req.Key.EndsWith("test.txt")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadAsync_WhenS3Fails_ShouldReturnFailureResult()
    {
        // Arrange
        var fileStream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var options = new UploadOptions { FileName = "test.txt" };

        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("S3 error"));

        // Act
        var result = await _provider.UploadAsync(fileStream, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Location.Should().BeNull();
        result.ErrorMessage.Should().Contain("S3 error");
    }

    [Fact]
    public async Task InitiateRetrievalAsync_WithValidLocation_ShouldInitiateGlacierRestore()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-deep", $"s3://{TestBucketName}/test-file.txt");

        _mockS3Client
            .Setup(x => x.RestoreObjectAsync(It.IsAny<RestoreObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestoreObjectResponse());

        // Act
        var result = await _provider.InitiateRetrievalAsync(location, RetrievalTier.Standard, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RetrievalId.Should().NotBeNullOrEmpty();
        result.Status.Should().Be(RetrievalStatus.InProgress);
        result.EstimatedCompletionTime.Should().Be(TimeSpan.FromHours(5)); // Standard tier

        // Verify S3 restore call
        _mockS3Client.Verify(x => x.RestoreObjectAsync(
            It.Is<RestoreObjectRequest>(req =>
                req.BucketName == TestBucketName &&
                req.Key == "test-file.txt" &&
                req.Days == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitiateRetrievalAsync_WithBulkTier_ShouldUseCorrectEstimatedTime()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-deep", $"s3://{TestBucketName}/test-file.txt");

        _mockS3Client
            .Setup(x => x.RestoreObjectAsync(It.IsAny<RestoreObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestoreObjectResponse());

        // Act
        var result = await _provider.InitiateRetrievalAsync(location, RetrievalTier.Bulk, CancellationToken.None);

        // Assert
        result.EstimatedCompletionTime.Should().Be(TimeSpan.FromHours(12)); // Bulk tier
    }

    [Fact]
    public async Task InitiateRetrievalAsync_WithExpeditedTier_ShouldUseCorrectEstimatedTime()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-deep", $"s3://{TestBucketName}/test-file.txt");

        _mockS3Client
            .Setup(x => x.RestoreObjectAsync(It.IsAny<RestoreObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestoreObjectResponse());

        // Act
        var result = await _provider.InitiateRetrievalAsync(location, RetrievalTier.Expedited, CancellationToken.None);

        // Assert
        result.EstimatedCompletionTime.Should().Be(TimeSpan.FromMinutes(5)); // Expedited tier
    }

    [Fact]
    public async Task DownloadAsync_WithValidLocation_ShouldDownloadFromS3()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-deep", $"s3://{TestBucketName}/test-file.txt");
        var expectedContent = "file content";
        var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(expectedContent));

        _mockS3Client
            .Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = responseStream });

        // Act
        var resultStream = await _provider.DownloadAsync(location, CancellationToken.None);

        // Assert
        resultStream.Should().NotBeNull();

        using var reader = new StreamReader(resultStream);
        var content = await reader.ReadToEndAsync();
        content.Should().Be(expectedContent);

        // Verify S3 call
        _mockS3Client.Verify(x => x.GetObjectAsync(
            It.Is<GetObjectRequest>(req =>
                req.BucketName == TestBucketName &&
                req.Key == "test-file.txt"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WithValidLocation_ShouldDeleteFromS3()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-deep", $"s3://{TestBucketName}/test-file.txt");

        _mockS3Client
            .Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse());

        // Act
        var result = await _provider.DeleteAsync(location, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        // Verify S3 call
        _mockS3Client.Verify(x => x.DeleteObjectAsync(
            It.Is<DeleteObjectRequest>(req =>
                req.BucketName == TestBucketName &&
                req.Key == "test-file.txt"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenS3IsHealthy_ShouldReturnHealthyStatus()
    {
        // Arrange
        _mockS3Client
            .Setup(x => x.ListBucketsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListBucketsResponse());

        // Act
        var result = await _provider.CheckHealthAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsHealthy.Should().BeTrue();
        result.ResponseTime.Should().BeGreaterThan(TimeSpan.Zero);
        result.Message.Should().Contain("S3 connection successful");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenS3IsUnhealthy_ShouldReturnUnhealthyStatus()
    {
        // Arrange
        _mockS3Client
            .Setup(x => x.ListBucketsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Connection failed"));

        // Act
        var result = await _provider.CheckHealthAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsHealthy.Should().BeFalse();
        result.Message.Should().Contain("Connection failed");
    }
}