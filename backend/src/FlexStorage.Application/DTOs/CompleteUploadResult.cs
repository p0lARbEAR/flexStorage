using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Application.DTOs;

/// <summary>
/// Result of completing a chunked upload.
/// </summary>
public class CompleteUploadResult
{
    public bool Success { get; init; }
    public FileId? FileId { get; init; }
    public StorageLocation? Location { get; init; }
    public string? ErrorMessage { get; init; }

    public static CompleteUploadResult SuccessResult(FileId fileId, StorageLocation location)
    {
        return new CompleteUploadResult
        {
            Success = true,
            FileId = fileId,
            Location = location
        };
    }

    public static CompleteUploadResult FailureResult(string errorMessage)
    {
        return new CompleteUploadResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
