using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Application.DTOs;

/// <summary>
/// Status of an upload session.
/// </summary>
public class UploadSessionStatus
{
    public UploadSessionId SessionId { get; init; } = null!;
    public FileId FileId { get; init; } = null!;
    public int Progress { get; init; }
    public int TotalChunks { get; init; }
    public IReadOnlyCollection<int> UploadedChunks { get; init; } = Array.Empty<int>();
    public bool IsComplete { get; init; }
    public bool IsExpired { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
}
