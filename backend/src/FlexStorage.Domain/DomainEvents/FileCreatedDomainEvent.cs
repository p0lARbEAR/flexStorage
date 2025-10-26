using FlexStorage.Domain.Common;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Domain.DomainEvents;

/// <summary>
/// Domain event raised when a new file is created.
/// </summary>
public sealed record FileCreatedDomainEvent(FileId FileId, UserId UserId) : IDomainEvent
{
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
