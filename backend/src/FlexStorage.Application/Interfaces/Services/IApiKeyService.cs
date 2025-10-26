using FlexStorage.Application.DTOs;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Application.Interfaces.Services;

/// <summary>
/// Service for managing API keys.
/// </summary>
public interface IApiKeyService
{
    /// <summary>
    /// Generates a new API key for a user.
    /// </summary>
    Task<GenerateApiKeyResult> GenerateApiKeyAsync(
        UserId userId,
        string? description = null,
        int? expiresInDays = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an API key and returns the associated user ID.
    /// </summary>
    Task<ValidateApiKeyResult> ValidateApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes an API key.
    /// </summary>
    Task<bool> RevokeApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default);
}
