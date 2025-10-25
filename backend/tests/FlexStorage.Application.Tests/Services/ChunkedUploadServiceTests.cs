using FluentAssertions;
using FlexStorage.Application.DTOs;
using FlexStorage.Application.Interfaces.Repositories;
using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Application.Services;
using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;
using NSubstitute;
using Xunit;
using File = FlexStorage.Domain.Entities.File;

namespace FlexStorage.Application.Tests.Services;

public class ChunkedUploadServiceTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileRepository _fileRepository;
    private readonly IUploadSessionRepository _sessionRepository;
    private readonly IHashService _hashService;
    private readonly ChunkedUploadService _sut;

    public ChunkedUploadServiceTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _fileRepository = Substitute.For<IFileRepository>();
        _sessionRepository = Substitute.For<IUploadSessionRepository>();
        _hashService = Substitute.For<IHashService>();

        _unitOfWork.Files.Returns(_fileRepository);
        _unitOfWork.UploadSessions.Returns(_sessionRepository);

        _sut = new ChunkedUploadService(_unitOfWork, _hashService);
    }

    [Fact]
    public async Task InitiateUploadAsync_ShouldCreateSessionAndFile()
    {
        // Arrange
        var userId = UserId.New();
        var fileName = "large-video.mp4";
        var mimeType = "video/mp4";
        var totalSize = 100_000_000L; // 100 MB
        var capturedAt = DateTime.UtcNow;

        // Act
        var result = await _sut.InitiateUploadAsync(
            userId,
            fileName,
            mimeType,
            totalSize,
            capturedAt);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.SessionId.Should().NotBeNull();
        result.FileId.Should().NotBeNull();
        result.TotalChunks.Should().BeGreaterThan(0);

        await _fileRepository.Received(1).AddAsync(Arg.Any<File>(), Arg.Any<CancellationToken>());
        await _sessionRepository.Received(1).AddAsync(Arg.Any<UploadSession>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadChunkAsync_ShouldUpdateSessionProgress()
    {
        // Arrange
        var userId = UserId.New();
        var fileId = FileId.New();
        var session = UploadSession.Create(fileId, userId, 10_000_000, 1_000_000);
        var sessionId = session.Id;
        var chunkData = new byte[1_000_000];
        var chunkIndex = 0;

        _sessionRepository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        var result = await _sut.UploadChunkAsync(
            sessionId,
            chunkIndex,
            chunkData);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ChunkIndex.Should().Be(0);
        result.Progress.Should().BeGreaterThan(0);

        await _sessionRepository.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadChunkAsync_WithInvalidSessionId_ShouldFail()
    {
        // Arrange
        var sessionId = UploadSessionId.New();
        var chunkData = new byte[1_000_000];
        var chunkIndex = 0;

        _sessionRepository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns((UploadSession?)null);

        // Act
        var result = await _sut.UploadChunkAsync(sessionId, chunkIndex, chunkData);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Session not found");
    }

    [Fact]
    public async Task UploadChunkAsync_WithInvalidChunkIndex_ShouldFail()
    {
        // Arrange
        var userId = UserId.New();
        var fileId = FileId.New();
        var session = UploadSession.Create(fileId, userId, 10_000_000, 1_000_000);
        var sessionId = session.Id;
        var chunkData = new byte[1_000_000];
        var invalidChunkIndex = 999; // Too high

        _sessionRepository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        var result = await _sut.UploadChunkAsync(sessionId, invalidChunkIndex, chunkData);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CompleteUploadAsync_WhenAllChunksUploaded_ShouldSucceed()
    {
        // Arrange
        var userId = UserId.New();
        var fileName = "test.mp4";
        var hash = "sha256:abc123";
        var metadata = FileMetadata.Create(fileName, hash, DateTime.UtcNow);
        var file = File.Create(
            userId,
            metadata,
            FileSize.FromBytes(10_000_000),
            FileType.FromMimeType("video/mp4"));

        var session = UploadSession.Create(file.Id, userId, 10_000_000, 5_000_000);
        session.MarkChunkUploaded(0);
        session.MarkChunkUploaded(1); // All chunks uploaded

        _sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        _fileRepository.GetByIdAsync(file.Id, Arg.Any<CancellationToken>())
            .Returns(file);

        _hashService.CalculateSha256Async(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(hash);

        // Act
        var result = await _sut.CompleteUploadAsync(session.Id, new MemoryStream());

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.FileId.Should().Be(file.Id);

        session.IsComplete.Should().BeTrue();
        session.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteUploadAsync_WithMissingChunks_ShouldFail()
    {
        // Arrange
        var userId = UserId.New();
        var fileId = FileId.New();
        var session = UploadSession.Create(fileId, userId, 10_000_000, 5_000_000);
        session.MarkChunkUploaded(0); // Only 1 of 2 chunks

        _sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        var result = await _sut.CompleteUploadAsync(session.Id, new MemoryStream());

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("missing chunks");
    }

    [Fact]
    public async Task GetSessionStatusAsync_ShouldReturnProgress()
    {
        // Arrange
        var userId = UserId.New();
        var fileId = FileId.New();
        var session = UploadSession.Create(fileId, userId, 10_000_000, 2_000_000);
        session.MarkChunkUploaded(0);
        session.MarkChunkUploaded(1);

        _sessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        var status = await _sut.GetSessionStatusAsync(session.Id);

        // Assert
        status.Should().NotBeNull();
        status.SessionId.Should().Be(session.Id);
        status.Progress.Should().Be(40); // 2 of 5 chunks
        status.UploadedChunks.Should().HaveCount(2);
    }
}
