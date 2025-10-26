using FlexStorage.Domain.Common;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Domain.Entities;

/// <summary>
/// Entity representing a chunked upload session for large files.
/// </summary>
public class UploadSession : Entity<UploadSessionId>
{
    /// <summary>
    /// Gets the file ID this session is for.
    /// </summary>
    public FileId FileId { get; private set; }

    /// <summary>
    /// Gets the user ID who initiated this session.
    /// </summary>
    public UserId UserId { get; private set; }

    /// <summary>
    /// Gets the total file size in bytes.
    /// </summary>
    public long TotalSize { get; private set; }

    /// <summary>
    /// Gets the chunk size in bytes.
    /// </summary>
    public int ChunkSize { get; private set; }

    /// <summary>
    /// Gets the total number of chunks.
    /// </summary>
    public int TotalChunks { get; private set; }

    /// <summary>
    /// Gets the set of uploaded chunk indices.
    /// </summary>
    private readonly HashSet<int> _uploadedChunks = new();
    public IReadOnlyCollection<int> UploadedChunks => _uploadedChunks;

    /// <summary>
    /// Gets when this session was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Gets when this session expires.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// Gets when this session was completed.
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Gets whether all chunks have been uploaded.
    /// </summary>
    public bool IsComplete => _uploadedChunks.Count == TotalChunks;

    /// <summary>
    /// Gets whether this session has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt && CompletedAt == null;

    /// <summary>
    /// Gets the upload progress percentage (0-100).
    /// </summary>
    public int Progress => TotalChunks == 0 ? 0 : (_uploadedChunks.Count * 100) / TotalChunks;

    // EF Core constructor
    private UploadSession()
    {
        FileId = null!;
        UserId = null!;
    }

    private UploadSession(
        UploadSessionId id,
        FileId fileId,
        UserId userId,
        long totalSize,
        int chunkSize)
    {
        if (totalSize <= 0)
            throw new ArgumentException("Total size must be positive", nameof(totalSize));

        if (chunkSize <= 0)
            throw new ArgumentException("Chunk size must be positive", nameof(chunkSize));

        Id = id;
        FileId = fileId;
        UserId = userId;
        TotalSize = totalSize;
        ChunkSize = chunkSize;
        TotalChunks = (int)Math.Ceiling((double)totalSize / chunkSize);
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = DateTime.UtcNow.AddHours(24); // 24 hour expiration
    }

    /// <summary>
    /// Creates a new upload session.
    /// </summary>
    public static UploadSession Create(
        FileId fileId,
        UserId userId,
        long totalSize,
        int chunkSize)
    {
        return new UploadSession(
            UploadSessionId.New(),
            fileId,
            userId,
            totalSize,
            chunkSize);
    }

    /// <summary>
    /// Marks a chunk as uploaded.
    /// </summary>
    /// <param name="chunkIndex">The zero-based chunk index</param>
    public void MarkChunkUploaded(int chunkIndex)
    {
        if (CompletedAt != null)
            throw new InvalidOperationException("Cannot modify completed session");

        if (IsExpired)
            throw new InvalidOperationException("Session has expired");

        if (chunkIndex < 0 || chunkIndex >= TotalChunks)
            throw new ArgumentOutOfRangeException(nameof(chunkIndex),
                $"Chunk index must be between 0 and {TotalChunks - 1}");

        _uploadedChunks.Add(chunkIndex);
    }

    /// <summary>
    /// Marks the session as completed.
    /// </summary>
    public void Complete()
    {
        if (CompletedAt != null)
            throw new InvalidOperationException("Session already completed");

        if (!IsComplete)
            throw new InvalidOperationException("Cannot complete session with missing chunks");

        CompletedAt = DateTime.UtcNow;
    }
}
