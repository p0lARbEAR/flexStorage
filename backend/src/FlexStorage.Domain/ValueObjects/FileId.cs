namespace FlexStorage.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for File entities.
/// </summary>
public sealed record FileId(Guid Value)
{
    /// <summary>
    /// Creates a new unique FileId.
    /// </summary>
    public static FileId New() => new(Guid.NewGuid());

    /// <summary>
    /// Creates a FileId from an existing GUID.
    /// </summary>
    public static FileId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(FileId fileId) => fileId.Value;
}
