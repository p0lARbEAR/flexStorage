using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Application.Services;
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

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadFile([FromForm] UploadFileCommand command, CancellationToken cancellationToken)
    {
        if (command.File is null || command.File.Length == 0)
        {
            return BadRequest("File is empty");
        }

        _logger.LogInformation("Starting upload for file {FileName}", command.File.FileName);

        await using var stream = command.File.OpenReadStream();

        var result = await _fileUploadService.UploadAsync(
            UserId.From(command.UserId),
            stream,
            command.File.FileName,
            command.File.ContentType,
            command.CapturedAt ?? DateTime.UtcNow,
            cancellationToken);

        if (!result.Success)
        {
            _logger.LogError("Upload failed for file {FileName}: {Error}", command.File.FileName, result.ErrorMessage);
            return BadRequest(result.ErrorMessage);
        }

        if (result.FileId is null)
        {
            _logger.LogError("Upload succeeded but FileId is null for file {FileName}", command.File.FileName);
            return StatusCode(500, "An unexpected error occurred during upload.");
        }
        
        if (result.IsDuplicate)
        {
            _logger.LogInformation("File {FileName} is a duplicate of {FileId}", command.File.FileName, result.FileId);
            return Ok(new { Message = "File already exists.", FileId = result.FileId });
        }

        _logger.LogInformation("File {FileName} uploaded successfully with id {FileId}", command.File.FileName, result.FileId);
        
        return CreatedAtAction(
            nameof(GetFileMetadata), 
            new { id = result.FileId.Value }, 
            new { FileId = result.FileId });
    }
}