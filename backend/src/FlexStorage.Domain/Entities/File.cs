using FlexStorage.Domain.Common;
using FlexStorage.Domain.DomainEvents;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Domain.Entities;

/// <summary>
/// Aggregate root representing a file in the system.
/// Manages the complete lifecycle of a file from upload to archival.
/// </summary>
public class File : Entity<FileId>, IAggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// Gets the user who owns this file.
    /// </summary>
    public UserId UserId { get; private set; }

    /// <summary>
    /// Gets the file metadata.
    /// </summary>
    public FileMetadata Metadata { get; private set; }

    /// <summary>
    /// Gets the file size.
    /// </summary>
    public FileSize Size { get; private set; }

    /// <summary>
    /// Gets the file type.
    /// </summary>
    public FileType Type { get; private set; }

    /// <summary>
    /// Gets the current upload status.
    /// </summary>
    public UploadStatus Status { get; private set; }

    /// <summary>
    /// Gets the storage location (null if not yet uploaded).
    /// </summary>
    public StorageLocation? Location { get; private set; }

    /// <summary>
    /// Gets the thumbnail storage location (null if no thumbnail generated).
    /// Thumbnails are stored in S3 Standard for instant access.
    /// </summary>
    public StorageLocation? ThumbnailLocation { get; private set; }

    /// <summary>
    /// Gets the upload progress percentage (0-100).
    /// </summary>
    public int UploadProgress { get; private set; }

    /// <summary>
    /// Gets the domain events raised by this aggregate.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // EF Core constructor
    private File()
    {
        UserId = null!;
        Metadata = null!;
        Size = null!;
        Type = null!;
        Status = null!;
    }

    private File(
        FileId id,
        UserId userId,
        FileMetadata metadata,
        FileSize size,
        FileType type)
    {
        Id = id;
        UserId = userId;
        Metadata = metadata;
        Size = size;
        Type = type;
        Status = UploadStatus.Pending();
        UploadProgress = 0;
    }

    /// <summary>
    /// Creates a new file.
    /// </summary>
    /// <param name="userId">The user who owns the file</param>
    /// <param name="metadata">The file metadata</param>
    /// <param name="size">The file size</param>
    /// <param name="type">The file type</param>
    /// <returns>A new File instance</returns>
    public static File Create(
        UserId userId,
        FileMetadata metadata,
        FileSize size,
        FileType type)
    {
        var file = new File(FileId.New(), userId, metadata, size, type);
        file.RaiseDomainEvent(new FileCreatedDomainEvent(file.Id, userId));
        return file;
    }

    /// <summary>
    /// Starts the upload process.
    /// </summary>
    public void StartUpload()
    {
        EnsureNotArchived();

        Status = Status.TransitionTo(UploadStatus.Uploading());
        RaiseDomainEvent(new FileUploadStartedDomainEvent(Id));
    }

    /// <summary>
    /// Updates the upload progress.
    /// </summary>
    /// <param name="progress">Progress percentage (0-100)</param>
    public void UpdateProgress(int progress)
    {
        EnsureNotArchived();

        if (progress < 0 || progress > 100)
            throw new ArgumentException("Progress must be between 0 and 100", nameof(progress));

        UploadProgress = progress;
    }

    /// <summary>
    /// Completes the upload with the storage location.
    /// </summary>
    /// <param name="location">Where the file was stored</param>
    public void CompleteUpload(StorageLocation location)
    {
        EnsureNotArchived();

        Location = location ?? throw new ArgumentNullException(nameof(location));
        Status = Status.TransitionTo(UploadStatus.Completed());
        UploadProgress = 100;

        RaiseDomainEvent(new FileUploadCompletedDomainEvent(Id, location));
    }

    /// <summary>
    /// Sets the thumbnail storage location.
    /// </summary>
    /// <param name="thumbnailLocation">Where the thumbnail was stored</param>
    public void SetThumbnail(StorageLocation thumbnailLocation)
    {
        ThumbnailLocation = thumbnailLocation ?? throw new ArgumentNullException(nameof(thumbnailLocation));
    }

    /// <summary>
    /// Marks the file as archived to cold storage.
    /// </summary>
    public void MarkAsArchived()
    {
        if (Location == null)
            throw new InvalidOperationException("Cannot archive file without a storage location");

        Status = Status.TransitionTo(UploadStatus.Archived());
        RaiseDomainEvent(new FileArchivedDomainEvent(Id, Location));
    }

    /// <summary>
    /// Marks the file upload as failed.
    /// </summary>
    public void MarkAsFailed()
    {
        Status = Status.TransitionTo(UploadStatus.Failed());
    }

    /// <summary>
    /// Clears all domain events (typically called after publishing).
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    private void EnsureNotArchived()
    {
        if (Status.IsArchived)
            throw new InvalidOperationException("Cannot modify archived file");
    }

    private void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
}
