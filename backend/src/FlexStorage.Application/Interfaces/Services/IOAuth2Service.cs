using FlexStorage.Application.DTOs;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Application.Interfaces.Services;

/// <summary>
/// Service interface for OAuth2 authentication (P1 feature).
/// Supports Authorization Code + Refresh Token grant types.
/// </summary>
public interface IOAuth2Service
{
    /// <summary>
    /// Exchanges authorization code for access and refresh tokens.
    /// </summary>
    /// <param name="authorizationCode">Authorization code from OAuth provider</param>
    /// <param name="provider">OAuth provider (Google, Apple, etc.)</param>
    /// <param name="redirectUri">Redirect URI used in authorization request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Token response with access and refresh tokens</returns>
    Task<OAuth2TokenResponse> ExchangeCodeForTokensAsync(
        string authorizationCode,
        string provider,
        string redirectUri,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes an expired access token using a refresh token.
    /// </summary>
    /// <param name="refreshToken">Valid refresh token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New token response with rotated refresh token</returns>
    Task<OAuth2TokenResponse> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes tokens and logs out user.
    /// </summary>
    /// <param name="userId">User ID to revoke tokens for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RevokeTokensAsync(
        UserId userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an access token and returns associated user information.
    /// </summary>
    /// <param name="accessToken">Access token to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User ID if token is valid, null otherwise</returns>
    Task<UserId?> ValidateAccessTokenAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the authorization URL for a given OAuth provider.
    /// </summary>
    /// <param name="provider">OAuth provider (Google, Apple, etc.)</param>
    /// <param name="redirectUri">Redirect URI after authorization</param>
    /// <param name="state">Anti-CSRF state parameter</param>
    /// <returns>Authorization URL to redirect user to</returns>
    string GetAuthorizationUrl(string provider, string redirectUri, string state);
}
