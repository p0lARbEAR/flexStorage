namespace FlexStorage.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for UploadSession entity.
/// </summary>
public sealed record UploadSessionId(Guid Value)
{
    /// <summary>
    /// Creates a new unique UploadSessionId.
    /// </summary>
    public static UploadSessionId New() => new(Guid.NewGuid());

    /// <summary>
    /// Creates an UploadSessionId from an existing Guid.
    /// </summary>
    public static UploadSessionId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
