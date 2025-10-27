using FlexStorage.API.Controllers;
using FlexStorage.Application.DTOs;
using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FlexStorage.API.Tests;

/// <summary>
/// Tests for AuthController API Key authentication functionality.
/// Following TDD: Red-Green-Refactor cycle.
/// </summary>
public class AuthControllerTests
{
    private readonly Mock<IApiKeyService> _apiKeyServiceMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _apiKeyServiceMock = new Mock<IApiKeyService>();
        _loggerMock = new Mock<ILogger<AuthController>>();
        _controller = new AuthController(_apiKeyServiceMock.Object, _loggerMock.Object);

        // Setup default HttpContext
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    #region GenerateApiKey Tests

    [Fact]
    public async Task GenerateApiKey_WithValidRequest_ShouldReturnOkWithApiKey()
    {
        // Arrange - RED: Test API key generation
        var userId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");
        var request = new GenerateApiKeyRequest
        {
            UserId = userId,
            Description = "Test API Key",
            ExpiresInDays = 30
        };

        var expectedApiKey = "fsk_test1234567890abcdefghijklmnopqrstuvwxyz";
        var expectedExpiresAt = DateTime.UtcNow.AddDays(30);

        var generateResult = new GenerateApiKeyResult
        {
            Success = true,
            ApiKey = expectedApiKey,
            ExpiresAt = expectedExpiresAt
        };

        _apiKeyServiceMock
            .Setup(s => s.GenerateApiKeyAsync(
                It.Is<UserId>(u => u.Value == userId),
                request.Description,
                request.ExpiresInDays,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(generateResult);

        // Act
        var result = await _controller.GenerateApiKey(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var response = okResult.Value;
        var apiKeyProperty = response.GetType().GetProperty("apiKey");
        var expiresAtProperty = response.GetType().GetProperty("expiresAt");
        var messageProperty = response.GetType().GetProperty("message");

        Assert.NotNull(apiKeyProperty);
        Assert.NotNull(expiresAtProperty);
        Assert.NotNull(messageProperty);

        Assert.Equal(expectedApiKey, apiKeyProperty.GetValue(response));
        Assert.Equal(expectedExpiresAt, expiresAtProperty.GetValue(response));
        Assert.Contains("Store it securely", messageProperty.GetValue(response)?.ToString());

        _apiKeyServiceMock.Verify(
            s => s.GenerateApiKeyAsync(
                It.Is<UserId>(u => u.Value == userId),
                request.Description,
                request.ExpiresInDays,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateApiKey_WhenServiceFails_ShouldReturnBadRequest()
    {
        // Arrange - RED: Test generation failure handling
        var userId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");
        var request = new GenerateApiKeyRequest
        {
            UserId = userId,
            Description = "Test Key"
        };

        var generateResult = new GenerateApiKeyResult
        {
            Success = false,
            ErrorMessage = "User quota exceeded"
        };

        _apiKeyServiceMock
            .Setup(s => s.GenerateApiKeyAsync(
                It.IsAny<UserId>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(generateResult);

        // Act
        var result = await _controller.GenerateApiKey(request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);

        var response = badRequestResult.Value;
        var errorProperty = response.GetType().GetProperty("error");
        Assert.NotNull(errorProperty);
        Assert.Equal("User quota exceeded", errorProperty.GetValue(response));
    }

    [Fact]
    public async Task GenerateApiKey_WithNeverExpiring_ShouldAcceptNullExpiresInDays()
    {
        // Arrange - RED: Test API key with no expiration
        var userId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");
        var request = new GenerateApiKeyRequest
        {
            UserId = userId,
            Description = "Never expiring key",
            ExpiresInDays = null
        };

        var generateResult = new GenerateApiKeyResult
        {
            Success = true,
            ApiKey = "fsk_neverexpire123",
            ExpiresAt = null
        };

        _apiKeyServiceMock
            .Setup(s => s.GenerateApiKeyAsync(
                It.IsAny<UserId>(),
                request.Description,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(generateResult);

        // Act
        var result = await _controller.GenerateApiKey(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        _apiKeyServiceMock.Verify(
            s => s.GenerateApiKeyAsync(
                It.IsAny<UserId>(),
                request.Description,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region ValidateApiKey Tests

    [Fact]
    public async Task ValidateApiKey_WithValidXApiKeyHeader_ShouldReturnOkWithUserId()
    {
        // Arrange - RED: Test X-API-Key header validation
        var apiKey = "fsk_validkey123";
        var userId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");

        _controller.ControllerContext.HttpContext.Request.Headers["X-API-Key"] = apiKey;

        var validationResult = new ValidateApiKeyResult
        {
            IsValid = true,
            UserId = UserId.From(userId)
        };

        _apiKeyServiceMock
            .Setup(s => s.ValidateApiKeyAsync(apiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidateApiKey(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var response = okResult.Value;
        var isValidProperty = response.GetType().GetProperty("isValid");
        var userIdProperty = response.GetType().GetProperty("userId");

        Assert.NotNull(isValidProperty);
        Assert.NotNull(userIdProperty);

        Assert.True((bool)isValidProperty.GetValue(response)!);
        Assert.Equal(userId, userIdProperty.GetValue(response));

        _apiKeyServiceMock.Verify(
            s => s.ValidateApiKeyAsync(apiKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateApiKey_WithValidAuthorizationHeader_ShouldReturnOkWithUserId()
    {
        // Arrange - RED: Test Authorization header with ApiKey scheme
        var apiKey = "fsk_validkey456";
        var userId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");

        _controller.ControllerContext.HttpContext.Request.Headers["Authorization"] = $"ApiKey {apiKey}";

        var validationResult = new ValidateApiKeyResult
        {
            IsValid = true,
            UserId = UserId.From(userId)
        };

        _apiKeyServiceMock
            .Setup(s => s.ValidateApiKeyAsync(apiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidateApiKey(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        _apiKeyServiceMock.Verify(
            s => s.ValidateApiKeyAsync(apiKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateApiKey_WithMissingApiKey_ShouldReturnUnauthorized()
    {
        // Arrange - RED: Test missing API key returns 401
        // No headers set

        // Act
        var result = await _controller.ValidateApiKey(CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorizedResult.Value);

        var response = unauthorizedResult.Value;
        var errorProperty = response.GetType().GetProperty("error");
        Assert.NotNull(errorProperty);
        Assert.Equal("API key not provided", errorProperty.GetValue(response));

        _apiKeyServiceMock.Verify(
            s => s.ValidateApiKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateApiKey_WithInvalidApiKey_ShouldReturnUnauthorized()
    {
        // Arrange - RED: Test invalid API key returns 401
        var apiKey = "fsk_invalidkey";

        _controller.ControllerContext.HttpContext.Request.Headers["X-API-Key"] = apiKey;

        var validationResult = new ValidateApiKeyResult
        {
            IsValid = false
        };

        _apiKeyServiceMock
            .Setup(s => s.ValidateApiKeyAsync(apiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidateApiKey(CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorizedResult.Value);

        var response = unauthorizedResult.Value;
        var errorProperty = response.GetType().GetProperty("error");
        Assert.NotNull(errorProperty);
        Assert.Equal("Invalid or expired API key", errorProperty.GetValue(response));
    }

    [Fact]
    public async Task ValidateApiKey_WithExpiredApiKey_ShouldReturnUnauthorized()
    {
        // Arrange - RED: Test expired API key returns 401
        var apiKey = "fsk_expiredkey";

        _controller.ControllerContext.HttpContext.Request.Headers["X-API-Key"] = apiKey;

        var validationResult = new ValidateApiKeyResult
        {
            IsValid = false
        };

        _apiKeyServiceMock
            .Setup(s => s.ValidateApiKeyAsync(apiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidateApiKey(CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorizedResult.Value);
    }

    #endregion

    #region RevokeApiKey Tests

    [Fact]
    public async Task RevokeApiKey_WithValidApiKey_ShouldReturnOk()
    {
        // Arrange - RED: Test API key revocation
        var apiKey = "fsk_validkey789";

        _controller.ControllerContext.HttpContext.Request.Headers["X-API-Key"] = apiKey;

        _apiKeyServiceMock
            .Setup(s => s.RevokeApiKeyAsync(apiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.RevokeApiKey(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var response = okResult.Value;
        var messageProperty = response.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        Assert.Equal("API key revoked successfully", messageProperty.GetValue(response));

        _apiKeyServiceMock.Verify(
            s => s.RevokeApiKeyAsync(apiKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RevokeApiKey_WithMissingApiKey_ShouldReturnUnauthorized()
    {
        // Arrange - RED: Test revoke without API key
        // No headers set

        // Act
        var result = await _controller.RevokeApiKey(CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorizedResult.Value);

        var response = unauthorizedResult.Value;
        var errorProperty = response.GetType().GetProperty("error");
        Assert.NotNull(errorProperty);
        Assert.Equal("API key not provided", errorProperty.GetValue(response));

        _apiKeyServiceMock.Verify(
            s => s.RevokeApiKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RevokeApiKey_WithNonExistentApiKey_ShouldReturnNotFound()
    {
        // Arrange - RED: Test revoke with non-existent API key
        var apiKey = "fsk_nonexistent";

        _controller.ControllerContext.HttpContext.Request.Headers["X-API-Key"] = apiKey;

        _apiKeyServiceMock
            .Setup(s => s.RevokeApiKeyAsync(apiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.RevokeApiKey(CancellationToken.None);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFoundResult.Value);

        var response = notFoundResult.Value;
        var errorProperty = response.GetType().GetProperty("error");
        Assert.NotNull(errorProperty);
        Assert.Equal("API key not found", errorProperty.GetValue(response));
    }

    #endregion

    #region Header Extraction Tests

    [Fact]
    public async Task ValidateApiKey_WithBothHeaders_ShouldPrioritizeXApiKeyHeader()
    {
        // Arrange - RED: Test X-API-Key takes precedence over Authorization
        var xApiKey = "fsk_xapikey123";
        var authApiKey = "fsk_authkey456";
        var userId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");

        _controller.ControllerContext.HttpContext.Request.Headers["X-API-Key"] = xApiKey;
        _controller.ControllerContext.HttpContext.Request.Headers["Authorization"] = $"ApiKey {authApiKey}";

        var validationResult = new ValidateApiKeyResult
        {
            IsValid = true,
            UserId = UserId.From(userId)
        };

        _apiKeyServiceMock
            .Setup(s => s.ValidateApiKeyAsync(xApiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.ValidateApiKey(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Should have called with X-API-Key, not Authorization header
        _apiKeyServiceMock.Verify(
            s => s.ValidateApiKeyAsync(xApiKey, It.IsAny<CancellationToken>()),
            Times.Once);

        _apiKeyServiceMock.Verify(
            s => s.ValidateApiKeyAsync(authApiKey, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateApiKey_WithEmptyApiKey_ShouldReturnUnauthorized()
    {
        // Arrange - RED: Test empty API key string
        _controller.ControllerContext.HttpContext.Request.Headers["X-API-Key"] = "   ";

        // Act
        var result = await _controller.ValidateApiKey(CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorizedResult.Value);

        _apiKeyServiceMock.Verify(
            s => s.ValidateApiKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion
}
