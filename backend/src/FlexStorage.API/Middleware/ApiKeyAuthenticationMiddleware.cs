using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.API.Middleware;

/// <summary>
/// Middleware for validating API keys on incoming requests.
/// </summary>
public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

    // Paths that don't require authentication
    private static readonly HashSet<string> _publicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/apikey",      // Generate API key
        "/swagger",              // Swagger UI
        "/swagger/index.html",
        "/swagger/v1/swagger.json",
        "/health"                // Health check
    };

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IApiKeyService apiKeyService)
    {
        // Skip authentication for public paths
        if (IsPublicPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Extract API key from header
        var apiKey = ExtractApiKeyFromHeader(context.Request);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Request to {Path} rejected: No API key provided", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "API key required" });
            return;
        }

        // Validate API key
        var result = await apiKeyService.ValidateApiKeyAsync(apiKey);

        if (!result.IsValid)
        {
            _logger.LogWarning("Request to {Path} rejected: Invalid API key", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or expired API key" });
            return;
        }

        // Store user ID in HttpContext for controllers to use
        context.Items["UserId"] = result.UserId;

        await _next(context);
    }

    private static bool IsPublicPath(PathString path)
    {
        var pathValue = path.Value ?? "";

        // Check exact matches
        if (_publicPaths.Contains(pathValue))
            return true;

        // Check if path starts with any public path prefix
        return _publicPaths.Any(publicPath =>
            pathValue.StartsWith(publicPath, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractApiKeyFromHeader(HttpRequest request)
    {
        // Try X-API-Key header first
        if (request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader))
        {
            return apiKeyHeader.ToString();
        }

        // Try Authorization header with "ApiKey" scheme
        if (request.Headers.TryGetValue("Authorization", out var authHeader))
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
/// Extension methods for adding API key authentication middleware.
/// </summary>
public static class ApiKeyAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }
}

/// <summary>
/// Extension methods for accessing authenticated user from HttpContext.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Gets the authenticated user ID from the current HTTP context.
    /// Returns null if no user is authenticated.
    /// </summary>
    public static UserId? GetAuthenticatedUserId(this HttpContext context)
    {
        return context.Items["UserId"] as UserId;
    }
}
