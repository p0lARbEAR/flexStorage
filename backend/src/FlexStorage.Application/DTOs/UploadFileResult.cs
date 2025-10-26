using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Application.DTOs;

/// <summary>
/// Result of file upload operation.
/// </summary>
public class UploadFileResult
{
    public bool Success { get; init; }
    public FileId? FileId { get; init; }
    public bool IsDuplicate { get; init; }
    public string? ErrorMessage { get; init; }
    public StorageLocation? Location { get; init; }

    public static UploadFileResult SuccessResult(FileId fileId, StorageLocation location, bool isDuplicate = false)
    {
        return new UploadFileResult
        {
            Success = true,
            FileId = fileId,
            Location = location,
            IsDuplicate = isDuplicate
        };
    }

    public static UploadFileResult FailureResult(string errorMessage)
    {
        return new UploadFileResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
