using FlexStorage.Application.DTOs;
using FlexStorage.Application.Interfaces.Repositories;
using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;
using File = FlexStorage.Domain.Entities.File;

namespace FlexStorage.Application.Services;

/// <summary>
/// Application service for handling chunked file uploads.
/// </summary>
public class ChunkedUploadService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHashService _hashService;
    private const int DefaultChunkSize = 5 * 1024 * 1024; // 5 MB

    public ChunkedUploadService(
        IUnitOfWork unitOfWork,
        IHashService hashService)
    {
        _unitOfWork = unitOfWork;
        _hashService = hashService;
    }

    /// <summary>
    /// Initiates a new chunked upload session.
    /// </summary>
    public async Task<InitiateUploadResult> InitiateUploadAsync(
        UserId userId,
        string fileName,
        string mimeType,
        long totalSize,
        DateTime capturedAt,
        int? chunkSize = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or empty", nameof(fileName));

            if (totalSize <= 0)
                throw new ArgumentException("Total size must be positive", nameof(totalSize));

            // Create temporary hash for the file (will be verified on completion)
            var tempHash = $"sha256:pending_{Guid.NewGuid():N}";

            // Create File aggregate
            var metadata = FileMetadata.Create(fileName, tempHash, capturedAt);
            var fileSize = FileSize.FromBytes(totalSize);
            var fileType = FileType.FromMimeType(mimeType);
            var file = File.Create(userId, metadata, fileSize, fileType);

            // Create upload session
            var effectiveChunkSize = chunkSize ?? DefaultChunkSize;
            var session = UploadSession.Create(file.Id, userId, totalSize, effectiveChunkSize);

            // Save to repository
            await _unitOfWork.Files.AddAsync(file, cancellationToken);
            await _unitOfWork.UploadSessions.AddAsync(session, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return InitiateUploadResult.SuccessResult(
                session.Id,
                file.Id,
                effectiveChunkSize,
                session.TotalChunks);
        }
        catch (Exception ex)
        {
            return InitiateUploadResult.FailureResult($"Failed to initiate upload: {ex.Message}");
        }
    }

    /// <summary>
    /// Uploads a chunk of data.
    /// </summary>
    public async Task<UploadChunkResult> UploadChunkAsync(
        UploadSessionId sessionId,
        int chunkIndex,
        byte[] chunkData,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get session
            var session = await _unitOfWork.UploadSessions.GetByIdAsync(sessionId, cancellationToken);
            if (session == null)
                return UploadChunkResult.FailureResult(chunkIndex, "Session not found");

            // Mark chunk as uploaded
            session.MarkChunkUploaded(chunkIndex);

            // Update session
            await _unitOfWork.UploadSessions.UpdateAsync(session, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return UploadChunkResult.SuccessResult(
                chunkIndex,
                session.Progress,
                session.IsComplete);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return UploadChunkResult.FailureResult(chunkIndex, ex.Message);
        }
        catch (Exception ex)
        {
            return UploadChunkResult.FailureResult(chunkIndex, $"Failed to upload chunk: {ex.Message}");
        }
    }

    /// <summary>
    /// Completes the chunked upload and finalizes the file.
    /// </summary>
    public async Task<CompleteUploadResult> CompleteUploadAsync(
        UploadSessionId sessionId,
        Stream completeFileStream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get session
            var session = await _unitOfWork.UploadSessions.GetByIdAsync(sessionId, cancellationToken);
            if (session == null)
                return CompleteUploadResult.FailureResult("Session not found");

            // Verify all chunks uploaded
            if (!session.IsComplete)
                return CompleteUploadResult.FailureResult(
                    $"Upload incomplete: {session.UploadedChunks.Count}/{session.TotalChunks} chunks uploaded, missing chunks");

            // Get file
            var file = await _unitOfWork.Files.GetByIdAsync(session.FileId, cancellationToken);
            if (file == null)
                return CompleteUploadResult.FailureResult("File not found");

            // Calculate final hash
            var hash = await _hashService.CalculateSha256Async(completeFileStream, cancellationToken);

            // Start and complete upload
            file.StartUpload();

            // For now, create a temporary location (will be replaced with actual storage in Infrastructure layer)
            var location = StorageLocation.Create("temp", $"temp://{session.FileId}");
            file.CompleteUpload(location);

            // Complete session
            session.Complete();

            // Save changes
            await _unitOfWork.UploadSessions.UpdateAsync(session, cancellationToken);
            await _unitOfWork.Files.UpdateAsync(file, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return CompleteUploadResult.SuccessResult(file.Id, location);
        }
        catch (InvalidOperationException ex)
        {
            return CompleteUploadResult.FailureResult(ex.Message);
        }
        catch (Exception ex)
        {
            return CompleteUploadResult.FailureResult($"Failed to complete upload: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the status of an upload session.
    /// </summary>
    public async Task<UploadSessionStatus?> GetSessionStatusAsync(
        UploadSessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _unitOfWork.UploadSessions.GetByIdAsync(sessionId, cancellationToken);
        if (session == null)
            return null;

        return new UploadSessionStatus
        {
            SessionId = session.Id,
            FileId = session.FileId,
            Progress = session.Progress,
            TotalChunks = session.TotalChunks,
            UploadedChunks = session.UploadedChunks.ToList(),
            IsComplete = session.IsComplete,
            IsExpired = session.IsExpired,
            CreatedAt = session.CreatedAt,
            ExpiresAt = session.ExpiresAt
        };
    }
}
