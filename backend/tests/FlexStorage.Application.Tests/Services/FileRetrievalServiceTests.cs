using FluentAssertions;
using FlexStorage.Application.DTOs;
using FlexStorage.Application.Interfaces.Repositories;
using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Application.Services;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;
using NSubstitute;
using Xunit;
using File = FlexStorage.Domain.Entities.File;

namespace FlexStorage.Application.Tests.Services;

public class FileRetrievalServiceTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileRepository _fileRepository;
    private readonly IStorageService _storageService;
    private readonly FileRetrievalService _sut;

    public FileRetrievalServiceTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _fileRepository = Substitute.For<IFileRepository>();
        _storageService = Substitute.For<IStorageService>();

        _unitOfWork.Files.Returns(_fileRepository);

        _sut = new FileRetrievalService(_unitOfWork, _storageService);
    }

    [Fact]
    public async Task InitiateRetrievalAsync_FromColdStorage_ShouldStartRetrieval()
    {
        // Arrange
        var userId = UserId.New();
        var file = CreateCompletedFile(userId, "s3-glacier-deep");
        var tier = RetrievalTier.Standard;

        _fileRepository.GetByIdAsync(file.Id, Arg.Any<CancellationToken>())
            .Returns(file);

        _storageService.InitiateRetrievalAsync(
            file.Location!,
            tier,
            Arg.Any<CancellationToken>())
            .Returns(new RetrievalResult
            {
                Success = true,
                RetrievalId = "retrieval-123",
                Status = RetrievalStatus.InProgress
            });

        // Act
        var result = await _sut.InitiateRetrievalAsync(file.Id, tier);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RetrievalId.Should().Be("retrieval-123");
        result.EstimatedCompletionTime.Should().NotBeNull();
    }

    [Fact]
    public async Task InitiateRetrievalAsync_WithInvalidFileId_ShouldFail()
    {
        // Arrange
        var fileId = FileId.New();

        _fileRepository.GetByIdAsync(fileId, Arg.Any<CancellationToken>())
            .Returns((File?)null);

        // Act
        var result = await _sut.InitiateRetrievalAsync(fileId, RetrievalTier.Standard);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("File not found");
    }

    [Fact]
    public async Task CheckRetrievalStatusAsync_ShouldReturnCurrentStatus()
    {
        // Arrange
        var retrievalId = "retrieval-123";

        _storageService.GetRetrievalStatusAsync(retrievalId, Arg.Any<CancellationToken>())
            .Returns(new RetrievalStatusDetail
            {
                RetrievalId = retrievalId,
                Status = RetrievalStatus.InProgress,
                ProgressPercentage = 50
            });

        // Act
        var result = await _sut.CheckRetrievalStatusAsync(retrievalId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Status.Should().Be(RetrievalStatus.InProgress);
        result.ProgressPercentage.Should().Be(50);
    }

    [Fact]
    public async Task DownloadFileAsync_WhenAvailable_ShouldReturnStream()
    {
        // Arrange
        var userId = UserId.New();
        var file = CreateCompletedFile(userId, "s3-glacier-deep");
        var expectedStream = new MemoryStream(new byte[] { 1, 2, 3 });

        _fileRepository.GetByIdAsync(file.Id, Arg.Any<CancellationToken>())
            .Returns(file);

        _storageService.DownloadAsync(file.Location!, Arg.Any<CancellationToken>())
            .Returns(expectedStream);

        // Act
        var result = await _sut.DownloadFileAsync(file.Id);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.FileStream.Should().BeSameAs(expectedStream);
        result.FileName.Should().Be(file.Metadata.OriginalFileName);
        result.ContentType.Should().Be(file.Type.MimeType);
    }

    [Fact]
    public async Task DownloadFileAsync_WithNoLocation_ShouldFail()
    {
        // Arrange
        var userId = UserId.New();
        var metadata = FileMetadata.Create("test.jpg", "sha256:abc", DateTime.UtcNow);
        var file = File.Create(
            userId,
            metadata,
            FileSize.FromBytes(1000),
            FileType.FromMimeType("image/jpeg"));
        // File has no location (not uploaded)

        _fileRepository.GetByIdAsync(file.Id, Arg.Any<CancellationToken>())
            .Returns(file);

        // Act
        var result = await _sut.DownloadFileAsync(file.Id);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("has not been uploaded yet");
    }

    [Fact]
    public async Task DownloadFileAsync_WhenStorageFails_ShouldReturnError()
    {
        // Arrange
        var userId = UserId.New();
        var file = CreateCompletedFile(userId, "s3-glacier-deep");

        _fileRepository.GetByIdAsync(file.Id, Arg.Any<CancellationToken>())
            .Returns(file);

        _storageService.DownloadAsync(file.Location!, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Stream>(new Exception("Storage error")));

        // Act
        var result = await _sut.DownloadFileAsync(file.Id);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Storage error");
    }

    [Fact]
    public async Task GetFileMetadataAsync_WithValidFileId_ShouldReturnFile()
    {
        // Arrange - RED: Test GetFileMetadataAsync with valid ID
        var userId = UserId.New();
        var file = CreateCompletedFile(userId, "s3-glacier-deep");

        _fileRepository.GetByIdAsync(file.Id, Arg.Any<CancellationToken>())
            .Returns(file);

        // Act
        var result = await _sut.GetFileMetadataAsync(file.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(file.Id);
        result.Metadata.OriginalFileName.Should().Be("test.jpg");
    }

    [Fact]
    public async Task GetFileMetadataAsync_WithInvalidFileId_ShouldReturnNull()
    {
        // Arrange - RED: Test GetFileMetadataAsync with invalid ID
        var fileId = FileId.New();

        _fileRepository.GetByIdAsync(fileId, Arg.Any<CancellationToken>())
            .Returns((File?)null);

        // Act
        var result = await _sut.GetFileMetadataAsync(fileId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserFilesAsync_ShouldReturnPaginatedFiles()
    {
        // Arrange - RED: Test GetUserFilesAsync with pagination
        var userId = UserId.New();
        var file1 = CreateCompletedFile(userId, "s3-glacier-deep");
        var file2 = CreateCompletedFile(userId, "s3-glacier-flexible");

        var pagedResult = new PagedResult<File>(
            new List<File> { file1, file2 },
            totalCount: 10,
            page: 1,
            pageSize: 2);

        _fileRepository.GetByUserIdAsync(userId, 1, 2, Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        // Act
        var result = await _sut.GetUserFilesAsync(userId, 1, 2);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
        result[0].Should().Be(file1);
        result[1].Should().Be(file2);
    }

    [Fact]
    public async Task InitiateRetrievalAsync_WhenStorageServiceThrows_ShouldReturnFailure()
    {
        // Arrange - RED: Test exception handling in InitiateRetrievalAsync
        var userId = UserId.New();
        var file = CreateCompletedFile(userId, "s3-glacier-deep");

        _fileRepository.GetByIdAsync(file.Id, Arg.Any<CancellationToken>())
            .Returns(file);

        _storageService.InitiateRetrievalAsync(
            file.Location!,
            Arg.Any<RetrievalTier>(),
            Arg.Any<CancellationToken>())
            .Returns<RetrievalResult>(x => throw new InvalidOperationException("Storage service unavailable"));

        // Act
        var result = await _sut.InitiateRetrievalAsync(file.Id, RetrievalTier.Standard);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Storage service unavailable");
    }

    [Fact]
    public async Task CheckRetrievalStatusAsync_WhenStorageServiceThrows_ShouldReturnFailure()
    {
        // Arrange - RED: Test exception handling in CheckRetrievalStatusAsync
        var retrievalId = "retrieval-123";

        _storageService.GetRetrievalStatusAsync(retrievalId, Arg.Any<CancellationToken>())
            .Returns<RetrievalStatusDetail>(x => throw new InvalidOperationException("Status check failed"));

        // Act
        var result = await _sut.CheckRetrievalStatusAsync(retrievalId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Status check failed");
    }

    [Fact]
    public async Task DownloadFileAsync_WithFileNotFound_ShouldReturnFailure()
    {
        // Arrange - RED: Test DownloadFileAsync with non-existent file
        var fileId = FileId.New();

        _fileRepository.GetByIdAsync(fileId, Arg.Any<CancellationToken>())
            .Returns((File?)null);

        // Act
        var result = await _sut.DownloadFileAsync(fileId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("File not found");
    }

    [Fact]
    public async Task InitiateRetrievalAsync_WhenStorageReturnsNullRetrievalId_ShouldHandleGracefully()
    {
        // Arrange - RED: Test null retrievalId edge case
        var userId = UserId.New();
        var file = CreateCompletedFile(userId, "s3-glacier-deep");

        _fileRepository.GetByIdAsync(file.Id, Arg.Any<CancellationToken>())
            .Returns(file);

        var storageResult = new RetrievalResult
        {
            Success = true,
            RetrievalId = null, // Edge case: null despite success
            EstimatedCompletionTime = TimeSpan.FromHours(5),
            Status = RetrievalStatus.Requested
        };

        _storageService.InitiateRetrievalAsync(
            file.Location!,
            Arg.Any<RetrievalTier>(),
            Arg.Any<CancellationToken>())
            .Returns(storageResult);

        // Act
        var result = await _sut.InitiateRetrievalAsync(file.Id, RetrievalTier.Bulk);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("RetrievalId cannot be null");
    }

    private File CreateCompletedFile(UserId userId, string providerName)
    {
        var metadata = FileMetadata.Create("test.jpg", "sha256:abc123", DateTime.UtcNow);
        var file = File.Create(
            userId,
            metadata,
            FileSize.FromBytes(1000),
            FileType.FromMimeType("image/jpeg"));

        file.StartUpload();
        file.CompleteUpload(StorageLocation.Create(providerName, $"{providerName}://bucket/path"));

        return file;
    }
}
