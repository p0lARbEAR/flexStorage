# FlexStorage Plugin Development Guide

**Version:** 1.0.0
**Purpose:** Guide for developing custom storage provider plugins
**Audience:** Third-party developers, contributors

---

## Overview

FlexStorage uses a plugin architecture that allows anyone to add support for additional storage providers without modifying the core codebase. This guide explains how to create your own storage provider plugin.

## Why Create a Plugin?

- **Add your preferred storage service** (Cloudflare R2, Wasabi, Google Cloud Storage, etc.)
- **Support custom S3-compatible storage**
- **Integrate with on-premises storage solutions**
- **Experiment with new storage technologies**
- **Contribute back to the community**

---

## Quick Start

### 1. Create a New .NET Class Library

```bash
dotnet new classlib -n FlexStorage.Provider.MyProvider
cd FlexStorage.Provider.MyProvider
dotnet add reference ../FlexStorage.Domain/FlexStorage.Domain.csproj
```

### 2. Implement the IStorageProvider Interface

```csharp
using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Provider.MyProvider
{
    public class MyStorageProvider : IStorageProvider
    {
        public string ProviderName => "My Custom Storage";

        public ProviderCapabilities Capabilities => new()
        {
            SupportsInstantAccess = true,
            SupportsRetrieval = false, // No delay for access
            SupportsDeletion = true,
            MinRetrievalTime = TimeSpan.Zero,
            MaxRetrievalTime = TimeSpan.Zero
        };

        public async Task<UploadResult> UploadAsync(
            Stream fileStream,
            UploadOptions options,
            CancellationToken cancellationToken)
        {
            // Your upload logic here
            throw new NotImplementedException();
        }

        public async Task<Stream> DownloadAsync(
            StorageLocation location,
            CancellationToken cancellationToken)
        {
            // Your download logic here
            throw new NotImplementedException();
        }

        public async Task<bool> DeleteAsync(
            StorageLocation location,
            CancellationToken cancellationToken)
        {
            // Your delete logic here
            throw new NotImplementedException();
        }

        public async Task<RetrievalResult> InitiateRetrievalAsync(
            StorageLocation location,
            RetrievalTier tier,
            CancellationToken cancellationToken)
        {
            // If your provider doesn't need retrieval (instant access),
            // return immediately ready status
            return new RetrievalResult
            {
                RetrievalId = Guid.NewGuid().ToString(),
                EstimatedCompletionTime = TimeSpan.Zero,
                Status = RetrievalStatus.Ready
            };
        }

        public async Task<RetrievalStatus> GetRetrievalStatusAsync(
            string retrievalId,
            CancellationToken cancellationToken)
        {
            // Return retrieval status
            throw new NotImplementedException();
        }

        public async Task<HealthStatus> CheckHealthAsync(
            CancellationToken cancellationToken)
        {
            // Check if your service is accessible
            try
            {
                // Perform a lightweight operation (e.g., list buckets, ping endpoint)
                return new HealthStatus
                {
                    IsHealthy = true,
                    ResponseTime = TimeSpan.FromMilliseconds(50),
                    Message = "Provider is healthy"
                };
            }
            catch (Exception ex)
            {
                return new HealthStatus
                {
                    IsHealthy = false,
                    Message = $"Health check failed: {ex.Message}"
                };
            }
        }
    }
}
```

### 3. Build Your Plugin

```bash
dotnet build -c Release
```

### 4. Deploy Your Plugin

Copy the compiled DLL to the FlexStorage plugins directory:

```bash
cp bin/Release/net8.0/FlexStorage.Provider.MyProvider.dll \
   /path/to/flexstorage/plugins/
```

### 5. Configure Your Plugin

Add configuration to `appsettings.json`:

```json
{
  "StorageProviders": {
    "MyCustomStorage": {
      "Enabled": true,
      "AssemblyName": "FlexStorage.Provider.MyProvider",
      "TypeName": "FlexStorage.Provider.MyProvider.MyStorageProvider",
      "MaxRequestsPerSecond": 100,
      "Config": {
        "ApiKey": "your-api-key",
        "Endpoint": "https://api.mystorage.com",
        "BucketName": "my-bucket"
      }
    }
  }
}
```

---

## IStorageProvider Interface Reference

### Methods

#### UploadAsync

```csharp
Task<UploadResult> UploadAsync(
    Stream fileStream,
    UploadOptions options,
    CancellationToken cancellationToken)
```

**Purpose:** Upload a file to your storage service

**Parameters:**
- `fileStream`: The file content as a stream
- `options`: Upload options (filename, content type, metadata)
- `cancellationToken`: For cancellation support

**Returns:** `UploadResult` with:
- `Location`: `StorageLocation` object (provider name + path)
- `UploadedAt`: Timestamp of upload completion

**Responsibilities:**
- Upload the file to your storage
- Generate a unique storage path/key
- Handle errors and retry transient failures
- Return a `StorageLocation` that can be used to retrieve the file later

**Example:**

```csharp
public async Task<UploadResult> UploadAsync(
    Stream fileStream,
    UploadOptions options,
    CancellationToken cancellationToken)
{
    var fileName = options.FileName ?? Guid.NewGuid().ToString();
    var key = $"{DateTime.UtcNow:yyyy/MM/dd}/{fileName}";

    // Upload to your service
    await _myStorageClient.UploadAsync(key, fileStream, cancellationToken);

    return new UploadResult
    {
        Location = StorageLocation.Create(ProviderName, $"mystorage://{key}"),
        UploadedAt = DateTime.UtcNow
    };
}
```

---

#### DownloadAsync

```csharp
Task<Stream> DownloadAsync(
    StorageLocation location,
    CancellationToken cancellationToken)
```

**Purpose:** Download a file from your storage service

**Parameters:**
- `location`: The `StorageLocation` returned from upload
- `cancellationToken`: For cancellation support

**Returns:** `Stream` containing the file data

**Responsibilities:**
- Parse the storage location to extract your internal key/path
- Download the file from your storage
- Return the file as a stream
- Handle cases where file doesn't exist (throw `FileNotFoundException`)

**Example:**

```csharp
public async Task<Stream> DownloadAsync(
    StorageLocation location,
    CancellationToken cancellationToken)
{
    var key = ExtractKeyFromLocation(location); // mystorage://2025/10/25/file.jpg → 2025/10/25/file.jpg

    try
    {
        var stream = await _myStorageClient.DownloadAsync(key, cancellationToken);
        return stream;
    }
    catch (ObjectNotFoundException)
    {
        throw new FileNotFoundException($"File not found at {location}");
    }
}

private string ExtractKeyFromLocation(StorageLocation location)
{
    // Remove your provider prefix (e.g., "mystorage://")
    return location.Path.Replace("mystorage://", "");
}
```

---

#### DeleteAsync

```csharp
Task<bool> DeleteAsync(
    StorageLocation location,
    CancellationToken cancellationToken)
```

**Purpose:** Delete a file from your storage service

**Parameters:**
- `location`: The `StorageLocation` of the file to delete
- `cancellationToken`: For cancellation support

**Returns:** `true` if deleted, `false` if file didn't exist

**Responsibilities:**
- Delete the file from your storage
- Return `false` if file doesn't exist (not an error)
- Handle errors appropriately

**Example:**

```csharp
public async Task<bool> DeleteAsync(
    StorageLocation location,
    CancellationToken cancellationToken)
{
    var key = ExtractKeyFromLocation(location);

    try
    {
        await _myStorageClient.DeleteAsync(key, cancellationToken);
        return true;
    }
    catch (ObjectNotFoundException)
    {
        return false; // File didn't exist
    }
}
```

---

#### InitiateRetrievalAsync (For Cold Storage)

```csharp
Task<RetrievalResult> InitiateRetrievalAsync(
    StorageLocation location,
    RetrievalTier tier,
    CancellationToken cancellationToken)
```

**Purpose:** Initiate retrieval for cold/archived files (like Glacier)

**When to Implement:**
- Your storage has a "cold" tier that requires retrieval before download
- For instant-access storage, return immediately ready status

**Example (Instant Access):**

```csharp
public async Task<RetrievalResult> InitiateRetrievalAsync(
    StorageLocation location,
    RetrievalTier tier,
    CancellationToken cancellationToken)
{
    // No retrieval needed - files are instantly accessible
    return new RetrievalResult
    {
        RetrievalId = Guid.NewGuid().ToString(),
        EstimatedCompletionTime = TimeSpan.Zero,
        Status = RetrievalStatus.Ready
    };
}
```

**Example (Cold Storage):**

```csharp
public async Task<RetrievalResult> InitiateRetrievalAsync(
    StorageLocation location,
    RetrievalTier tier,
    CancellationToken cancellationToken)
{
    var key = ExtractKeyFromLocation(location);

    var jobId = await _myStorageClient.RequestRetrievalAsync(key, tier);

    var estimatedTime = tier switch
    {
        RetrievalTier.Bulk => TimeSpan.FromHours(12),
        RetrievalTier.Standard => TimeSpan.FromHours(5),
        RetrievalTier.Expedited => TimeSpan.FromMinutes(5),
        _ => TimeSpan.FromHours(5)
    };

    return new RetrievalResult
    {
        RetrievalId = jobId,
        EstimatedCompletionTime = estimatedTime,
        Status = RetrievalStatus.Requested
    };
}
```

---

#### CheckHealthAsync

```csharp
Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken)
```

**Purpose:** Verify that your storage service is accessible

**Returns:** `HealthStatus` with:
- `IsHealthy`: `true` if service is accessible
- `ResponseTime`: How long the check took
- `Message`: Details about health status

**Example:**

```csharp
public async Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken)
{
    var stopwatch = Stopwatch.StartNew();

    try
    {
        // Perform lightweight check (e.g., list buckets, ping endpoint)
        await _myStorageClient.PingAsync(cancellationToken);

        stopwatch.Stop();

        return new HealthStatus
        {
            IsHealthy = true,
            ResponseTime = stopwatch.Elapsed,
            Message = $"{ProviderName} is healthy"
        };
    }
    catch (Exception ex)
    {
        stopwatch.Stop();

        return new HealthStatus
        {
            IsHealthy = false,
            ResponseTime = stopwatch.Elapsed,
            Message = $"Health check failed: {ex.Message}"
        };
    }
}
```

---

## Best Practices

### 1. Configuration Management

Use dependency injection to receive configuration:

```csharp
public class MyStorageProvider : IStorageProvider
{
    private readonly MyStorageOptions _options;
    private readonly ILogger<MyStorageProvider> _logger;

    public MyStorageProvider(
        IOptions<MyStorageOptions> options,
        ILogger<MyStorageProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }
}

public class MyStorageOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
}
```

### 2. Error Handling

Handle errors gracefully and provide useful messages:

```csharp
public async Task<UploadResult> UploadAsync(...)
{
    try
    {
        // Upload logic
    }
    catch (AuthenticationException ex)
    {
        _logger.LogError(ex, "Authentication failed for {Provider}", ProviderName);
        throw new StorageProviderException($"Authentication failed: {ex.Message}", ex);
    }
    catch (NetworkException ex)
    {
        _logger.LogError(ex, "Network error while uploading to {Provider}", ProviderName);
        throw new StorageProviderException($"Network error: {ex.Message}", ex);
    }
}
```

### 3. Retry Logic

Use Polly for retry policies:

```csharp
private readonly AsyncRetryPolicy _retryPolicy;

public MyStorageProvider(...)
{
    _retryPolicy = Policy
        .Handle<HttpRequestException>()
        .Or<TimeoutException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            onRetry: (exception, timeSpan, retryCount, context) =>
            {
                _logger.LogWarning(
                    "Retry {RetryCount} after {Delay}s due to {Exception}",
                    retryCount, timeSpan.TotalSeconds, exception.GetType().Name);
            });
}

public async Task<UploadResult> UploadAsync(...)
{
    return await _retryPolicy.ExecuteAsync(async () =>
    {
        // Your upload logic
    });
}
```

### 4. Logging

Log important operations:

```csharp
public async Task<UploadResult> UploadAsync(...)
{
    _logger.LogInformation(
        "Uploading file {FileName} ({Size} bytes) to {Provider}",
        options.FileName, fileStream.Length, ProviderName);

    var result = await /* upload */;

    _logger.LogInformation(
        "Successfully uploaded {FileName} to {Location}",
        options.FileName, result.Location);

    return result;
}
```

### 5. Cancellation Support

Respect cancellation tokens:

```csharp
public async Task<UploadResult> UploadAsync(
    Stream fileStream,
    UploadOptions options,
    CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();

    // Pass cancellationToken to all async operations
    await _httpClient.PostAsync(url, content, cancellationToken);
}
```

---

## Example: Cloudflare R2 Provider

Here's a complete example for Cloudflare R2 (S3-compatible):

```csharp
using Amazon.S3;
using Amazon.S3.Model;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlexStorage.Provider.CloudflareR2
{
    public class CloudflareR2Provider : IStorageProvider
    {
        private readonly IAmazonS3 _s3Client;
        private readonly CloudflareR2Options _options;
        private readonly ILogger<CloudflareR2Provider> _logger;

        public string ProviderName => "Cloudflare R2";

        public ProviderCapabilities Capabilities => new()
        {
            SupportsInstantAccess = true,
            SupportsRetrieval = false,
            SupportsDeletion = true,
            MinRetrievalTime = TimeSpan.Zero,
            MaxRetrievalTime = TimeSpan.Zero
        };

        public CloudflareR2Provider(
            IOptions<CloudflareR2Options> options,
            ILogger<CloudflareR2Provider> logger)
        {
            _options = options.Value;
            _logger = logger;

            // Configure S3 client for Cloudflare R2
            var config = new AmazonS3Config
            {
                ServiceURL = _options.Endpoint, // e.g., https://<account-id>.r2.cloudflarestorage.com
                ForcePathStyle = true
            };

            _s3Client = new AmazonS3Client(
                _options.AccessKeyId,
                _options.SecretAccessKey,
                config);
        }

        public async Task<UploadResult> UploadAsync(
            Stream fileStream,
            UploadOptions options,
            CancellationToken cancellationToken)
        {
            var key = GenerateKey(options.FileName);

            _logger.LogInformation("Uploading {FileName} to Cloudflare R2", options.FileName);

            var request = new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = key,
                InputStream = fileStream,
                ContentType = options.ContentType
            };

            await _s3Client.PutObjectAsync(request, cancellationToken);

            var location = StorageLocation.Create(
                ProviderName,
                $"r2://{_options.BucketName}/{key}");

            return new UploadResult
            {
                Location = location,
                UploadedAt = DateTime.UtcNow
            };
        }

        public async Task<Stream> DownloadAsync(
            StorageLocation location,
            CancellationToken cancellationToken)
        {
            var key = ExtractKey(location);

            var request = new GetObjectRequest
            {
                BucketName = _options.BucketName,
                Key = key
            };

            var response = await _s3Client.GetObjectAsync(request, cancellationToken);
            return response.ResponseStream;
        }

        public async Task<bool> DeleteAsync(
            StorageLocation location,
            CancellationToken cancellationToken)
        {
            var key = ExtractKey(location);

            try
            {
                await _s3Client.DeleteObjectAsync(
                    _options.BucketName,
                    key,
                    cancellationToken);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public Task<RetrievalResult> InitiateRetrievalAsync(
            StorageLocation location,
            RetrievalTier tier,
            CancellationToken cancellationToken)
        {
            // R2 has instant access - no retrieval needed
            return Task.FromResult(new RetrievalResult
            {
                RetrievalId = Guid.NewGuid().ToString(),
                EstimatedCompletionTime = TimeSpan.Zero,
                Status = RetrievalStatus.Ready
            });
        }

        public Task<RetrievalStatus> GetRetrievalStatusAsync(
            string retrievalId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(RetrievalStatus.Ready);
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
                    Message = "Cloudflare R2 is healthy"
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                return new HealthStatus
                {
                    IsHealthy = false,
                    ResponseTime = stopwatch.Elapsed,
                    Message = $"Health check failed: {ex.Message}"
                };
            }
        }

        private string GenerateKey(string fileName)
        {
            var sanitized = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            var timestamp = DateTime.UtcNow;

            return $"{timestamp:yyyy/MM/dd}/{Guid.NewGuid()}{extension}";
        }

        private string ExtractKey(StorageLocation location)
        {
            // r2://bucket-name/2025/10/25/guid.jpg → 2025/10/25/guid.jpg
            var path = location.Path;
            var bucketPrefix = $"r2://{_options.BucketName}/";
            return path.Replace(bucketPrefix, "");
        }
    }

    public class CloudflareR2Options
    {
        public string AccessKeyId { get; set; } = string.Empty;
        public string SecretAccessKey { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
    }
}
```

---

## Testing Your Plugin

### Unit Tests

```csharp
public class MyStorageProviderTests
{
    [Fact]
    public async Task UploadAsync_ShouldUploadFile_AndReturnLocation()
    {
        // Arrange
        var options = Options.Create(new MyStorageOptions { /* config */ });
        var logger = new Mock<ILogger<MyStorageProvider>>();
        var provider = new MyStorageProvider(options, logger.Object);

        var fileStream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));
        var uploadOptions = new UploadOptions
        {
            FileName = "test.txt",
            ContentType = "text/plain"
        };

        // Act
        var result = await provider.UploadAsync(fileStream, uploadOptions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Location.Should().NotBeNull();
        result.Location.ProviderName.Should().Be("My Custom Storage");
    }
}
```

### Integration Tests

Test against real storage service (or use Testcontainers for S3-compatible services):

```csharp
public class MyStorageProviderIntegrationTests : IAsyncLifetime
{
    private MyStorageProvider _provider;

    public async Task InitializeAsync()
    {
        // Setup real provider
        _provider = new MyStorageProvider(/* real config */);
    }

    [Fact]
    public async Task UploadAndDownload_ShouldWork()
    {
        // Upload
        var content = "Test content";
        var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var uploadResult = await _provider.UploadAsync(uploadStream, new UploadOptions { FileName = "test.txt" }, CancellationToken.None);

        // Download
        var downloadStream = await _provider.DownloadAsync(uploadResult.Location, CancellationToken.None);
        var downloadedContent = await new StreamReader(downloadStream).ReadToEndAsync();

        // Assert
        downloadedContent.Should().Be(content);

        // Cleanup
        await _provider.DeleteAsync(uploadResult.Location, CancellationToken.None);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
```

---

## Publishing Your Plugin

### 1. NuGet Package (Recommended)

```bash
dotnet pack -c Release
dotnet nuget push bin/Release/FlexStorage.Provider.MyProvider.1.0.0.nupkg --source https://api.nuget.org/v3/index.json
```

Users can then install via:

```bash
dotnet add package FlexStorage.Provider.MyProvider
```

### 2. GitHub Release

1. Create a GitHub repository for your plugin
2. Create a release with the compiled DLL
3. Users download and place in `/plugins` directory

### 3. Documentation

Include a README with:
- What storage service it supports
- Configuration options
- Example appsettings.json
- Any prerequisites (API keys, account setup)

---

## Example Plugins to Study

- **FlexStorage.Provider.S3GlacierDeep** - AWS Glacier Deep Archive (included in core)
- **FlexStorage.Provider.S3GlacierFlex** - AWS Glacier Flexible Retrieval (included in core)
- **FlexStorage.Provider.Backblaze** - Backblaze B2 (included in core)

---

## FAQ

**Q: Do I need to modify FlexStorage core code to add my plugin?**
A: No! Just implement `IStorageProvider`, build your DLL, and place it in the `/plugins` directory.

**Q: Can I use external NuGet packages in my plugin?**
A: Yes, but ensure they're compatible with .NET 8 and won't conflict with FlexStorage dependencies.

**Q: How does plugin discovery work?**
A: FlexStorage scans the `/plugins` directory on startup, loads all assemblies, and looks for types implementing `IStorageProvider`.

**Q: Can I contribute my plugin to the core project?**
A: Absolutely! Submit a PR to the main repository. We welcome high-quality providers.

**Q: What if my storage service has unique features?**
A: You can extend `IStorageProvider` in your plugin, but core FlexStorage will only use the base interface methods.

---

## Support

- **Issues:** https://github.com/your-org/flexstorage/issues
- **Discussions:** https://github.com/your-org/flexstorage/discussions
- **Discord:** [Coming soon]

---

**Last Updated:** 2025-10-25
**Document Owner:** FlexStorage Community
