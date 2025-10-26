using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.ValueObjects;
using FlexStorage.Infrastructure.Storage;
using Moq;
using Xunit;

namespace FlexStorage.Infrastructure.Tests.Storage;

public class S3GlacierFlexibleRetrievalProviderTests
{
    private readonly Mock<IAmazonS3> _s3Client;
    private readonly S3GlacierFlexibleRetrievalProvider _sut;
    private const string BucketName = "test-bucket";

    public S3GlacierFlexibleRetrievalProviderTests()
    {
        _s3Client = new Mock<IAmazonS3>();
        _sut = new S3GlacierFlexibleRetrievalProvider(_s3Client.Object, BucketName);
    }

    [Fact]
    public void ProviderName_ShouldReturnCorrectName()
    {
        // Act
        var name = _sut.ProviderName;

        // Assert
        name.Should().Be("s3-glacier-flexible");
    }

    [Fact]
    public void Capabilities_ShouldIndicateFlexibleRetrievalSupport()
    {
        // Act
        var capabilities = _sut.Capabilities;

        // Assert
        capabilities.SupportsFlexibleRetrieval.Should().BeTrue();
        capabilities.SupportsDeepArchive.Should().BeFalse();
        capabilities.SupportsRetrieval.Should().BeTrue();
        capabilities.SupportsInstantAccess.Should().BeFalse();
        capabilities.SupportsDeletion.Should().BeTrue();
        capabilities.MinRetrievalTime.Should().Be(TimeSpan.FromHours(3));
        capabilities.MaxRetrievalTime.Should().Be(TimeSpan.FromHours(5));
    }

    [Fact]
    public async Task UploadAsync_ShouldUploadToS3WithGlacierIRStorageClass()
    {
        // Arrange
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var options = new UploadOptions
        {
            FileName = "video.mp4",
            ContentType = "video/mp4"
        };

        PutObjectRequest? capturedRequest = null;
        _s3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

        // Act
        var result = await _sut.UploadAsync(fileStream, options, default);

        // Assert
        result.Success.Should().BeTrue();
        result.Location!.ProviderName.Should().Be("s3-glacier-flexible");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.StorageClass.Should().Be(S3StorageClass.GlacierInstantRetrieval);
        capturedRequest.ContentType.Should().Be("video/mp4");
    }

    [Fact]
    public async Task InitiateRetrievalAsync_ShouldHaveFasterRetrievalTimes()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-flexible", "s3://test-bucket/files/video.mp4");

        _s3Client.Setup(x => x.RestoreObjectAsync(It.IsAny<RestoreObjectRequest>(), default))
            .ReturnsAsync(new RestoreObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.Accepted });

        // Act - Standard tier
        var resultStandard = await _sut.InitiateRetrievalAsync(location, RetrievalTier.Standard, default);

        // Assert - Should be faster than Deep Archive (3-5 hours vs 12-48 hours)
        resultStandard.Success.Should().BeTrue();
        resultStandard.EstimatedCompletionTime.Should().BeGreaterThan(TimeSpan.FromHours(3));
        resultStandard.EstimatedCompletionTime.Should().BeLessThan(TimeSpan.FromHours(6));
    }

    [Fact]
    public async Task InitiateRetrievalAsync_WithBulkTier_ShouldStillBeFaster()
    {
        // Arrange
        var location = StorageLocation.Create("s3-glacier-flexible", "s3://test-bucket/files/video.mp4");

        _s3Client.Setup(x => x.RestoreObjectAsync(It.IsAny<RestoreObjectRequest>(), default))
            .ReturnsAsync(new RestoreObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.Accepted });

        // Act - Bulk tier
        var result = await _sut.InitiateRetrievalAsync(location, RetrievalTier.Bulk, default);

        // Assert - Even bulk should be faster than Deep Archive standard
        result.Success.Should().BeTrue();
        result.EstimatedCompletionTime.Should().BeGreaterThan(TimeSpan.FromHours(3));
        result.EstimatedCompletionTime.Should().BeLessThan(TimeSpan.FromHours(10));
    }
}
