using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Infrastructure.Storage;

/// <summary>
/// Storage provider for AWS S3 Glacier Deep Archive.
/// Provides lowest cost storage with 12-48 hour retrieval time.
/// </summary>
public class S3GlacierDeepArchiveProvider : IStorageProvider
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public string ProviderName => "s3-glacier-deep";

    public ProviderCapabilities Capabilities => new()
    {
        SupportsInstantAccess = false,
        SupportsRetrieval = true,
        SupportsDeletion = true,
        SupportsDeepArchive = true,
        SupportsFlexibleRetrieval = false,
        MinRetrievalTime = TimeSpan.FromHours(12),
        MaxRetrievalTime = TimeSpan.FromHours(48)
    };

    public S3GlacierDeepArchiveProvider(IAmazonS3 s3Client, string bucketName)
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
                StorageClass = S3StorageClass.DeepArchive,
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

    public async Task<Stream> DownloadAsync(
        StorageLocation location,
        CancellationToken cancellationToken)
    {
        var key = ExtractS3Key(location);

        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };

        var response = await _s3Client.GetObjectAsync(request, cancellationToken);
        return response.ResponseStream;
    }

    public async Task<RetrievalResult> InitiateRetrievalAsync(
        StorageLocation location,
        RetrievalTier tier,
        CancellationToken cancellationToken)
    {
        try
        {
            var key = ExtractS3Key(location);
            var retrievalId = Guid.NewGuid().ToString();

            var request = new RestoreObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                Days = 1, // Keep restored copy for 1 day
                Tier = MapRetrievalTier(tier)
            };

            await _s3Client.RestoreObjectAsync(request, cancellationToken);

            var estimatedTime = tier switch
            {
                RetrievalTier.Bulk => TimeSpan.FromHours(12),
                RetrievalTier.Standard => TimeSpan.FromHours(5),
                RetrievalTier.Expedited => TimeSpan.FromMinutes(5),
                _ => TimeSpan.FromHours(12)
            };

            return new RetrievalResult
            {
                Success = true,
                RetrievalId = retrievalId,
                EstimatedCompletionTime = estimatedTime,
                Status = RetrievalStatus.InProgress
            };
        }
        catch (Exception ex)
        {
            return new RetrievalResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Status = RetrievalStatus.Failed
            };
        }
    }

    public async Task<RetrievalStatusDetail> GetRetrievalStatusAsync(
        string retrievalId,
        CancellationToken cancellationToken)
    {
        // Note: In a real implementation, we would store retrieval requests in a database
        // and track their status. For now, we'll return a mock status.
        await Task.Delay(1, cancellationToken); // Simulate async operation

        return new RetrievalStatusDetail
        {
            RetrievalId = retrievalId,
            Status = RetrievalStatus.InProgress,
            ProgressPercentage = 50
        };
    }

    public async Task<bool> DeleteAsync(
        StorageLocation location,
        CancellationToken cancellationToken)
    {
        try
        {
            var key = ExtractS3Key(location);

            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            await _s3Client.DeleteObjectAsync(request, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Simple health check - list buckets to verify connection
            await _s3Client.ListBucketsAsync(cancellationToken);
            stopwatch.Stop();

            return new HealthStatus
            {
                IsHealthy = true,
                ResponseTime = stopwatch.Elapsed,
                Message = "S3 connection successful"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new HealthStatus
            {
                IsHealthy = false,
                ResponseTime = stopwatch.Elapsed,
                Message = ex.Message
            };
        }
    }

    private string GenerateS3Key(string? fileName)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy/MM/dd");
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var sanitizedFileName = fileName ?? "file";

        return $"{timestamp}/{uniqueId}_{sanitizedFileName}";
    }

    private string ExtractS3Key(StorageLocation location)
    {
        // Extract key from s3://bucket/key format
        var s3Uri = location.Path;
        if (!s3Uri.StartsWith("s3://"))
            throw new ArgumentException($"Invalid S3 URI format: {s3Uri}");

        var pathPart = s3Uri["s3://".Length..];
        var slashIndex = pathPart.IndexOf('/');
        if (slashIndex == -1)
            throw new ArgumentException($"Invalid S3 URI format: {s3Uri}");

        return pathPart[(slashIndex + 1)..];
    }

    private static GlacierJobTier MapRetrievalTier(RetrievalTier tier)
    {
        return tier switch
        {
            RetrievalTier.Bulk => GlacierJobTier.Bulk,
            RetrievalTier.Standard => GlacierJobTier.Standard,
            RetrievalTier.Expedited => GlacierJobTier.Expedited,
            _ => GlacierJobTier.Standard
        };
    }
}