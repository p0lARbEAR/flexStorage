using FluentAssertions;
using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;
using FlexStorage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FlexStorage.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests for ApiKeyRepository using EF Core InMemory database.
/// Following TDD: Red-Green-Refactor cycle.
/// </summary>
public class ApiKeyRepositoryTests : IDisposable
{
    private readonly FlexStorageDbContext _context;
    private readonly ApiKeyRepository _sut;

    public ApiKeyRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<FlexStorageDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new FlexStorageDbContext(options);
        _sut = new ApiKeyRepository(_context);
    }

    [Fact]
    public async Task AddAsync_ShouldPersistApiKeyToDatabase()
    {
        // Arrange - RED: Test adding API key to database
        var userId = UserId.New();
        var keyHash = $"sha256:{Guid.NewGuid():N}";
        var expiresAt = DateTime.UtcNow.AddDays(30);
        var apiKey = ApiKey.Create(userId, keyHash, expiresAt, "Test API Key");

        // Act
        await _sut.AddAsync(apiKey);
        await _context.SaveChangesAsync();

        // Assert
        var retrieved = await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKey.Id);
        retrieved.Should().NotBeNull();
        retrieved!.UserId.Should().Be(userId);
        retrieved.Description.Should().Be("Test API Key");
    }

    [Fact]
    public async Task GetByKeyHashAsync_WithValidHash_ShouldReturnApiKey()
    {
        // Arrange - RED: Test retrieving API key by hash
        var userId = UserId.New();
        var keyHash = $"sha256:{Guid.NewGuid():N}";
        var apiKey = ApiKey.Create(userId, keyHash, DateTime.UtcNow.AddDays(30), "Test Key");

        await _context.ApiKeys.AddAsync(apiKey);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByKeyHashAsync(apiKey.KeyHash);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(apiKey.Id);
        result.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task GetByKeyHashAsync_WithInvalidHash_ShouldReturnNull()
    {
        // Arrange - RED: Test hash not found scenario
        var invalidHash = "sha256:nonexistent";

        // Act
        var result = await _sut.GetByKeyHashAsync(invalidHash);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ShouldReturnKeysOrderedByNewestFirst()
    {
        // Arrange - RED: Test retrieving keys by user ID with ordering
        var userId = UserId.New();
        var otherUserId = UserId.New();

        var key1 = ApiKey.Create(userId, $"sha256:{Guid.NewGuid():N}", DateTime.UtcNow.AddDays(30), "Key 1");
        await Task.Delay(10); // Small delay to ensure different timestamps
        var key2 = ApiKey.Create(userId, $"sha256:{Guid.NewGuid():N}", DateTime.UtcNow.AddDays(60), "Key 2");
        var otherKey = ApiKey.Create(otherUserId, $"sha256:{Guid.NewGuid():N}", DateTime.UtcNow.AddDays(30), "Other Key");

        await _context.ApiKeys.AddRangeAsync(key1, key2, otherKey);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByUserIdAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result[0].Description.Should().Be("Key 2"); // Newest first
        result[1].Description.Should().Be("Key 1");
        result.Should().NotContain(k => k.UserId == otherUserId);
    }

    [Fact]
    public async Task GetByUserIdAsync_WithNoKeys_ShouldReturnEmptyList()
    {
        // Arrange - RED: Test user with no API keys
        var userId = UserId.New();

        // Act
        var result = await _sut.GetByUserIdAsync(userId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_ShouldPersistChanges()
    {
        // Arrange - RED: Test updating API key
        var userId = UserId.New();
        var keyHash = $"sha256:{Guid.NewGuid():N}";
        var apiKey = ApiKey.Create(userId, keyHash, DateTime.UtcNow.AddDays(30), "Original Description");

        await _context.ApiKeys.AddAsync(apiKey);
        await _context.SaveChangesAsync();

        // Modify the key
        apiKey.Revoke();

        // Act
        _sut.Update(apiKey);
        await _context.SaveChangesAsync();

        // Assert
        var retrieved = await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKey.Id);
        retrieved.Should().NotBeNull();
        retrieved!.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_ShouldRemoveApiKey()
    {
        // Arrange - RED: Test deleting API key
        var userId = UserId.New();
        var keyHash = $"sha256:{Guid.NewGuid():N}";
        var apiKey = ApiKey.Create(userId, keyHash, DateTime.UtcNow.AddDays(30), "To Delete");

        await _context.ApiKeys.AddAsync(apiKey);
        await _context.SaveChangesAsync();

        // Act
        _sut.Delete(apiKey);
        await _context.SaveChangesAsync();

        // Assert
        var retrieved = await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKey.Id);
        retrieved.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
