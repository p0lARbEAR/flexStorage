using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Application.DTOs;

/// <summary>
/// Result of initiating a chunked upload.
/// </summary>
public class InitiateUploadResult
{
    public bool Success { get; init; }
    public UploadSessionId? SessionId { get; init; }
    public FileId? FileId { get; init; }
    public int ChunkSize { get; init; }
    public int TotalChunks { get; init; }
    public string? ErrorMessage { get; init; }

    public static InitiateUploadResult SuccessResult(
        UploadSessionId sessionId,
        FileId fileId,
        int chunkSize,
        int totalChunks)
    {
        return new InitiateUploadResult
        {
            Success = true,
            SessionId = sessionId,
            FileId = fileId,
            ChunkSize = chunkSize,
            TotalChunks = totalChunks
        };
    }

    public static InitiateUploadResult FailureResult(string errorMessage)
    {
        return new InitiateUploadResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
