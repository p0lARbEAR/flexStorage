using FlexStorage.Application.DTOs;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Application.Interfaces.Services;

/// <summary>
/// Service interface for handling chunked file uploads.
/// </summary>
public interface IChunkedUploadService
{
    /// <summary>
    /// Initiates a new chunked upload session.
    /// </summary>
    Task<InitiateUploadResult> InitiateUploadAsync(
        UserId userId,
        string fileName,
        string mimeType,
        long totalSize,
        DateTime capturedAt,
        int? chunkSize = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a chunk of data.
    /// </summary>
    Task<UploadChunkResult> UploadChunkAsync(
        UploadSessionId sessionId,
        int chunkIndex,
        byte[] chunkData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the chunked upload and finalizes the file.
    /// </summary>
    Task<CompleteUploadResult> CompleteUploadAsync(
        UploadSessionId sessionId,
        Stream completeFileStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of an upload session.
    /// </summary>
    Task<UploadSessionStatus?> GetSessionStatusAsync(
        UploadSessionId sessionId,
        CancellationToken cancellationToken = default);
}
