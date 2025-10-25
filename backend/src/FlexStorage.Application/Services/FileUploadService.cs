using FlexStorage.Application.DTOs;
using FlexStorage.Application.Interfaces.Repositories;
using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;
using File = FlexStorage.Domain.Entities.File;

namespace FlexStorage.Application.Services;

/// <summary>
/// Application service for handling file uploads.
/// </summary>
public class FileUploadService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHashService _hashService;
    private readonly IStorageService _storageService;
    private readonly StorageProviderSelector _providerSelector;

    public FileUploadService(
        IUnitOfWork unitOfWork,
        IHashService hashService,
        IStorageService storageService,
        StorageProviderSelector providerSelector)
    {
        _unitOfWork = unitOfWork;
        _hashService = hashService;
        _storageService = storageService;
        _providerSelector = providerSelector;
    }

    /// <summary>
    /// Uploads a file to storage.
    /// </summary>
    public async Task<UploadFileResult> UploadAsync(
        UserId userId,
        Stream fileStream,
        string fileName,
        string mimeType,
        DateTime capturedAt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be null or empty", nameof(fileName));

        if (string.IsNullOrWhiteSpace(mimeType))
            throw new ArgumentException("MIME type cannot be null or empty", nameof(mimeType));

        try
        {
            // Calculate hash for deduplication
            var hash = await _hashService.CalculateSha256Async(fileStream, cancellationToken);

            // Check for duplicate
            var existingFile = await _unitOfWork.Files.GetByHashAsync(hash, cancellationToken);
            if (existingFile != null)
            {
                return UploadFileResult.SuccessResult(
                    existingFile.Id,
                    existingFile.Location!,
                    isDuplicate: true);
            }

            // Reset stream position after hash calculation
            if (fileStream.CanSeek)
                fileStream.Position = 0;

            // Create domain objects
            var fileSize = FileSize.FromBytes(fileStream.Length);
            var fileType = FileType.FromMimeType(mimeType);
            var metadata = FileMetadata.Create(fileName, hash, capturedAt);

            // Create File aggregate
            var file = File.Create(userId, metadata, fileSize, fileType);

            // Start upload
            file.StartUpload();

            // Select appropriate storage provider
            var provider = _providerSelector.SelectProvider(fileType, fileSize);

            // Upload to storage
            var uploadOptions = new UploadOptions
            {
                FileName = metadata.SanitizedFileName,
                ContentType = fileType.MimeType,
                PreferredProvider = provider.ProviderName
            };

            var uploadResult = await _storageService.UploadAsync(
                fileStream,
                uploadOptions,
                cancellationToken);

            if (!uploadResult.Success)
            {
                return UploadFileResult.FailureResult(
                    uploadResult.ErrorMessage ?? "Upload to storage provider failed");
            }

            // Complete upload in domain
            file.CompleteUpload(uploadResult.Location!);

            // Save to repository
            await _unitOfWork.Files.AddAsync(file, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return UploadFileResult.SuccessResult(file.Id, uploadResult.Location!);
        }
        catch (ArgumentException)
        {
            throw; // Re-throw validation exceptions
        }
        catch (Exception ex)
        {
            return UploadFileResult.FailureResult($"Upload failed: {ex.Message}");
        }
    }
}
