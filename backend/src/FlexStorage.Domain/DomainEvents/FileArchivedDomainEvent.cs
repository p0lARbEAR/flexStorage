using FlexStorage.Domain.Common;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Domain.DomainEvents;

/// <summary>
/// Domain event raised when a file is archived to cold storage.
/// </summary>
public sealed record FileArchivedDomainEvent(FileId FileId, StorageLocation Location) : IDomainEvent
{
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
