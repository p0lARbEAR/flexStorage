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

public class FileUploadServiceTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileRepository _fileRepository;
    private readonly IHashService _hashService;
    private readonly IStorageService _storageService;
    private readonly StorageProviderSelector _providerSelector;
    private readonly FileUploadService _sut;

    public FileUploadServiceTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _fileRepository = Substitute.For<IFileRepository>();
        _hashService = Substitute.For<IHashService>();
        _storageService = Substitute.For<IStorageService>();

        // Setup mock storage providers for selector
        var deepArchiveProvider = Substitute.For<IStorageProvider>();
        deepArchiveProvider.ProviderName.Returns("s3-glacier-deep");
        deepArchiveProvider.Capabilities.Returns(new ProviderCapabilities
        {
            SupportsDeepArchive = true,
            SupportsFlexibleRetrieval = false
        });

        var flexibleProvider = Substitute.For<IStorageProvider>();
        flexibleProvider.ProviderName.Returns("s3-glacier-flexible");
        flexibleProvider.Capabilities.Returns(new ProviderCapabilities
        {
            SupportsDeepArchive = false,
            SupportsFlexibleRetrieval = true
        });

        _providerSelector = new StorageProviderSelector(new[] { deepArchiveProvider, flexibleProvider });

        _unitOfWork.Files.Returns(_fileRepository);

        _sut = new FileUploadService(
            _unitOfWork,
            _hashService,
            _storageService,
            _providerSelector);
    }

    [Fact]
    public async Task UploadAsync_WithNewFile_ShouldCalculateHashAndUpload()
    {
        // Arrange
        var userId = UserId.New();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "test.jpg";
        var mimeType = "image/jpeg";
        var capturedAt = DateTime.UtcNow;
        var expectedHash = "sha256:abc123";

        _hashService.CalculateSha256Async(fileStream, Arg.Any<CancellationToken>())
            .Returns(expectedHash);

        _fileRepository.GetByHashAsync(expectedHash, Arg.Any<CancellationToken>())
            .Returns((File?)null); // No duplicate

        _storageService.UploadAsync(
            Arg.Any<Stream>(),
            Arg.Any<UploadOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(new UploadResult
            {
                Location = StorageLocation.Create("s3-glacier-deep", "s3://bucket/path"),
                Success = true
            });

        // Act
        var result = await _sut.UploadAsync(
            userId,
            fileStream,
            fileName,
            mimeType,
            capturedAt);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.FileId.Should().NotBeNull();

        await _hashService.Received(1).CalculateSha256Async(fileStream, Arg.Any<CancellationToken>());
        await _fileRepository.Received(1).AddAsync(Arg.Any<File>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_WithDuplicateHash_ShouldReturnExistingFile()
    {
        // Arrange
        var userId = UserId.New();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "test.jpg";
        var mimeType = "image/jpeg";
        var capturedAt = DateTime.UtcNow;
        var expectedHash = "sha256:abc123";

        var existingFile = File.Create(
            userId,
            FileMetadata.Create(fileName, expectedHash, capturedAt),
            FileSize.FromBytes(3),
            FileType.FromMimeType(mimeType));

        // Simulate that the file was already uploaded
        existingFile.StartUpload();
        existingFile.CompleteUpload(StorageLocation.Create("s3-glacier-deep", "s3://bucket/existing"));

        _hashService.CalculateSha256Async(fileStream, Arg.Any<CancellationToken>())
            .Returns(expectedHash);

        _fileRepository.GetByHashAsync(expectedHash, Arg.Any<CancellationToken>())
            .Returns(existingFile); // Duplicate found

        // Act
        var result = await _sut.UploadAsync(
            userId,
            fileStream,
            fileName,
            mimeType,
            capturedAt);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.IsDuplicate.Should().BeTrue();
        result.FileId.Should().Be(existingFile.Id);

        // Should NOT upload or add to repository
        await _storageService.DidNotReceive().UploadAsync(
            Arg.Any<Stream>(),
            Arg.Any<UploadOptions>(),
            Arg.Any<CancellationToken>());
        await _fileRepository.DidNotReceive().AddAsync(Arg.Any<File>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_WhenStorageUploadFails_ShouldNotSaveToRepository()
    {
        // Arrange
        var userId = UserId.New();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "test.jpg";
        var mimeType = "image/jpeg";
        var capturedAt = DateTime.UtcNow;
        var expectedHash = "sha256:abc123";

        _hashService.CalculateSha256Async(fileStream, Arg.Any<CancellationToken>())
            .Returns(expectedHash);

        _fileRepository.GetByHashAsync(expectedHash, Arg.Any<CancellationToken>())
            .Returns((File?)null);

        _storageService.UploadAsync(
            Arg.Any<Stream>(),
            Arg.Any<UploadOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(new UploadResult
            {
                Success = false,
                ErrorMessage = "Storage provider error"
            });

        // Act
        var result = await _sut.UploadAsync(
            userId,
            fileStream,
            fileName,
            mimeType,
            capturedAt);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Storage provider error");

        // Should NOT save to repository
        await _fileRepository.DidNotReceive().AddAsync(Arg.Any<File>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_ShouldSelectCorrectStorageProvider()
    {
        // Arrange
        var userId = UserId.New();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "test.jpg";
        var mimeType = "image/jpeg"; // Photo - should use deep archive
        var capturedAt = DateTime.UtcNow;
        var expectedHash = "sha256:abc123";

        _hashService.CalculateSha256Async(fileStream, Arg.Any<CancellationToken>())
            .Returns(expectedHash);

        _fileRepository.GetByHashAsync(expectedHash, Arg.Any<CancellationToken>())
            .Returns((File?)null);

        UploadOptions? capturedOptions = null;
        _storageService.UploadAsync(
            Arg.Any<Stream>(),
            Arg.Do<UploadOptions>(x => capturedOptions = x),
            Arg.Any<CancellationToken>())
            .Returns(new UploadResult
            {
                Location = StorageLocation.Create("s3-glacier-deep", "s3://bucket/path"),
                Success = true
            });

        // Act
        await _sut.UploadAsync(userId, fileStream, fileName, mimeType, capturedAt);

        // Assert
        capturedOptions.Should().NotBeNull();
        // Photo should recommend deep archive
        capturedOptions!.PreferredProvider.Should().Contain("deep");
    }

    [Fact]
    public async Task UploadAsync_WithNullFileName_ShouldThrowArgumentException()
    {
        // Arrange
        var userId = UserId.New();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var capturedAt = DateTime.UtcNow;

        // Act & Assert
        var act = async () => await _sut.UploadAsync(userId, fileStream, null!, "image/jpeg", capturedAt);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UploadAsync_WithInvalidMimeType_ShouldThrowArgumentException()
    {
        // Arrange
        var userId = UserId.New();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "test.jpg";
        var capturedAt = DateTime.UtcNow;

        // Act & Assert
        var act = async () => await _sut.UploadAsync(userId, fileStream, fileName, "invalid/mime", capturedAt);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UploadAsync_ShouldSetFileProgress()
    {
        // Arrange
        var userId = UserId.New();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "test.jpg";
        var mimeType = "image/jpeg";
        var capturedAt = DateTime.UtcNow;
        var expectedHash = "sha256:abc123";

        _hashService.CalculateSha256Async(fileStream, Arg.Any<CancellationToken>())
            .Returns(expectedHash);

        _fileRepository.GetByHashAsync(expectedHash, Arg.Any<CancellationToken>())
            .Returns((File?)null);

        File? capturedFile = null;
        await _fileRepository.AddAsync(
            Arg.Do<File>(x => capturedFile = x),
            Arg.Any<CancellationToken>());

        _storageService.UploadAsync(
            Arg.Any<Stream>(),
            Arg.Any<UploadOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(new UploadResult
            {
                Location = StorageLocation.Create("s3-glacier-deep", "s3://bucket/path"),
                Success = true
            });

        // Act
        await _sut.UploadAsync(userId, fileStream, fileName, mimeType, capturedAt);

        // Assert
        capturedFile.Should().NotBeNull();
        capturedFile!.Status.CurrentState.Should().Be(UploadStatus.State.Completed);
    }

    [Fact]
    public async Task UploadAsync_WhenHashCalculationFails_ShouldReturnFailure()
    {
        // Arrange - RED: Test exception during hash calculation
        var userId = UserId.New();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "test.jpg";
        var mimeType = "image/jpeg";
        var capturedAt = DateTime.UtcNow;

        _hashService.CalculateSha256Async(fileStream, Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new InvalidOperationException("Hash calculation failed"));

        // Act
        var result = await _sut.UploadAsync(userId, fileStream, fileName, mimeType, capturedAt);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Hash calculation failed");

        // Should NOT attempt to upload or save
        await _storageService.DidNotReceive().UploadAsync(
            Arg.Any<Stream>(),
            Arg.Any<UploadOptions>(),
            Arg.Any<CancellationToken>());
        await _fileRepository.DidNotReceive().AddAsync(Arg.Any<File>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_WhenSaveChangesFails_ShouldReturnFailure()
    {
        // Arrange - RED: Test exception during SaveChanges
        var userId = UserId.New();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileName = "test.jpg";
        var mimeType = "image/jpeg";
        var capturedAt = DateTime.UtcNow;
        var expectedHash = "sha256:abc123";

        _hashService.CalculateSha256Async(fileStream, Arg.Any<CancellationToken>())
            .Returns(expectedHash);

        _fileRepository.GetByHashAsync(expectedHash, Arg.Any<CancellationToken>())
            .Returns((File?)null);

        _storageService.UploadAsync(
            Arg.Any<Stream>(),
            Arg.Any<UploadOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(new UploadResult
            {
                Location = StorageLocation.Create("s3-glacier-deep", "s3://bucket/path"),
                Success = true
            });

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<int>(x => throw new InvalidOperationException("Database error"));

        // Act
        var result = await _sut.UploadAsync(userId, fileStream, fileName, mimeType, capturedAt);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Database error");
    }

    [Fact]
    public async Task UploadAsync_WithExcessiveFileSize_ShouldHandleGracefully()
    {
        // Arrange - RED: Test handling of very large files
        var userId = UserId.New();
        var largeFileSize = 5L * 1024 * 1024 * 1024; // 5GB
        var fileStream = Substitute.For<Stream>();
        fileStream.Length.Returns(largeFileSize);
        fileStream.CanSeek.Returns(true);
        fileStream.Position.Returns(0);

        var fileName = "large-video.mp4";
        var mimeType = "video/mp4";
        var capturedAt = DateTime.UtcNow;
        var expectedHash = "sha256:largefile123";

        _hashService.CalculateSha256Async(fileStream, Arg.Any<CancellationToken>())
            .Returns(expectedHash);

        _fileRepository.GetByHashAsync(expectedHash, Arg.Any<CancellationToken>())
            .Returns((File?)null);

        _storageService.UploadAsync(
            Arg.Any<Stream>(),
            Arg.Any<UploadOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(new UploadResult
            {
                Location = StorageLocation.Create("s3-glacier-flexible", "s3://bucket/large-file"),
                Success = true
            });

        // Act
        var result = await _sut.UploadAsync(userId, fileStream, fileName, mimeType, capturedAt);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.FileId.Should().NotBeNull();

        // Verify large file was processed
        await _fileRepository.Received(1).AddAsync(Arg.Any<File>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
