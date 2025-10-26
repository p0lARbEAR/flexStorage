using FlexStorage.Domain.Common;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Domain.DomainEvents;

/// <summary>
/// Domain event raised when file upload completes successfully.
/// </summary>
public sealed record FileUploadCompletedDomainEvent(FileId FileId, StorageLocation Location) : IDomainEvent
{
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
