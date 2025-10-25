using FlexStorage.Domain.Common;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Domain.DomainEvents;

/// <summary>
/// Domain event raised when file upload starts.
/// </summary>
public sealed record FileUploadStartedDomainEvent(FileId FileId) : IDomainEvent
{
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
