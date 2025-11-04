using FlexStorage.API.Constants;
using FlexStorage.API.Models.Requests;
using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace FlexStorage.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IFileUploadService _fileUploadService;
    private readonly IFileRetrievalService _fileRetrievalService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IFileUploadService fileUploadService, 
        IFileRetrievalService fileRetrievalService, 
        ILogger<FilesController> logger)
    {
        _fileUploadService = fileUploadService;
        _fileRetrievalService = fileRetrievalService;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetFileMetadata(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting metadata for file {FileId}", id);

        var file = await _fileRetrievalService.GetFileMetadataAsync(FileId.From(id), cancellationToken);

        if (file is null)
        {
            _logger.LogWarning("File with id {FileId} not found", id);
            return NotFound();
        }

        // In a real app, you'd map this to a DTO.
        return Ok(file);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadFile(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting download for file {FileId}", id);

        var result = await _fileRetrievalService.DownloadFileAsync(FileId.From(id), cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("Download failed for file {FileId}: {Error}", id, result.ErrorMessage);
            
            if (result.ErrorMessage?.Contains("not found") == true)
                return NotFound(new { error = result.ErrorMessage });
            
            // Check if this is a Glacier storage class error (needs restoration)
            if (result.ErrorMessage?.Contains("storage class") == true || 
                result.ErrorMessage?.Contains("not valid for the object") == true)
            {
                _logger.LogInformation("File {FileId} is in Glacier and needs restoration", id);
                
                // Initiate restoration
                var retrievalResult = await _fileRetrievalService.InitiateRetrievalAsync(
                    FileId.From(id), 
                    RetrievalTier.Standard, 
                    cancellationToken);
                
                if (retrievalResult.Success)
                {
                    return Accepted(new { 
                        message = "File is archived and restoration has been initiated",
                        retrievalId = retrievalResult.RetrievalId,
                        estimatedCompletionTime = retrievalResult.EstimatedCompletionTime,
                        status = "restoration_in_progress"
                    });
                }
                else
                {
                    return BadRequest(new { error = $"Failed to initiate restoration: {retrievalResult.ErrorMessage}" });
                }
            }
            
            return BadRequest(new { error = result.ErrorMessage });
        }

        if (result.FileStream is null)
        {
            _logger.LogError("Download succeeded but FileStream is null for file {FileId}", id);
            return StatusCode(500, new { error = "File stream is not available" });
        }

        _logger.LogInformation("File {FileId} download started successfully", id);

        // Return the file stream with appropriate headers
        return File(
            result.FileStream, 
            result.ContentType ?? "application/octet-stream",
            result.FileName ?? $"file-{id}");
    }

    [HttpGet]
    public async Task<IActionResult> ListFiles([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] Guid? userId = null, CancellationToken cancellationToken = default)
    {
        // For testing: if no userId provided, use a default one
        // In production, this would come from authentication
        var actualUserId = userId.HasValue 
            ? UserId.From(userId.Value)
            : UserId.From(Guid.Parse("123e4567-e89b-12d3-a456-426614174000"));
        
        _logger.LogInformation("Listing files for user {UserId}, page {Page}, pageSize {PageSize}", actualUserId, page, pageSize);

        var files = await _fileRetrievalService.GetUserFilesAsync(actualUserId, page, pageSize, cancellationToken);

        return Ok(new
        {
            files = files.Select(f => new
            {
                id = f.Id.Value,
                fileName = f.Metadata.OriginalFileName,
                size = f.Size.Bytes,
                sizeFormatted = f.Size.ToHumanReadable(),
                contentType = f.Type.MimeType,
                capturedAt = f.Metadata.CapturedAt,
                uploadedAt = f.Metadata.CreatedAt,
                status = f.Status.CurrentState.ToString(),
                storageProvider = f.Location?.ProviderName,
                storagePath = f.Location?.Path,
                // Client can use this to show icon: Deep Archive = needs retrieval
                needsRetrieval = f.Location?.ProviderName == "s3-glacier-deep" ||
                                 f.Location?.ProviderName == "s3-glacier-flexible",
                retrievalTimeHours = f.Location?.ProviderName switch
                {
                    "s3-glacier-deep" => 12,  // 12-48 hours (show minimum)
                    "s3-glacier-flexible" => 3, // 3-5 hours (show minimum)
                    _ => 0
                },
                thumbnailUrl = f.ThumbnailLocation?.Path, // S3 path to thumbnail in Standard storage
                userId = f.UserId.Value
            }),
            page,
            pageSize,
            totalFiles = files.Count,
            queriedUserId = actualUserId.Value
        });
    }

    [HttpGet("debug/all")]
    public async Task<IActionResult> ListAllFiles(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DEBUG: Listing all files in database");

        // This is a debug endpoint - in production this would be removed or secured
        // We'll need to access the repository directly since GetUserFilesAsync filters by user
        
        // For now, let's try a few common user IDs that might have been used
        var commonUserIds = new[]
        {
            Guid.Parse("123e4567-e89b-12d3-a456-426614174000"), // Default hardcoded
            Guid.Parse("00000000-0000-0000-0000-000000000000"), // Empty GUID
        };

        var allFiles = new List<object>();
        
        foreach (var testUserId in commonUserIds)
        {
            var files = await _fileRetrievalService.GetUserFilesAsync(UserId.From(testUserId), 1, 100, cancellationToken);
            foreach (var file in files)
            {
                allFiles.Add(new
                {
                    id = file.Id.Value,
                    fileName = file.Metadata.OriginalFileName,
                    size = file.Size.Bytes,
                    contentType = file.Type.MimeType,
                    uploadedAt = file.Metadata.CreatedAt,
                    status = file.Status.CurrentState.ToString(),
                    userId = file.UserId.Value
                });
            }
        }

        return Ok(new
        {
            message = "DEBUG: All files found with common user IDs",
            totalFiles = allFiles.Count,
            files = allFiles,
            searchedUserIds = commonUserIds
        });
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadConstants.MaxFileSizeBytes)] // 20 MB limit for single-request uploads
    public async Task<IActionResult> UploadFile([FromForm] UploadFileRequest request, CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest("File is empty");
        }

        _logger.LogInformation("Starting upload for file {FileName}", request.File.FileName);

        await using var stream = request.File.OpenReadStream();

        var result = await _fileUploadService.UploadAsync(
            UserId.From(request.UserId),
            stream,
            request.File.FileName,
            request.File.ContentType,
            request.CapturedAt ?? DateTime.UtcNow,
            cancellationToken);

        if (!result.Success)
        {
            _logger.LogError("Upload failed for file {FileName}: {Error}", request.File.FileName, result.ErrorMessage);
            return BadRequest(result.ErrorMessage);
        }

        if (result.FileId is null)
        {
            _logger.LogError("Upload succeeded but FileId is null for file {FileName}", request.File.FileName);
            return StatusCode(500, "An unexpected error occurred during upload.");
        }

        if (result.IsDuplicate)
        {
            _logger.LogInformation("File {FileName} is a duplicate of {FileId}", request.File.FileName, result.FileId);
            return Ok(new { Message = "File already exists.", FileId = result.FileId });
        }

        _logger.LogInformation("File {FileName} uploaded successfully with id {FileId}", request.File.FileName, result.FileId);

        return CreatedAtAction(
            nameof(GetFileMetadata),
            new { id = result.FileId.Value },
            new { FileId = result.FileId });
    }
}