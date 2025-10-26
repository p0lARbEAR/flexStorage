namespace FlexStorage.Application.DTOs;

/// <summary>
/// Result of downloading a file.
/// </summary>
public class DownloadFileResult
{
    public bool Success { get; init; }
    public Stream? FileStream { get; init; }
    public string? FileName { get; init; }
    public string? ContentType { get; init; }
    public long? FileSize { get; init; }
    public string? ErrorMessage { get; init; }

    public static DownloadFileResult SuccessResult(
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSize)
    {
        return new DownloadFileResult
        {
            Success = true,
            FileStream = fileStream,
            FileName = fileName,
            ContentType = contentType,
            FileSize = fileSize
        };
    }

    public static DownloadFileResult FailureResult(string errorMessage)
    {
        return new DownloadFileResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
