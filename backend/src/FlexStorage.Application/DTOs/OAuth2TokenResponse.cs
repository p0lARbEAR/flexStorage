using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Application.DTOs;

/// <summary>
/// Response containing OAuth2 tokens after successful authentication.
/// </summary>
public class OAuth2TokenResponse
{
    /// <summary>
    /// Indicates if the token exchange was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Access token (1 hour expiry).
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// Refresh token (90 days expiry, rotates on refresh).
    /// </summary>
    public string? RefreshToken { get; init; }

    /// <summary>
    /// Token type (usually "Bearer").
    /// </summary>
    public string? TokenType { get; init; } = "Bearer";

    /// <summary>
    /// Access token expiration time in seconds (default 3600 = 1 hour).
    /// </summary>
    public int ExpiresIn { get; init; } = 3600;

    /// <summary>
    /// User ID associated with the tokens.
    /// </summary>
    public UserId? UserId { get; init; }

    /// <summary>
    /// Error message if Success is false.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// OAuth provider used (Google, Apple, Email, etc.).
    /// </summary>
    public string? Provider { get; init; }

    /// <summary>
    /// User's email from OAuth provider (if available).
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// User's display name from OAuth provider (if available).
    /// </summary>
    public string? DisplayName { get; init; }

    public static OAuth2TokenResponse SuccessResult(
        string accessToken,
        string refreshToken,
        UserId userId,
        string provider,
        string? email = null,
        string? displayName = null)
    {
        return new OAuth2TokenResponse
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            UserId = userId,
            Provider = provider,
            Email = email,
            DisplayName = displayName
        };
    }

    public static OAuth2TokenResponse FailureResult(string errorMessage)
    {
        return new OAuth2TokenResponse
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
