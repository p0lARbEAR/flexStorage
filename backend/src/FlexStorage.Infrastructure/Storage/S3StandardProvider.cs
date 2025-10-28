using Amazon.S3;
using Amazon.S3.Model;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Infrastructure.Storage;

/// <summary>
/// Storage provider for AWS S3 Standard storage class.
/// Provides instant access with no retrieval time. Used for thumbnails.
/// </summary>
public class S3StandardProvider : IStorageProvider
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public string ProviderName => "s3-standard";

    public ProviderCapabilities Capabilities => new()
    {
        SupportsInstantAccess = true,
        SupportsRetrieval = false, // No retrieval needed - instant access
        SupportsDeletion = true,
        SupportsDeepArchive = false,
        SupportsFlexibleRetrieval = false,
        MinRetrievalTime = TimeSpan.Zero,
        MaxRetrievalTime = TimeSpan.Zero
    };

    public S3StandardProvider(IAmazonS3 s3Client, string bucketName)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
    }

    public async Task<UploadResult> UploadAsync(
        Stream fileStream,
        UploadOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var key = GenerateS3Key(options.FileName);

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = fileStream,
                StorageClass = S3StorageClass.Standard, // Standard storage for instant access
                ContentType = options.ContentType ?? "application/octet-stream"
            };

            // Add metadata if provided
            if (options.Metadata != null)
            {
                foreach (var (metaKey, metaValue) in options.Metadata)
                {
                    request.Metadata.Add(metaKey, metaValue);
                }
            }

            await _s3Client.PutObjectAsync(request, cancellationToken);

            var location = StorageLocation.Create(ProviderName, $"s3://{_bucketName}/{key}");

            return new UploadResult
            {
                Success = true,
                Location = location,
                UploadedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new UploadResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<Stream> DownloadAsync(StorageLocation location, CancellationToken cancellationToken)
    {
        try
        {
            var key = ExtractS3Key(location.Path);

            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            var response = await _s3Client.GetObjectAsync(request, cancellationToken);
            return response.ResponseStream;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to download from S3 Standard: {ex.Message}", ex);
        }
    }

    public async Task<bool> DeleteAsync(StorageLocation location, CancellationToken cancellationToken)
    {
        try
        {
            var key = ExtractS3Key(location.Path);

            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            await _s3Client.DeleteObjectAsync(request, cancellationToken);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public Task<RetrievalResult> InitiateRetrievalAsync(
        StorageLocation location,
        RetrievalTier tier,
        CancellationToken cancellationToken)
    {
        // S3 Standard doesn't need retrieval - instant access
        throw new NotSupportedException("S3 Standard storage class supports instant access and does not require retrieval.");
    }

    public Task<RetrievalStatusDetail> GetRetrievalStatusAsync(string retrievalId, CancellationToken cancellationToken)
    {
        // S3 Standard doesn't need retrieval
        throw new NotSupportedException("S3 Standard storage class supports instant access and does not require retrieval.");
    }

    public async Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Try to list objects in bucket to verify connectivity
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                MaxKeys = 1
            };

            await _s3Client.ListObjectsV2Async(request, cancellationToken);

            return new HealthStatus
            {
                IsHealthy = true,
                Message = $"S3 Standard provider healthy. Bucket: {_bucketName}"
            };
        }
        catch (Exception ex)
        {
            return new HealthStatus
            {
                IsHealthy = false,
                Message = $"S3 Standard provider unhealthy: {ex.Message}"
            };
        }
    }

    private string GenerateS3Key(string fileName)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy/MM/dd");
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var sanitizedName = SanitizeFileName(fileName);
        return $"thumbnails/{timestamp}/{uniqueId}_{sanitizedName}";
    }

    private string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
    }

    private string ExtractS3Key(string path)
    {
        // Path format: s3://bucket-name/key
        var uri = new Uri(path);
        return uri.AbsolutePath.TrimStart('/');
    }
}
