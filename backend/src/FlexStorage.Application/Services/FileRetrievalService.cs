using FlexStorage.Application.DTOs;
using FlexStorage.Application.Interfaces.Repositories;
using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Application.Services;

/// <summary>
/// Application service for retrieving files from storage.
/// </summary>
public class FileRetrievalService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStorageService _storageService;

    public FileRetrievalService(
        IUnitOfWork unitOfWork,
        IStorageService storageService)
    {
        _unitOfWork = unitOfWork;
        _storageService = storageService;
    }

    /// <summary>
    /// Initiates retrieval of a file from cold storage.
    /// </summary>
    public async Task<InitiateRetrievalResult> InitiateRetrievalAsync(
        FileId fileId,
        RetrievalTier tier,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get file
            var file = await _unitOfWork.Files.GetByIdAsync(fileId, cancellationToken);
            if (file == null)
                return InitiateRetrievalResult.FailureResult("File not found");

            if (file.Location == null)
                return InitiateRetrievalResult.FailureResult("File has not been uploaded yet");

            // Initiate retrieval from storage provider
            var retrievalResult = await _storageService.InitiateRetrievalAsync(
                file.Location,
                tier,
                cancellationToken);

            if (!retrievalResult.Success)
                return InitiateRetrievalResult.FailureResult(
                    retrievalResult.ErrorMessage ?? "Failed to initiate retrieval");

            // Calculate estimated completion time
            var estimatedTime = DateTime.UtcNow.Add(retrievalResult.EstimatedCompletionTime);

            return InitiateRetrievalResult.SuccessResult(
                retrievalResult.RetrievalId,
                estimatedTime);
        }
        catch (Exception ex)
        {
            return InitiateRetrievalResult.FailureResult($"Failed to initiate retrieval: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks the status of a retrieval request.
    /// </summary>
    public async Task<CheckRetrievalStatusResult> CheckRetrievalStatusAsync(
        string retrievalId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var statusDetail = await _storageService.GetRetrievalStatusAsync(retrievalId, cancellationToken);

            return CheckRetrievalStatusResult.SuccessResult(
                statusDetail.Status,
                statusDetail.ProgressPercentage,
                statusDetail.CompletedAt);
        }
        catch (Exception ex)
        {
            return CheckRetrievalStatusResult.FailureResult($"Failed to check retrieval status: {ex.Message}");
        }
    }

    /// <summary>
    /// Downloads a file.
    /// </summary>
    public async Task<DownloadFileResult> DownloadFileAsync(
        FileId fileId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get file
            var file = await _unitOfWork.Files.GetByIdAsync(fileId, cancellationToken);
            if (file == null)
                return DownloadFileResult.FailureResult("File not found");

            if (file.Location == null)
                return DownloadFileResult.FailureResult("File has not been uploaded yet");

            // Download from storage
            var fileStream = await _storageService.DownloadAsync(file.Location, cancellationToken);

            return DownloadFileResult.SuccessResult(
                fileStream,
                file.Metadata.OriginalFileName,
                file.Type.MimeType,
                file.Size.Bytes);
        }
        catch (Exception ex)
        {
            return DownloadFileResult.FailureResult($"Failed to download file: {ex.Message}");
        }
    }
}
