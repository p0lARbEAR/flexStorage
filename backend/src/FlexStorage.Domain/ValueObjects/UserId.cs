namespace FlexStorage.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for User entities.
/// </summary>
public sealed record UserId(Guid Value)
{
    /// <summary>
    /// Creates a new unique UserId.
    /// </summary>
    public static UserId New() => new(Guid.NewGuid());

    /// <summary>
    /// Creates a UserId from an existing GUID.
    /// </summary>
    public static UserId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(UserId userId) => userId.Value;
}
