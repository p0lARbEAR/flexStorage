using Amazon.S3;
using Amazon.S3.Model;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.ValueObjects;
using FlexStorage.Infrastructure.Storage;
using FluentAssertions;
using Xunit;

namespace FlexStorage.IntegrationTests;

/// <summary>
/// End-to-end integration tests with LocalStack S3.
/// These tests require LocalStack to be running (docker-compose up).
/// Run with: docker-compose up -d localstack
/// </summary>
[Collection("LocalStack")]
public class LocalStackS3IntegrationTests : IAsyncLifetime
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3GlacierDeepArchiveProvider _deepArchiveProvider;
    private readonly S3GlacierFlexibleRetrievalProvider _flexibleProvider;
    private const string TestBucketDeep = "test-deep-archive";
    private const string TestBucketFlexible = "test-flexible-retrieval";

    public LocalStackS3IntegrationTests()
    {
        // Configure S3 client for LocalStack
        var config = new AmazonS3Config
        {
            ServiceURL = "http://localhost:4566", // LocalStack endpoint
            ForcePathStyle = true, // Required for LocalStack
            AuthenticationRegion = "us-east-1"
        };

        _s3Client = new AmazonS3Client("test", "test", config);
        _deepArchiveProvider = new S3GlacierDeepArchiveProvider(_s3Client, TestBucketDeep);
        _flexibleProvider = new S3GlacierFlexibleRetrievalProvider(_s3Client, TestBucketFlexible);
    }

    public async Task InitializeAsync()
    {
        // Create test buckets
        try
        {
            await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = TestBucketDeep });
            await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = TestBucketFlexible });
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "BucketAlreadyOwnedByYou")
        {
            // Bucket already exists, that's fine
        }
    }

    public async Task DisposeAsync()
    {
        // Cleanup: Delete all objects and buckets
        try
        {
            await CleanupBucket(TestBucketDeep);
            await CleanupBucket(TestBucketFlexible);
        }
        catch
        {
            // Best effort cleanup
        }

        _s3Client?.Dispose();
    }

    private async Task CleanupBucket(string bucketName)
    {
        try
        {
            var listResponse = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucketName
            });

            foreach (var obj in listResponse.S3Objects)
            {
                await _s3Client.DeleteObjectAsync(bucketName, obj.Key);
            }

            await _s3Client.DeleteBucketAsync(bucketName);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact(Skip = "Requires LocalStack running - run manually with docker-compose up")]
    public async Task DeepArchiveProvider_EndToEnd_UploadDownloadDelete()
    {
        // Arrange
        var testContent = "Hello from Deep Archive E2E test!"u8.ToArray();
        var fileStream = new MemoryStream(testContent);
        var uploadOptions = new UploadOptions
        {
            FileName = "e2e-test-deep.txt",
            ContentType = "text/plain",
            Metadata = new Dictionary<string, string>
            {
                { "test-id", "e2e-deep-archive" },
                { "upload-time", DateTime.UtcNow.ToString("O") }
            }
        };

        // Act 1: Upload
        var uploadResult = await _deepArchiveProvider.UploadAsync(fileStream, uploadOptions, CancellationToken.None);

        // Assert 1: Upload successful
        uploadResult.Should().NotBeNull();
        uploadResult.Success.Should().BeTrue();
        uploadResult.Location.Should().NotBeNull();
        uploadResult.Location!.ProviderName.Should().Be("s3-glacier-deep");
        uploadResult.Location.Path.Should().StartWith($"s3://{TestBucketDeep}/");

        // Act 2: Download
        var downloadStream = await _deepArchiveProvider.DownloadAsync(uploadResult.Location, CancellationToken.None);

        // Assert 2: Download successful
        downloadStream.Should().NotBeNull();
        using var reader = new StreamReader(downloadStream);
        var downloadedContent = await reader.ReadToEndAsync();
        downloadedContent.Should().Be("Hello from Deep Archive E2E test!");

        // Act 3: Delete
        var deleteResult = await _deepArchiveProvider.DeleteAsync(uploadResult.Location, CancellationToken.None);

        // Assert 3: Delete successful
        deleteResult.Should().BeTrue();

        // Verify file is deleted
        var objectExists = await CheckObjectExists(TestBucketDeep, ExtractKeyFromLocation(uploadResult.Location));
        objectExists.Should().BeFalse();
    }

    [Fact(Skip = "Requires LocalStack running - run manually with docker-compose up")]
    public async Task FlexibleProvider_EndToEnd_UploadDownloadDelete()
    {
        // Arrange
        var testContent = "Hello from Flexible Retrieval E2E test!"u8.ToArray();
        var fileStream = new MemoryStream(testContent);
        var uploadOptions = new UploadOptions
        {
            FileName = "e2e-test-flexible.txt",
            ContentType = "text/plain"
        };

        // Act 1: Upload
        var uploadResult = await _flexibleProvider.UploadAsync(fileStream, uploadOptions, CancellationToken.None);

        // Assert 1: Upload successful
        uploadResult.Should().NotBeNull();
        uploadResult.Success.Should().BeTrue();
        uploadResult.Location.Should().NotBeNull();
        uploadResult.Location!.ProviderName.Should().Be("s3-glacier-flexible");

        // Act 2: Download
        var downloadStream = await _flexibleProvider.DownloadAsync(uploadResult.Location, CancellationToken.None);

        // Assert 2: Download successful
        using var reader = new StreamReader(downloadStream);
        var downloadedContent = await reader.ReadToEndAsync();
        downloadedContent.Should().Be("Hello from Flexible Retrieval E2E test!");

        // Act 3: Delete
        var deleteResult = await _flexibleProvider.DeleteAsync(uploadResult.Location, CancellationToken.None);

        // Assert 3: Delete successful
        deleteResult.Should().BeTrue();
    }

    [Fact(Skip = "Requires LocalStack running - run manually with docker-compose up")]
    public async Task BothProviders_ShouldWorkIndependently()
    {
        // Arrange
        var deepContent = "Deep Archive content"u8.ToArray();
        var flexContent = "Flexible content"u8.ToArray();
        var deepStream = new MemoryStream(deepContent);
        var flexStream = new MemoryStream(flexContent);

        // Act: Upload to both providers
        var deepUpload = await _deepArchiveProvider.UploadAsync(
            deepStream,
            new UploadOptions { FileName = "deep-test.txt" },
            CancellationToken.None);

        var flexUpload = await _flexibleProvider.UploadAsync(
            flexStream,
            new UploadOptions { FileName = "flex-test.txt" },
            CancellationToken.None);

        // Assert: Both uploads successful and stored in different buckets
        deepUpload.Success.Should().BeTrue();
        flexUpload.Success.Should().BeTrue();
        deepUpload.Location!.Path.Should().Contain(TestBucketDeep);
        flexUpload.Location!.Path.Should().Contain(TestBucketFlexible);

        // Cleanup
        await _deepArchiveProvider.DeleteAsync(deepUpload.Location!, CancellationToken.None);
        await _flexibleProvider.DeleteAsync(flexUpload.Location!, CancellationToken.None);
    }

    [Fact(Skip = "Requires LocalStack running - run manually with docker-compose up")]
    public async Task HealthCheck_ShouldVerifyLocalStackConnection()
    {
        // Act
        var deepHealth = await _deepArchiveProvider.CheckHealthAsync(CancellationToken.None);
        var flexHealth = await _flexibleProvider.CheckHealthAsync(CancellationToken.None);

        // Assert
        deepHealth.Should().NotBeNull();
        deepHealth.IsHealthy.Should().BeTrue();
        deepHealth.ResponseTime.Should().BeGreaterThan(TimeSpan.Zero);
        deepHealth.Message.Should().Contain("successful");

        flexHealth.Should().NotBeNull();
        flexHealth.IsHealthy.Should().BeTrue();
        flexHealth.ResponseTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact(Skip = "Requires LocalStack running - Glacier restore simulation")]
    public async Task DeepArchiveProvider_InitiateRetrieval_ShouldReturnJobId()
    {
        // Note: LocalStack may have limited Glacier simulation support
        // This test documents the expected behavior

        // Arrange
        var testContent = "Content for retrieval test"u8.ToArray();
        var fileStream = new MemoryStream(testContent);
        var uploadResult = await _deepArchiveProvider.UploadAsync(
            fileStream,
            new UploadOptions { FileName = "retrieval-test.txt" },
            CancellationToken.None);

        uploadResult.Success.Should().BeTrue();

        // Act: Initiate retrieval with Standard tier
        var retrievalResult = await _deepArchiveProvider.InitiateRetrievalAsync(
            uploadResult.Location!,
            RetrievalTier.Standard,
            CancellationToken.None);

        // Assert
        retrievalResult.Should().NotBeNull();
        retrievalResult.Success.Should().BeTrue();
        retrievalResult.RetrievalId.Should().NotBeNullOrEmpty();
        retrievalResult.Status.Should().Be(RetrievalStatus.InProgress);
        retrievalResult.EstimatedCompletionTime.Should().BeGreaterThan(TimeSpan.Zero);

        // Cleanup
        await _deepArchiveProvider.DeleteAsync(uploadResult.Location!, CancellationToken.None);
    }

    private async Task<bool> CheckObjectExists(string bucketName, string key)
    {
        try
        {
            await _s3Client.GetObjectMetadataAsync(bucketName, key);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private string ExtractKeyFromLocation(StorageLocation location)
    {
        // Extract key from s3://bucket/key format
        var s3Uri = location.Path;
        var pathPart = s3Uri["s3://".Length..];
        var slashIndex = pathPart.IndexOf('/');
        return pathPart[(slashIndex + 1)..];
    }
}
