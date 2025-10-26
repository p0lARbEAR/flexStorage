using Amazon.S3;
using Microsoft.AspNetCore.Mvc;

namespace FlexStorage.API.Controllers;

/// <summary>
/// Test controller for debugging LocalStack connectivity.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<TestController> _logger;

    public TestController(IAmazonS3 s3Client, ILogger<TestController> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
    }

    /// <summary>
    /// Test S3 connection by listing buckets.
    /// </summary>
    [HttpGet("s3-connection")]
    public async Task<IActionResult> TestS3Connection()
    {
        try
        {
            _logger.LogInformation("Testing S3 connection...");
            
            var response = await _s3Client.ListBucketsAsync();
            
            var buckets = response.Buckets.Select(b => new
            {
                Name = b.BucketName,
                CreationDate = b.CreationDate
            }).ToList();

            _logger.LogInformation("S3 connection successful. Found {BucketCount} buckets", buckets.Count);

            return Ok(new
            {
                success = true,
                message = "S3 connection successful",
                buckets = buckets,
                endpoint = _s3Client.Config.ServiceURL
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "S3 connection failed");
            
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                endpoint = _s3Client.Config.ServiceURL
            });
        }
    }

    /// <summary>
    /// Test uploading a simple file to S3.
    /// </summary>
    [HttpPost("s3-upload-test")]
    public async Task<IActionResult> TestS3Upload()
    {
        try
        {
            var bucketName = "flexstorage-deep-archive";
            var key = $"test-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
            var content = "This is a test file from FlexStorage API";

            _logger.LogInformation("Testing S3 upload to bucket {BucketName} with key {Key}", bucketName, key);

            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            
            var request = new Amazon.S3.Model.PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                InputStream = stream,
                ContentType = "text/plain"
            };

            var response = await _s3Client.PutObjectAsync(request);

            _logger.LogInformation("S3 upload successful. ETag: {ETag}", response.ETag);

            return Ok(new
            {
                success = true,
                message = "S3 upload successful",
                bucket = bucketName,
                key = key,
                etag = response.ETag,
                endpoint = _s3Client.Config.ServiceURL
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "S3 upload failed");
            
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                endpoint = _s3Client.Config.ServiceURL
            });
        }
    }
}