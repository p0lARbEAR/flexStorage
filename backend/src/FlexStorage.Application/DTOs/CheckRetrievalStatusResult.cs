using FlexStorage.Domain.DomainServices;

namespace FlexStorage.Application.DTOs;

/// <summary>
/// Result of checking retrieval status.
/// </summary>
public class CheckRetrievalStatusResult
{
    public bool Success { get; init; }
    public RetrievalStatus Status { get; init; }
    public int ProgressPercentage { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? ErrorMessage { get; init; }

    public static CheckRetrievalStatusResult SuccessResult(
        RetrievalStatus status,
        int progressPercentage,
        DateTime? completedAt = null)
    {
        return new CheckRetrievalStatusResult
        {
            Success = true,
            Status = status,
            ProgressPercentage = progressPercentage,
            CompletedAt = completedAt
        };
    }

    public static CheckRetrievalStatusResult FailureResult(string errorMessage)
    {
        return new CheckRetrievalStatusResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
