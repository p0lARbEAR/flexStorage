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
        result.ErrorMessage.Should().Contain("not uploaded");
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
