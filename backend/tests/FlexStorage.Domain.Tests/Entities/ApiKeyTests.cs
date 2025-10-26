using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;
using FluentAssertions;

namespace FlexStorage.Domain.Tests.Entities;

public class ApiKeyTests
{
    [Fact]
    public void Create_ShouldCreateValidApiKey()
    {
        // Arrange
        var userId = UserId.New();
        var keyHash = "hashed_key_value";
        var description = "Test API Key";

        // Act
        var apiKey = ApiKey.Create(userId, keyHash, description: description);

        // Assert
        apiKey.Should().NotBeNull();
        apiKey.Id.Should().NotBeNull();
        apiKey.UserId.Should().Be(userId);
        apiKey.KeyHash.Should().Be(keyHash);
        apiKey.Description.Should().Be(description);
        apiKey.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        apiKey.ExpiresAt.Should().BeNull();
        apiKey.IsRevoked.Should().BeFalse();
        apiKey.LastUsedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithExpirationDate_ShouldSetExpiresAt()
    {
        // Arrange
        var userId = UserId.New();
        var keyHash = "hashed_key_value";
        var expiresAt = DateTime.UtcNow.AddDays(30);

        // Act
        var apiKey = ApiKey.Create(userId, keyHash, expiresAt);

        // Assert
        apiKey.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public void Create_WithNullUserId_ShouldThrowArgumentNullException()
    {
        // Arrange
        UserId userId = null!;
        var keyHash = "hashed_key_value";

        // Act
        var act = () => ApiKey.Create(userId, keyHash);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("userId");
    }

    [Fact]
    public void Create_WithEmptyKeyHash_ShouldThrowArgumentException()
    {
        // Arrange
        var userId = UserId.New();
        var keyHash = "";

        // Act
        var act = () => ApiKey.Create(userId, keyHash);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("keyHash");
    }

    [Fact]
    public void IsValid_WithValidApiKey_ShouldReturnTrue()
    {
        // Arrange
        var userId = UserId.New();
        var apiKey = ApiKey.Create(userId, "hashed_key");

        // Act
        var isValid = apiKey.IsValid();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithRevokedApiKey_ShouldReturnFalse()
    {
        // Arrange
        var userId = UserId.New();
        var apiKey = ApiKey.Create(userId, "hashed_key");
        apiKey.Revoke();

        // Act
        var isValid = apiKey.IsValid();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithExpiredApiKey_ShouldReturnFalse()
    {
        // Arrange
        var userId = UserId.New();
        var expiresAt = DateTime.UtcNow.AddDays(-1); // Expired yesterday
        var apiKey = ApiKey.Create(userId, "hashed_key", expiresAt);

        // Act
        var isValid = apiKey.IsValid();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithFutureExpirationDate_ShouldReturnTrue()
    {
        // Arrange
        var userId = UserId.New();
        var expiresAt = DateTime.UtcNow.AddDays(30);
        var apiKey = ApiKey.Create(userId, "hashed_key", expiresAt);

        // Act
        var isValid = apiKey.IsValid();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void Revoke_ShouldSetIsRevokedToTrue()
    {
        // Arrange
        var userId = UserId.New();
        var apiKey = ApiKey.Create(userId, "hashed_key");

        // Act
        apiKey.Revoke();

        // Assert
        apiKey.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void UpdateLastUsed_ShouldSetLastUsedAt()
    {
        // Arrange
        var userId = UserId.New();
        var apiKey = ApiKey.Create(userId, "hashed_key");

        // Act
        apiKey.UpdateLastUsed();

        // Assert
        apiKey.LastUsedAt.Should().NotBeNull();
        apiKey.LastUsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
