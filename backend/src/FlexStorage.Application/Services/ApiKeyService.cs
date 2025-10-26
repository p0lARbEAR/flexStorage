using System.Security.Cryptography;
using System.Text;
using FlexStorage.Application.DTOs;
using FlexStorage.Application.Interfaces.Repositories;
using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Application.Services;

/// <summary>
/// Application service for managing API keys.
/// </summary>
public class ApiKeyService : IApiKeyService
{
    private readonly IUnitOfWork _unitOfWork;

    public ApiKeyService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Generates a new API key for a user.
    /// </summary>
    public async Task<GenerateApiKeyResult> GenerateApiKeyAsync(
        UserId userId,
        string? description = null,
        int? expiresInDays = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate a cryptographically secure random API key
            var apiKey = GenerateSecureApiKey();

            // Hash the API key before storing
            var keyHash = HashApiKey(apiKey);

            // Calculate expiration date if provided
            DateTime? expiresAt = expiresInDays.HasValue
                ? DateTime.UtcNow.AddDays(expiresInDays.Value)
                : null;

            // Create the API key entity
            var apiKeyEntity = ApiKey.Create(userId, keyHash, expiresAt, description);

            // Save to database
            await _unitOfWork.ApiKeys.AddAsync(apiKeyEntity, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return GenerateApiKeyResult.SuccessResult(apiKey, expiresAt);
        }
        catch (Exception ex)
        {
            return GenerateApiKeyResult.FailureResult($"Failed to generate API key: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates an API key and returns the associated user ID.
    /// </summary>
    public async Task<ValidateApiKeyResult> ValidateApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return ValidateApiKeyResult.InvalidResult();

            // Hash the provided API key
            var keyHash = HashApiKey(apiKey);

            // Look up the API key
            var apiKeyEntity = await _unitOfWork.ApiKeys.GetByKeyHashAsync(keyHash, cancellationToken);

            if (apiKeyEntity == null)
                return ValidateApiKeyResult.InvalidResult();

            // Check if the key is valid
            if (!apiKeyEntity.IsValid())
                return ValidateApiKeyResult.InvalidResult();

            // Update last used timestamp
            apiKeyEntity.UpdateLastUsed();
            _unitOfWork.ApiKeys.Update(apiKeyEntity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ValidateApiKeyResult.SuccessResult(apiKeyEntity.UserId);
        }
        catch (Exception ex)
        {
            return ValidateApiKeyResult.FailureResult($"Failed to validate API key: {ex.Message}");
        }
    }

    /// <summary>
    /// Revokes an API key.
    /// </summary>
    public async Task<bool> RevokeApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var keyHash = HashApiKey(apiKey);
            var apiKeyEntity = await _unitOfWork.ApiKeys.GetByKeyHashAsync(keyHash, cancellationToken);

            if (apiKeyEntity == null)
                return false;

            apiKeyEntity.Revoke();
            _unitOfWork.ApiKeys.Update(apiKeyEntity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generates a cryptographically secure API key.
    /// Format: "fsk_" prefix + 32 random bytes encoded as base64url (43 characters).
    /// Total length: 47 characters.
    /// </summary>
    private static string GenerateSecureApiKey()
    {
        const int keyLength = 32; // 32 bytes = 256 bits
        var bytes = new byte[keyLength];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        // Convert to base64url (URL-safe base64 without padding)
        var base64 = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return $"fsk_{base64}"; // fsk = FlexStorage Key
    }

    /// <summary>
    /// Hashes an API key using SHA256.
    /// </summary>
    private static string HashApiKey(string apiKey)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
