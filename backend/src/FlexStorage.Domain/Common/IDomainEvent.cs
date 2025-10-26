namespace FlexStorage.Domain.Common;

/// <summary>
/// Represents a domain event - something that happened in the domain that domain experts care about.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets when the event occurred.
    /// </summary>
    DateTime OccurredAt { get; }
}
