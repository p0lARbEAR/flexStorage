namespace FlexStorage.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for ApiKey entities.
/// </summary>
public sealed record ApiKeyId(Guid Value)
{
    /// <summary>
    /// Creates a new unique ApiKeyId.
    /// </summary>
    public static ApiKeyId New() => new(Guid.NewGuid());

    /// <summary>
    /// Creates an ApiKeyId from an existing GUID.
    /// </summary>
    public static ApiKeyId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(ApiKeyId apiKeyId) => apiKeyId.Value;
}
