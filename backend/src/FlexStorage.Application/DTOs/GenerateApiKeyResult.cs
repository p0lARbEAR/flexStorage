namespace FlexStorage.Application.DTOs;

/// <summary>
/// Result of generating a new API key.
/// </summary>
public class GenerateApiKeyResult
{
    public bool Success { get; init; }
    public string? ApiKey { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? ErrorMessage { get; init; }

    public static GenerateApiKeyResult SuccessResult(string apiKey, DateTime? expiresAt = null)
    {
        return new GenerateApiKeyResult
        {
            Success = true,
            ApiKey = apiKey,
            ExpiresAt = expiresAt
        };
    }

    public static GenerateApiKeyResult FailureResult(string errorMessage)
    {
        return new GenerateApiKeyResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
