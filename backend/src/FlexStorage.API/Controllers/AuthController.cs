using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace FlexStorage.API.Controllers;

/// <summary>
/// Controller for API key authentication.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IApiKeyService _apiKeyService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IApiKeyService apiKeyService, ILogger<AuthController> logger)
    {
        _apiKeyService = apiKeyService;
        _logger = logger;
    }

    /// <summary>
    /// Generates a new API key for a user.
    /// </summary>
    /// <remarks>
    /// For MVP, userId is passed in the request body.
    /// In production, this would come from authenticated session.
    /// </remarks>
    [HttpPost("apikey")]
    public async Task<IActionResult> GenerateApiKey(
        [FromBody] GenerateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = UserId.From(request.UserId);

            var result = await _apiKeyService.GenerateApiKeyAsync(
                userId,
                request.Description,
                request.ExpiresInDays,
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return Ok(new
            {
                apiKey = result.ApiKey,
                expiresAt = result.ExpiresAt,
                message = "API key generated successfully. Store it securely - you won't be able to see it again."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating API key");
            return StatusCode(500, new { error = "Failed to generate API key" });
        }
    }

    /// <summary>
    /// Validates an API key.
    /// </summary>
    [HttpGet("validate")]
    public async Task<IActionResult> ValidateApiKey(CancellationToken cancellationToken)
    {
        try
        {
            // Extract API key from header
            var apiKey = ExtractApiKeyFromHeader();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Unauthorized(new { error = "API key not provided" });
            }

            var result = await _apiKeyService.ValidateApiKeyAsync(apiKey, cancellationToken);

            if (!result.IsValid)
            {
                return Unauthorized(new { error = "Invalid or expired API key" });
            }

            return Ok(new
            {
                isValid = true,
                userId = result.UserId!.Value
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating API key");
            return StatusCode(500, new { error = "Failed to validate API key" });
        }
    }

    /// <summary>
    /// Revokes an API key.
    /// </summary>
    [HttpDelete("apikey")]
    public async Task<IActionResult> RevokeApiKey(CancellationToken cancellationToken)
    {
        try
        {
            var apiKey = ExtractApiKeyFromHeader();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Unauthorized(new { error = "API key not provided" });
            }

            var result = await _apiKeyService.RevokeApiKeyAsync(apiKey, cancellationToken);

            if (!result)
            {
                return NotFound(new { error = "API key not found" });
            }

            return Ok(new { message = "API key revoked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking API key");
            return StatusCode(500, new { error = "Failed to revoke API key" });
        }
    }

    /// <summary>
    /// Extracts API key from X-API-Key header or Authorization header.
    /// </summary>
    private string? ExtractApiKeyFromHeader()
    {
        // Try X-API-Key header first
        if (Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader))
        {
            return apiKeyHeader.ToString();
        }

        // Try Authorization header with "ApiKey" scheme
        if (Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var authValue = authHeader.ToString();
            if (authValue.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
            {
                return authValue.Substring("ApiKey ".Length).Trim();
            }
        }

        return null;
    }
}

/// <summary>
/// Request model for generating an API key.
/// </summary>
public class GenerateApiKeyRequest
{
    /// <summary>
    /// The user ID for which to generate the API key.
    /// In production, this would come from the authenticated session.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Optional description for the API key.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Number of days until the key expires (null = never expires).
    /// </summary>
    public int? ExpiresInDays { get; set; }
}
