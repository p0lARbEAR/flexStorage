
using FlexStorage.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace FlexStorage.API.Controllers;

public class UploadFileCommand
{
    [FromForm(Name = "file")]
    public IFormFile? File { get; set; }

    // Placeholder for now, will be extracted from auth token
    [FromForm(Name = "userId")]
    public Guid UserId { get; set; } = Guid.NewGuid();

    [FromForm(Name = "capturedAt")]
    public DateTime? CapturedAt { get; set; }
}
