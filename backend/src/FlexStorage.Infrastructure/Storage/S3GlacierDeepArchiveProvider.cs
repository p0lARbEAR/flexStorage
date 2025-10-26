using Amazon.S3;
using Amazon.S3.Model;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.ValueObjects;
using System.Diagnostics;

namespace FlexStorage.Infrastructure.Storage;

/// <summary>
/// Storage provider for AWS S3 Glacier Deep Archive.
/// Lowest cost storage, 12-48 hour retrieval time.
/// </summary>
public class S3GlacierDeepArchiveProvider : IStorageProvider
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public S3GlacierDeepArchiveProvider(IAmazonS3 s3Client, string bucketName)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
    }

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

    public async Task<UploadResult> UploadAsync(
        Stream fileStream,
        UploadOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var key = GenerateS3Key(options.FileName ?? "file");

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = fileStream,
                ContentType = options.ContentType ?? "application/octet-stream",
                StorageClass = S3StorageClass.DeepArchive,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
            };

            // Add metadata if provided
            if (options.Metadata != null)
            {
                foreach (var (metaKey, metaValue) in options.Metadata)
                {
                    request.Metadata.Add(metaKey, metaValue);
                }
            }

            var response = await _s3Client.PutObjectAsync(request, cancellationToken);

            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
            {
                return new UploadResult
                {
                    Success = false,
                    ErrorMessage = $"S3 upload failed with status code: {response.HttpStatusCode}"
                };
            }

            var location = StorageLocation.Create(ProviderName, $"s3://{_bucketName}/{key}");

            return new UploadResult
            {
                Success = true,
                Location = location,
                UploadedAt = DateTime.UtcNow
            };
        }
        catch (AmazonS3Exception ex)
        {
            return new UploadResult
            {
                Success = false,
                ErrorMessage = $"S3 error: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new UploadResult
            {
                Success = false,
                ErrorMessage = $"Upload failed: {ex.Message}"
            };
        }
    }

    public async Task<Stream> DownloadAsync(
        StorageLocation location,
        CancellationToken cancellationToken)
    {
        var key = ExtractS3KeyFromLocation(location);

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
            var key = ExtractS3KeyFromLocation(location);

            var glacierTier = tier switch
            {
                RetrievalTier.Bulk => GlacierJobTier.Bulk,
                RetrievalTier.Standard => GlacierJobTier.Standard,
                RetrievalTier.Expedited => GlacierJobTier.Expedited,
                _ => GlacierJobTier.Standard
            };

            var request = new RestoreObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                Days = 1, // Keep restored object available for 1 day
                Tier = glacierTier
            };

            var response = await _s3Client.RestoreObjectAsync(request, cancellationToken);

            var estimatedTime = tier switch
            {
                RetrievalTier.Bulk => TimeSpan.FromHours(12),
                RetrievalTier.Standard => TimeSpan.FromHours(5),
                RetrievalTier.Expedited => TimeSpan.FromMinutes(5),
                _ => TimeSpan.FromHours(12)
            };

            var retrievalId = $"{ProviderName}-{Guid.NewGuid():N}";

            return new RetrievalResult
            {
                Success = true,
                RetrievalId = retrievalId,
                EstimatedCompletionTime = estimatedTime,
                Status = RetrievalStatus.InProgress
            };
        }
        catch (AmazonS3Exception ex)
        {
            return new RetrievalResult
            {
                Success = false,
                ErrorMessage = $"Failed to initiate retrieval: {ex.Message}"
            };
        }
    }

    public async Task<RetrievalStatusDetail> GetRetrievalStatusAsync(
        string retrievalId,
        CancellationToken cancellationToken)
    {
        // Note: For S3 Glacier, we would need to store the mapping of retrievalId to S3 key
        // in a database or cache. For now, this is a simplified implementation.
        // In production, you would query the restore status from S3.

        await Task.CompletedTask; // Placeholder for async signature

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
            var key = ExtractS3KeyFromLocation(location);

            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            await _s3Client.DeleteObjectAsync(request, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
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
                Message = $"S3 connection failed: {ex.Message}"
            };
        }
    }

    private string GenerateS3Key(string fileName)
    {
        var date = DateTime.UtcNow;
        var guid = Guid.NewGuid().ToString("N");
        var sanitizedFileName = SanitizeFileName(fileName);

        // Structure: files/YYYY/MM/DD/guid-filename
        return $"files/{date:yyyy}/{date:MM}/{date:dd}/{guid}-{sanitizedFileName}";
    }

    private string SanitizeFileName(string fileName)
    {
        // Remove path characters and special chars
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    private string ExtractS3KeyFromLocation(StorageLocation location)
    {
        // Format: s3://bucket-name/key
        var path = location.Path;
        var s3Prefix = "s3://";

        if (!path.StartsWith(s3Prefix))
            throw new ArgumentException($"Invalid S3 location format: {path}");

        // Remove s3://bucket-name/ to get just the key
        var withoutProtocol = path[s3Prefix.Length..];
        var slashIndex = withoutProtocol.IndexOf('/');

        if (slashIndex == -1)
            throw new ArgumentException($"Invalid S3 location format: {path}");

        return withoutProtocol[(slashIndex + 1)..];
    }
}
