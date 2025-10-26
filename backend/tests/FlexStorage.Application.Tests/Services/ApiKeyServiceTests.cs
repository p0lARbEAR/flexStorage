using FlexStorage.Application.Interfaces.Repositories;
using FlexStorage.Application.Services;
using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace FlexStorage.Application.Tests.Services;

public class ApiKeyServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IApiKeyRepository> _apiKeyRepositoryMock;
    private readonly ApiKeyService _service;

    public ApiKeyServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _apiKeyRepositoryMock = new Mock<IApiKeyRepository>();
        _unitOfWorkMock.Setup(x => x.ApiKeys).Returns(_apiKeyRepositoryMock.Object);
        _service = new ApiKeyService(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task GenerateApiKeyAsync_ShouldReturnSuccessWithValidApiKey()
    {
        // Arrange
        var userId = UserId.New();
        var description = "Test API Key";

        // Act
        var result = await _service.GenerateApiKeyAsync(userId, description);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ApiKey.Should().NotBeNullOrWhiteSpace();
        result.ApiKey.Should().StartWith("fsk_");
        result.ErrorMessage.Should().BeNull();

        _apiKeyRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateApiKeyAsync_WithExpirationDays_ShouldSetExpiresAt()
    {
        // Arrange
        var userId = UserId.New();
        var expiresInDays = 30;

        // Act
        var result = await _service.GenerateApiKeyAsync(userId, expiresInDays: expiresInDays);

        // Assert
        result.Success.Should().BeTrue();
        result.ExpiresAt.Should().NotBeNull();
        result.ExpiresAt.Should().BeCloseTo(
            DateTime.UtcNow.AddDays(expiresInDays),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GenerateApiKeyAsync_WithoutExpirationDays_ShouldNotSetExpiresAt()
    {
        // Arrange
        var userId = UserId.New();

        // Act
        var result = await _service.GenerateApiKeyAsync(userId);

        // Assert
        result.Success.Should().BeTrue();
        result.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithValidKey_ShouldReturnSuccess()
    {
        // Arrange
        var userId = UserId.New();
        var apiKeyEntity = ApiKey.Create(userId, "hashed_key");

        _apiKeyRepositoryMock
            .Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiKeyEntity);

        // Act
        var result = await _service.ValidateApiKeyAsync("fsk_somevalidkey");

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.UserId.Should().Be(userId);
        result.ErrorMessage.Should().BeNull();

        _apiKeyRepositoryMock.Verify(
            x => x.Update(It.IsAny<ApiKey>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithNonExistentKey_ShouldReturnInvalid()
    {
        // Arrange
        _apiKeyRepositoryMock
            .Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiKey?)null);

        // Act
        var result = await _service.ValidateApiKeyAsync("fsk_nonexistentkey");

        // Assert
        result.IsValid.Should().BeFalse();
        result.UserId.Should().BeNull();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithRevokedKey_ShouldReturnInvalid()
    {
        // Arrange
        var userId = UserId.New();
        var apiKeyEntity = ApiKey.Create(userId, "hashed_key");
        apiKeyEntity.Revoke();

        _apiKeyRepositoryMock
            .Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiKeyEntity);

        // Act
        var result = await _service.ValidateApiKeyAsync("fsk_revokedkey");

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithExpiredKey_ShouldReturnInvalid()
    {
        // Arrange
        var userId = UserId.New();
        var expiresAt = DateTime.UtcNow.AddDays(-1); // Expired yesterday
        var apiKeyEntity = ApiKey.Create(userId, "hashed_key", expiresAt);

        _apiKeyRepositoryMock
            .Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiKeyEntity);

        // Act
        var result = await _service.ValidateApiKeyAsync("fsk_expiredkey");

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithEmptyKey_ShouldReturnInvalid()
    {
        // Arrange & Act
        var result = await _service.ValidateApiKeyAsync("");

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WithValidKey_ShouldReturnTrue()
    {
        // Arrange
        var userId = UserId.New();
        var apiKeyEntity = ApiKey.Create(userId, "hashed_key");

        _apiKeyRepositoryMock
            .Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiKeyEntity);

        // Act
        var result = await _service.RevokeApiKeyAsync("fsk_validkey");

        // Assert
        result.Should().BeTrue();
        apiKeyEntity.IsRevoked.Should().BeTrue();

        _apiKeyRepositoryMock.Verify(
            x => x.Update(It.IsAny<ApiKey>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WithNonExistentKey_ShouldReturnFalse()
    {
        // Arrange
        _apiKeyRepositoryMock
            .Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiKey?)null);

        // Act
        var result = await _service.RevokeApiKeyAsync("fsk_nonexistentkey");

        // Assert
        result.Should().BeFalse();
    }
}