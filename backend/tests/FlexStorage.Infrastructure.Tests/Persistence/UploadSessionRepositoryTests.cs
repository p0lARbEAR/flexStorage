using FluentAssertions;
using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;
using FlexStorage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FlexStorage.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests for UploadSessionRepository using EF Core InMemory database.
/// Following TDD: Red-Green-Refactor cycle.
/// </summary>
public class UploadSessionRepositoryTests : IDisposable
{
    private readonly FlexStorageDbContext _context;
    private readonly UploadSessionRepository _sut;

    public UploadSessionRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<FlexStorageDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new FlexStorageDbContext(options);
        _sut = new UploadSessionRepository(_context);
    }

    [Fact]
    public async Task AddAsync_ShouldPersistSessionToDatabase()
    {
        // Arrange - RED: Test adding upload session to database
        var userId = UserId.New();
        var fileId = FileId.New();
        var session = UploadSession.Create(fileId, userId, totalSize: 10_000_000, chunkSize: 2_000_000);

        // Act
        await _sut.AddAsync(session);
        await _context.SaveChangesAsync();

        // Assert
        var retrieved = await _context.UploadSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        retrieved.Should().NotBeNull();
        retrieved!.UserId.Should().Be(userId);
        retrieved.FileId.Should().Be(fileId);
        retrieved.TotalChunks.Should().Be(5);
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnSession()
    {
        // Arrange - RED: Test retrieving session by ID
        var userId = UserId.New();
        var fileId = FileId.New();
        var session = UploadSession.Create(fileId, userId, totalSize: 5_000_000, chunkSize: 1_000_000);

        await _context.UploadSessions.AddAsync(session);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(session.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(session.Id);
        result.UserId.Should().Be(userId);
        result.FileId.Should().Be(fileId);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange - RED: Test session not found scenario
        var invalidId = UploadSessionId.New();

        // Act
        var result = await _sut.GetByIdAsync(invalidId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveSessionsAsync_ShouldReturnOnlyActiveSessions()
    {
        // Arrange - RED: Test filtering active sessions
        var userId = UserId.New();
        var otherUserId = UserId.New();

        // Active session for user
        var activeSession = UploadSession.Create(FileId.New(), userId, 10_000_000, 2_000_000);

        // Completed session for user
        var completedSession = UploadSession.Create(FileId.New(), userId, 10_000_000, 2_000_000);
        for (int i = 0; i < completedSession.TotalChunks; i++)
        {
            completedSession.MarkChunkUploaded(i);
        }
        completedSession.Complete();

        // Expired session for user (simulate by creating with past expiration)
        var expiredSession = UploadSession.Create(FileId.New(), userId, 10_000_000, 2_000_000);
        // Note: Cannot directly set ExpiresAt, so this session will not be expired in test

        // Session for other user
        var otherUserSession = UploadSession.Create(FileId.New(), otherUserId, 10_000_000, 2_000_000);

        await _context.UploadSessions.AddRangeAsync(activeSession, completedSession, expiredSession, otherUserSession);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetActiveSessionsAsync(userId);

        // Assert
        result.Should().Contain(s => s.Id == activeSession.Id);
        result.Should().Contain(s => s.Id == expiredSession.Id); // Not expired yet
        result.Should().NotContain(s => s.Id == completedSession.Id); // Completed
        result.Should().NotContain(s => s.UserId == otherUserId); // Different user
    }

    [Fact]
    public async Task GetActiveSessionsAsync_WithNoActiveSessions_ShouldReturnEmptyList()
    {
        // Arrange - RED: Test user with no active sessions
        var userId = UserId.New();

        // Act
        var result = await _sut.GetActiveSessionsAsync(userId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges()
    {
        // Arrange - RED: Test updating upload session
        var userId = UserId.New();
        var fileId = FileId.New();
        var session = UploadSession.Create(fileId, userId, totalSize: 10_000_000, chunkSize: 2_000_000);

        await _context.UploadSessions.AddAsync(session);
        await _context.SaveChangesAsync();

        // Modify the session
        session.MarkChunkUploaded(0);
        session.MarkChunkUploaded(1);

        // Act
        await _sut.UpdateAsync(session);
        await _context.SaveChangesAsync();

        // Assert
        var retrieved = await _context.UploadSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        retrieved.Should().NotBeNull();
        retrieved!.UploadedChunks.Should().HaveCount(2);
        retrieved.Progress.Should().Be(40); // 2 of 5 chunks = 40%
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveSession()
    {
        // Arrange - RED: Test deleting upload session
        var userId = UserId.New();
        var fileId = FileId.New();
        var session = UploadSession.Create(fileId, userId, totalSize: 10_000_000, chunkSize: 2_000_000);

        await _context.UploadSessions.AddAsync(session);
        await _context.SaveChangesAsync();

        // Act
        await _sut.DeleteAsync(session.Id);
        await _context.SaveChangesAsync();

        // Assert
        var retrieved = await _context.UploadSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        retrieved.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
