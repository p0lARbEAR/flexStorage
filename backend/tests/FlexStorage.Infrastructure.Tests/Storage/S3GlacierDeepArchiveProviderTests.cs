using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.ValueObjects;
using FlexStorage.Infrastructure.Storage;
using Moq;
using Xunit;

namespace FlexStorage.Infrastructure.Tests.Storage;

public class S3GlacierDeepArchiveProviderTests
{
    private readonly Mock<IAmazonS3> _s3Client;
    private readonly S3GlacierDeepArchiveProvider _sut;
    private const string BucketName = "test-bucket";

    public S3GlacierDeepArchiveProviderTests()
    {
        _s3Client = new Mock<IAmazonS3>();
        _sut = new S3GlacierDeepArchiveProvider(_s3Client.Object, BucketName);
    }

    [Fact]
    public void ProviderName_ShouldReturnCorrectName()
    {
        // Act
        var name = _sut.ProviderName;

        // Assert
        name.Should().Be("s3-glacier-deep");
    }

    [Fact]
    public void Capabilities_ShouldIndicateDeepArchiveSupport()
    {
        // Act
        var capabilities = _sut.Capabilities;

        // Assert
        capabilities.SupportsDeepArchive.Should().BeTrue();
        capabilities.SupportsFlexibleRetrieval.Should().BeFalse();
        capabilities.SupportsRetrieval.Should().BeTrue();
        capabilities.SupportsInstantAccess.Should().BeFalse();
        capabilities.SupportsDeletion.Should().BeTrue();
    }

    [Fact]
    public async Task UploadAsync_ShouldUploadToS3WithDeepArchiveStorageClass()
    {
        // Arrange
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var options = new UploadOptions
        {
            FileName = "test.jpg",
            ContentType = "image/jpeg"
        };

        PutObjectRequest? capturedRequest = null;
        _s3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

        // Act
        var result = await _sut.UploadAsync(fileStream, options, default);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Location.Should().NotBeNull();
        result.Location!.ProviderName.Should().Be("s3-glacier-deep");
        result.Location.Path.Should().Contain("s3://");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.StorageClass.Should().Be(S3StorageClass.DeepArchive);
        capturedRequest.BucketName.Should().Be(BucketName);
        capturedRequest.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task UploadAsync_ShouldGenerateUniqueS3Key()
    {
        // Arrange
        var fileStream1 = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileStream2 = new MemoryStream(new byte[] { 4, 5, 6 });
        var options = new UploadOptions { FileName = "test.jpg" };

        var keys = new List<string>();
        _s3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => keys.Add(req.Key))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

        // Act
        await _sut.UploadAsync(fileStream1, options, default);
        await _sut.UploadAsync(fileStream2, options, default);

        // Assert
        keys.Should().HaveCount(2);
        keys[0].Should().NotBe(keys[1]);
        keys.Should().AllSatisfy(k => k.Should().Contain("/"));
    }

    [Fact]
    public async Task UploadAsync_WhenS3Fails_ShouldReturnFailureResult()
    {
        // Arrange
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var options = new UploadOptions { FileName = "test.jpg" };

        _s3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ThrowsAsync(new AmazonS3Exception("S3 error"));

        // Act
        var result = await _sut.UploadAsync(fileStream, options, default);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("S3 error");
        result.Location.Should().BeNull();
    }

    [Fact]
    public async Task DownloadAsync_ShouldRetrieveFileFromS3()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-deep", "s3://test-bucket/files/test.jpg");
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var responseStream = new MemoryStream(fileContent);

        _s3Client.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), default))
            .ReturnsAsync(new GetObjectResponse
            {
                ResponseStream = responseStream,
                HttpStatusCode = System.Net.HttpStatusCode.OK
            });

        // Act
        var result = await _sut.DownloadAsync(location, default);

        // Assert
        result.Should().NotBeNull();
        var buffer = new byte[fileContent.Length];
        await result.ReadAsync(buffer);
        buffer.Should().Equal(fileContent);
    }

    [Fact]
    public async Task InitiateRetrievalAsync_WithBulkTier_ShouldRequestBulkRetrieval()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-deep", "s3://test-bucket/files/test.jpg");
        var tier = RetrievalTier.Bulk;

        RestoreObjectRequest? capturedRequest = null;
        _s3Client.Setup(x => x.RestoreObjectAsync(It.IsAny<RestoreObjectRequest>(), default))
            .Callback<RestoreObjectRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new RestoreObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.Accepted });

        // Act
        var result = await _sut.InitiateRetrievalAsync(location, tier, default);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RetrievalId.Should().NotBeNullOrEmpty();
        result.Status.Should().Be(RetrievalStatus.InProgress);
        result.EstimatedCompletionTime.Should().BeGreaterThan(TimeSpan.FromHours(5));

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Tier.Should().Be(GlacierJobTier.Bulk);
    }

    [Fact]
    public async Task InitiateRetrievalAsync_WithStandardTier_ShouldRequestStandardRetrieval()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-deep", "s3://test-bucket/files/test.jpg");
        var tier = RetrievalTier.Standard;

        RestoreObjectRequest? capturedRequest = null;
        _s3Client.Setup(x => x.RestoreObjectAsync(It.IsAny<RestoreObjectRequest>(), default))
            .Callback<RestoreObjectRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new RestoreObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.Accepted });

        // Act
        var result = await _sut.InitiateRetrievalAsync(location, tier, default);

        // Assert
        result.Success.Should().BeTrue();
        result.EstimatedCompletionTime.Should().BeGreaterThan(TimeSpan.FromHours(3));
        result.EstimatedCompletionTime.Should().BeLessThan(TimeSpan.FromHours(6));

        capturedRequest!.Tier.Should().Be(GlacierJobTier.Standard);
    }

    [Fact]
    public async Task InitiateRetrievalAsync_WithExpeditedTier_ShouldRequestExpeditedRetrieval()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-deep", "s3://test-bucket/files/test.jpg");
        var tier = RetrievalTier.Expedited;

        _s3Client.Setup(x => x.RestoreObjectAsync(It.IsAny<RestoreObjectRequest>(), default))
            .ReturnsAsync(new RestoreObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.Accepted });

        // Act
        var result = await _sut.InitiateRetrievalAsync(location, tier, default);

        // Assert
        result.Success.Should().BeTrue();
        result.EstimatedCompletionTime.Should().BeLessThan(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteObjectFromS3()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-deep", "s3://test-bucket/files/test.jpg");

        _s3Client.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), default))
            .ReturnsAsync(new DeleteObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.NoContent });

        // Act
        var result = await _sut.DeleteAsync(location, default);

        // Assert
        result.Should().BeTrue();
        _s3Client.Verify(x => x.DeleteObjectAsync(
            It.Is<DeleteObjectRequest>(r => r.BucketName == BucketName),
            default), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenObjectNotFound_ShouldReturnFalse()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-deep", "s3://test-bucket/files/nonexistent.jpg");

        _s3Client.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), default))
            .ThrowsAsync(new AmazonS3Exception("Not found") { StatusCode = System.Net.HttpStatusCode.NotFound });

        // Act
        var result = await _sut.DeleteAsync(location, default);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldVerifyS3Connection()
    {
        // Arrange
        _s3Client.Setup(x => x.ListBucketsAsync(default))
            .ReturnsAsync(new ListBucketsResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

        // Act
        var result = await _sut.CheckHealthAsync(default);

        // Assert
        result.IsHealthy.Should().BeTrue();
        result.ResponseTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenS3Unavailable_ShouldReturnUnhealthy()
    {
        // Arrange
        _s3Client.Setup(x => x.ListBucketsAsync(default))
            .ThrowsAsync(new AmazonS3Exception("Service unavailable"));

        // Act
        var result = await _sut.CheckHealthAsync(default);

        // Assert
        result.IsHealthy.Should().BeFalse();
        result.Message.Should().Contain("Service unavailable");
    }
}
