namespace FlexStorage.Application.DTOs;

/// <summary>
/// Result of initiating file retrieval from cold storage.
/// </summary>
public class InitiateRetrievalResult
{
    public bool Success { get; init; }
    public string? RetrievalId { get; init; }
    public DateTime? EstimatedCompletionTime { get; init; }
    public string? ErrorMessage { get; init; }

    public static InitiateRetrievalResult SuccessResult(
        string retrievalId,
        DateTime estimatedCompletionTime)
    {
        return new InitiateRetrievalResult
        {
            Success = true,
            RetrievalId = retrievalId,
            EstimatedCompletionTime = estimatedCompletionTime
        };
    }

    public static InitiateRetrievalResult FailureResult(string errorMessage)
    {
        return new InitiateRetrievalResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
