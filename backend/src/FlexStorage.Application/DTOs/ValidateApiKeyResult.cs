using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Application.DTOs;

/// <summary>
/// Result of validating an API key.
/// </summary>
public class ValidateApiKeyResult
{
    public bool IsValid { get; init; }
    public UserId? UserId { get; init; }
    public string? ErrorMessage { get; init; }

    public static ValidateApiKeyResult SuccessResult(UserId userId)
    {
        return new ValidateApiKeyResult
        {
            IsValid = true,
            UserId = userId
        };
    }

    public static ValidateApiKeyResult FailureResult(string errorMessage)
    {
        return new ValidateApiKeyResult
        {
            IsValid = false,
            ErrorMessage = errorMessage
        };
    }

    public static ValidateApiKeyResult InvalidResult()
    {
        return new ValidateApiKeyResult
        {
            IsValid = false
        };
    }
}
