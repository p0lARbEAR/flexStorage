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

public class S3GlacierFlexibleRetrievalProviderTests
{
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly S3GlacierFlexibleRetrievalProvider _provider;
    private const string TestBucketName = "test-bucket-flexible";

    public S3GlacierFlexibleRetrievalProviderTests()
    {
        _mockS3Client = new Mock<IAmazonS3>();
        _provider = new S3GlacierFlexibleRetrievalProvider(_mockS3Client.Object, TestBucketName);
    }

    [Fact]
    public void ProviderName_ShouldReturnCorrectName()
    {
        // Act
        var name = _provider.ProviderName;

        // Assert
        name.Should().Be("s3-glacier-flexible");
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
        capabilities.SupportsDeepArchive.Should().BeFalse();
        capabilities.SupportsFlexibleRetrieval.Should().BeTrue();
        capabilities.MinRetrievalTime.Should().Be(TimeSpan.FromHours(3));
        capabilities.MaxRetrievalTime.Should().Be(TimeSpan.FromHours(5));
    }

    [Fact]
    public async Task UploadAsync_WithValidFile_ShouldUploadToS3GlacierIR()
    {
        // Arrange
        var fileContent = "test file content for flexible retrieval";
        var fileStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
        var options = new UploadOptions
        {
            FileName = "flexible-test.txt",
            ContentType = "text/plain",
            Metadata = new Dictionary<string, string>
            {
                { "tier", "flexible" },
                { "priority", "medium" }
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
        result.Location!.ProviderName.Should().Be("s3-glacier-flexible");
        result.Location.Path.Should().StartWith($"s3://{TestBucketName}/");
        result.Location.Path.Should().Contain("flexible-test.txt");
        result.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify S3 call with GLACIER_IR storage class
        _mockS3Client.Verify(x => x.PutObjectAsync(
            It.Is<PutObjectRequest>(req => 
                req.BucketName == TestBucketName &&
                req.StorageClass == S3StorageClass.GlacierInstantRetrieval &&
                req.ContentType == "text/plain" &&
                req.Key.EndsWith("flexible-test.txt")),
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
            .ThrowsAsync(new AmazonS3Exception("S3 Glacier IR error"));

        // Act
        var result = await _provider.UploadAsync(fileStream, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Location.Should().BeNull();
        result.ErrorMessage.Should().Contain("S3 Glacier IR error");
    }

    [Fact]
    public async Task InitiateRetrievalAsync_WithStandardTier_ShouldInitiateGlacierRestore()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-flexible", $"s3://{TestBucketName}/test-file.txt");

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
        result.EstimatedCompletionTime.Should().Be(TimeSpan.FromHours(4)); // Standard tier for flexible

        // Verify S3 restore call
        _mockS3Client.Verify(x => x.RestoreObjectAsync(
            It.Is<RestoreObjectRequest>(req =>
                req.BucketName == TestBucketName &&
                req.Key == "test-file.txt" &&
                req.Days == 1 &&
                req.Tier == GlacierJobTier.Standard),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitiateRetrievalAsync_WithBulkTier_ShouldUseFasterEstimatedTime()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-flexible", $"s3://{TestBucketName}/test-file.txt");

        _mockS3Client
            .Setup(x => x.RestoreObjectAsync(It.IsAny<RestoreObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestoreObjectResponse());

        // Act
        var result = await _provider.InitiateRetrievalAsync(location, RetrievalTier.Bulk, CancellationToken.None);

        // Assert
        result.EstimatedCompletionTime.Should().Be(TimeSpan.FromHours(5)); // Bulk tier for flexible (slower than standard)
    }

    [Fact]
    public async Task InitiateRetrievalAsync_WithExpeditedTier_ShouldUseFastestEstimatedTime()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-flexible", $"s3://{TestBucketName}/test-file.txt");

        _mockS3Client
            .Setup(x => x.RestoreObjectAsync(It.IsAny<RestoreObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestoreObjectResponse());

        // Act
        var result = await _provider.InitiateRetrievalAsync(location, RetrievalTier.Expedited, CancellationToken.None);

        // Assert
        result.EstimatedCompletionTime.Should().Be(TimeSpan.FromMinutes(15)); // Expedited tier for flexible
    }

    [Fact]
    public async Task InitiateRetrievalAsync_WhenRestoreFails_ShouldReturnFailureResult()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-flexible", $"s3://{TestBucketName}/test-file.txt");

        _mockS3Client
            .Setup(x => x.RestoreObjectAsync(It.IsAny<RestoreObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Restore failed"));

        // Act
        var result = await _provider.InitiateRetrievalAsync(location, RetrievalTier.Standard, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Status.Should().Be(RetrievalStatus.Failed);
        result.ErrorMessage.Should().Contain("Restore failed");
    }

    [Fact]
    public async Task DownloadAsync_WithValidLocation_ShouldDownloadFromS3()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-flexible", $"s3://{TestBucketName}/flexible-file.txt");
        var expectedContent = "flexible retrieval file content";
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
                req.Key == "flexible-file.txt"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_WhenS3Fails_ShouldThrowException()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-flexible", $"s3://{TestBucketName}/nonexistent.txt");

        _mockS3Client
            .Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Object not found"));

        // Act & Assert
        await _provider.Invoking(p => p.DownloadAsync(location, CancellationToken.None))
            .Should().ThrowAsync<AmazonS3Exception>()
            .WithMessage("*Object not found*");
    }

    [Fact]
    public async Task DeleteAsync_WithValidLocation_ShouldDeleteFromS3()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-flexible", $"s3://{TestBucketName}/to-delete.txt");

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
                req.Key == "to-delete.txt"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenS3Fails_ShouldReturnFalse()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-flexible", $"s3://{TestBucketName}/nonexistent.txt");

        _mockS3Client
            .Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Delete failed"));

        // Act
        var result = await _provider.DeleteAsync(location, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
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
        result.Message.Should().Contain("S3 Glacier Flexible connection successful");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenS3IsUnhealthy_ShouldReturnUnhealthyStatus()
    {
        // Arrange
        _mockS3Client
            .Setup(x => x.ListBucketsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Connection timeout"));

        // Act
        var result = await _provider.CheckHealthAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsHealthy.Should().BeFalse();
        result.Message.Should().Contain("Connection timeout");
    }

    [Fact]
    public async Task GetRetrievalStatusAsync_ShouldReturnMockStatus()
    {
        // Arrange
        var retrievalId = "test-retrieval-123";

        // Act
        var result = await _provider.GetRetrievalStatusAsync(retrievalId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RetrievalId.Should().Be(retrievalId);
        result.Status.Should().Be(RetrievalStatus.InProgress);
        result.ProgressPercentage.Should().BeInRange(0, 100);
    }
}