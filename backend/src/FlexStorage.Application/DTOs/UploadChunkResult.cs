namespace FlexStorage.Application.DTOs;

/// <summary>
/// Result of uploading a chunk.
/// </summary>
public class UploadChunkResult
{
    public bool Success { get; init; }
    public int ChunkIndex { get; init; }
    public int Progress { get; init; }
    public bool IsComplete { get; init; }
    public string? ErrorMessage { get; init; }

    public static UploadChunkResult SuccessResult(int chunkIndex, int progress, bool isComplete)
    {
        return new UploadChunkResult
        {
            Success = true,
            ChunkIndex = chunkIndex,
            Progress = progress,
            IsComplete = isComplete
        };
    }

    public static UploadChunkResult FailureResult(int chunkIndex, string errorMessage)
    {
        return new UploadChunkResult
        {
            Success = false,
            ChunkIndex = chunkIndex,
            ErrorMessage = errorMessage
        };
    }
}
