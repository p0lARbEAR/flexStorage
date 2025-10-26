using FluentAssertions;
using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;
using FlexStorage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using File = FlexStorage.Domain.Entities.File;

namespace FlexStorage.Infrastructure.Tests.Persistence;

public class FileRepositoryTests : IDisposable
{
    private readonly FlexStorageDbContext _context;
    private readonly FileRepository _sut;

    public FileRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<FlexStorageDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new FlexStorageDbContext(options);
        _sut = new FileRepository(_context);
    }

    [Fact]
    public async Task AddAsync_ShouldPersistFileToDatabase()
    {
        // Arrange
        var file = CreateTestFile();

        // Act
        await _sut.AddAsync(file);
        await _context.SaveChangesAsync();

        // Assert
        var savedFile = await _context.Files.FindAsync(file.Id);
        savedFile.Should().NotBeNull();
        savedFile!.Id.Should().Be(file.Id);
        savedFile.Metadata.OriginalFileName.Should().Be(file.Metadata.OriginalFileName);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnFileWhenExists()
    {
        // Arrange
        var file = CreateTestFile();
        await _context.Files.AddAsync(file);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(file.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(file.Id);
        result.UserId.Should().Be(file.UserId);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNullWhenNotExists()
    {
        // Arrange
        var nonExistentId = FileId.New();

        // Act
        var result = await _sut.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByHashAsync_ShouldReturnFileWithMatchingHash()
    {
        // Arrange
        var hash = "sha256:abc123def456";
        var file = CreateTestFile(hash: hash);
        await _context.Files.AddAsync(file);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByHashAsync(hash);

        // Assert
        result.Should().NotBeNull();
        result!.Metadata.Hash.Should().Be(hash);
    }

    [Fact]
    public async Task GetByHashAsync_ShouldReturnNullWhenNoMatch()
    {
        // Arrange
        var file = CreateTestFile(hash: "sha256:abc123");
        await _context.Files.AddAsync(file);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByHashAsync("sha256:different");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ShouldReturnUserFilesWithPagination()
    {
        // Arrange
        var userId = UserId.New();
        var files = Enumerable.Range(0, 15)
            .Select(_ => CreateTestFile(userId: userId))
            .ToList();

        await _context.Files.AddRangeAsync(files);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByUserIdAsync(userId, page: 1, pageSize: 10);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(10);
        result.TotalCount.Should().Be(15);
        result.Page.Should().Be(1);
        result.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task GetByUserIdAsync_ShouldReturnSecondPage()
    {
        // Arrange
        var userId = UserId.New();
        var files = Enumerable.Range(0, 15)
            .Select(_ => CreateTestFile(userId: userId))
            .ToList();

        await _context.Files.AddRangeAsync(files);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByUserIdAsync(userId, page: 2, pageSize: 10);

        // Assert
        result.Items.Should().HaveCount(5);
        result.Page.Should().Be(2);
    }

    [Fact]
    public async Task SearchAsync_ShouldFilterByFileCategory()
    {
        // Arrange
        var userId = UserId.New();
        var photoFile = CreateTestFile(userId: userId, mimeType: "image/jpeg");
        var videoFile = CreateTestFile(userId: userId, mimeType: "video/mp4");

        await _context.Files.AddRangeAsync(photoFile, videoFile);
        await _context.SaveChangesAsync();

        var criteria = new FlexStorage.Application.Interfaces.Repositories.FileSearchCriteria
        {
            UserId = userId,
            Category = FileCategory.Photo
        };

        // Act
        var result = await _sut.SearchAsync(criteria);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items.First().Type.Category.Should().Be(FileCategory.Photo);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges()
    {
        // Arrange
        var file = CreateTestFile();
        await _context.Files.AddAsync(file);
        await _context.SaveChangesAsync();

        // Detach to simulate getting from repository
        _context.Entry(file).State = EntityState.Detached;

        var fileToUpdate = await _sut.GetByIdAsync(file.Id);
        fileToUpdate!.StartUpload();

        // Act
        await _sut.UpdateAsync(fileToUpdate);
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.Files.FindAsync(file.Id);
        updated!.Status.CurrentState.Should().Be(UploadStatus.State.Uploading);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveFile()
    {
        // Arrange
        var file = CreateTestFile();
        await _context.Files.AddAsync(file);
        await _context.SaveChangesAsync();

        // Act
        await _sut.DeleteAsync(file.Id);
        await _context.SaveChangesAsync();

        // Assert
        var deleted = await _context.Files.FindAsync(file.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_WithFileName_ShouldFilterResults()
    {
        // Arrange - RED: Write test first
        var userId = UserId.New();
        var file1 = CreateTestFile(userId: userId, fileName: "vacation-photo.jpg");
        var file2 = CreateTestFile(userId: userId, fileName: "birthday-party.jpg");
        var file3 = CreateTestFile(userId: userId, fileName: "work-document.pdf");

        await _context.Files.AddRangeAsync(file1, file2, file3);
        await _context.SaveChangesAsync();

        var criteria = new FlexStorage.Application.Interfaces.Repositories.FileSearchCriteria
        {
            UserId = userId,
            FileName = "photo"  // Should match "vacation-photo.jpg"
        };

        // Act
        var result = await _sut.SearchAsync(criteria);

        // Assert - This test should PASS (GREEN) because implementation exists
        result.Items.Should().HaveCount(1);
        result.Items.First().Metadata.OriginalFileName.Should().Contain("photo");
    }

    [Fact]
    public async Task SearchAsync_WithDateRange_ShouldFilterResults()
    {
        // Arrange - RED: Write test for date filtering
        var userId = UserId.New();
        var oldDate = DateTime.UtcNow.AddDays(-30);
        var recentDate = DateTime.UtcNow.AddDays(-5);
        var todayDate = DateTime.UtcNow;

        var file1 = CreateTestFile(userId: userId, fileName: "old-file.jpg", capturedAt: oldDate);
        var file2 = CreateTestFile(userId: userId, fileName: "recent-file.jpg", capturedAt: recentDate);
        var file3 = CreateTestFile(userId: userId, fileName: "today-file.jpg", capturedAt: todayDate);

        await _context.Files.AddRangeAsync(file1, file2, file3);
        await _context.SaveChangesAsync();

        var criteria = new FlexStorage.Application.Interfaces.Repositories.FileSearchCriteria
        {
            UserId = userId,
            FromDate = DateTime.UtcNow.AddDays(-10),  // Last 10 days
            ToDate = DateTime.UtcNow
        };

        // Act
        var result = await _sut.SearchAsync(criteria);

        // Assert - Should find files from last 10 days (file2 and file3)
        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain(f => f.Metadata.OriginalFileName == "recent-file.jpg");
        result.Items.Should().Contain(f => f.Metadata.OriginalFileName == "today-file.jpg");
        result.Items.Should().NotContain(f => f.Metadata.OriginalFileName == "old-file.jpg");
    }

    [Fact]
    public async Task SearchAsync_WithMultipleFilters_ShouldCombineFilters()
    {
        // Arrange - RED: Test combining multiple filters
        var userId = UserId.New();
        var recentDate = DateTime.UtcNow.AddDays(-5);

        var file1 = CreateTestFile(userId: userId, fileName: "vacation-photo.jpg", mimeType: "image/jpeg", capturedAt: recentDate);
        var file2 = CreateTestFile(userId: userId, fileName: "vacation-video.mp4", mimeType: "video/mp4", capturedAt: recentDate);
        var file3 = CreateTestFile(userId: userId, fileName: "old-photo.jpg", mimeType: "image/jpeg", capturedAt: DateTime.UtcNow.AddDays(-30));

        await _context.Files.AddRangeAsync(file1, file2, file3);
        await _context.SaveChangesAsync();

        var criteria = new FlexStorage.Application.Interfaces.Repositories.FileSearchCriteria
        {
            UserId = userId,
            FileName = "vacation",  // Match "vacation" files
            Category = FileCategory.Photo,  // Only photos
            FromDate = DateTime.UtcNow.AddDays(-10)  // Last 10 days
        };

        // Act
        var result = await _sut.SearchAsync(criteria);

        // Assert - Should only find vacation-photo.jpg (matches all 3 filters)
        result.Items.Should().HaveCount(1);
        result.Items.First().Metadata.OriginalFileName.Should().Be("vacation-photo.jpg");
        result.Items.First().Type.Category.Should().Be(FileCategory.Photo);
    }

    private File CreateTestFile(
        UserId? userId = null,
        string? hash = null,
        string? mimeType = null,
        string? fileName = null,
        DateTime? capturedAt = null)
    {
        var effectiveUserId = userId ?? UserId.New();
        var effectiveHash = hash ?? $"sha256:{Guid.NewGuid():N}";
        var effectiveMimeType = mimeType ?? "image/jpeg";
        var effectiveFileName = fileName ?? "test-file.jpg";
        var effectiveCapturedAt = capturedAt ?? DateTime.UtcNow;

        var metadata = FileMetadata.Create(
            effectiveFileName,
            effectiveHash,
            effectiveCapturedAt);

        return File.Create(
            effectiveUserId,
            metadata,
            FileSize.FromBytes(1000),
            FileType.FromMimeType(effectiveMimeType));
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
