
using FlexStorage.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace FlexStorage.API.Controllers;

public class UploadFileCommand
{
    [FromForm(Name = "file")]
    public IFormFile? File { get; set; }

    // Placeholder for now, will be extracted from auth token
    // Using consistent default for testing - in production this would come from JWT
    [FromForm(Name = "userId")]
    public Guid UserId { get; set; } = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");

    [FromForm(Name = "capturedAt")]
    public DateTime? CapturedAt { get; set; }
}
