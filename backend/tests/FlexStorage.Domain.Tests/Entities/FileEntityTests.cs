using FluentAssertions;
using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;
using FlexStorage.Domain.DomainEvents;
using Xunit;

namespace FlexStorage.Domain.Tests.Entities;

public class FileEntityTests
{
    private readonly UserId _userId = UserId.New();
    private readonly FileMetadata _metadata;
    private readonly FileSize _fileSize;
    private readonly FileType _fileType;

    public FileEntityTests()
    {
        _metadata = FileMetadata.Create("test.jpg", "sha256:abc123", DateTime.UtcNow);
        _fileSize = FileSize.FromBytes(1048576); // 1 MB
        _fileType = FileType.FromMimeType("image/jpeg");
    }

    [Fact]
    public void Should_CreateFile_WithRequiredProperties()
    {
        // Act
        var file = File.Create(_userId, _metadata, _fileSize, _fileType);

        // Assert
        file.Should().NotBeNull();
        file.Id.Should().NotBe(FileId.From(Guid.Empty));
        file.UserId.Should().Be(_userId);
        file.Metadata.Should().Be(_metadata);
        file.Size.Should().Be(_fileSize);
        file.Type.Should().Be(_fileType);
    }

    [Fact]
    public void Should_GenerateUniqueFileId_OnCreation()
    {
        // Act
        var file1 = File.Create(_userId, _metadata, _fileSize, _fileType);
        var file2 = File.Create(_userId, _metadata, _fileSize, _fileType);

        // Assert
        file1.Id.Should().NotBe(file2.Id);
    }

    [Fact]
    public void Should_InitializeWithPendingStatus()
    {
        // Act
        var file = File.Create(_userId, _metadata, _fileSize, _fileType);

        // Assert
        file.Status.IsPending.Should().BeTrue();
    }

    [Fact]
    public void Should_HaveZeroUploadProgress_Initially()
    {
        // Act
        var file = File.Create(_userId, _metadata, _fileSize, _fileType);

        // Assert
        file.UploadProgress.Should().Be(0);
    }

    [Fact]
    public void Should_RaiseFileCreatedEvent_OnCreation()
    {
        // Act
        var file = File.Create(_userId, _metadata, _fileSize, _fileType);

        // Assert
        file.DomainEvents.Should().ContainSingle();
        file.DomainEvents.Should().ContainItemsAssignableTo<FileCreatedDomainEvent>();

        var createdEvent = file.DomainEvents.OfType<FileCreatedDomainEvent>().First();
        createdEvent.FileId.Should().Be(file.Id);
        createdEvent.UserId.Should().Be(_userId);
    }

    [Fact]
    public void Should_StartUpload_AndUpdateStatus()
    {
        // Arrange
        var file = File.Create(_userId, _metadata, _fileSize, _fileType);

        // Act
        file.StartUpload();

        // Assert
        file.Status.IsUploading.Should().BeTrue();
    }

    [Fact]
    public void Should_RaiseFileUploadStartedEvent_WhenUploadStarts()
    {
        // Arrange
        var file = File.Create(_userId, _metadata, _fileSize, _fileType);

        // Act
        file.StartUpload();

        // Assert
        file.DomainEvents.Should().Contain(e => e is FileUploadStartedDomainEvent);
    }

    [Fact]
    public void Should_UpdateUploadProgress()
    {
        // Arrange
        var file = File.Create(_userId, _metadata, _fileSize, _fileType);
        file.StartUpload();

        // Act
        file.UpdateProgress(50);

        // Assert
        file.UploadProgress.Should().Be(50);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(150)]
    public void Should_RejectInvalidUploadProgress(int invalidProgress)
    {
        // Arrange
        var file = File.Create(_userId, _metadata, _fileSize, _fileType);

        // Act
        Action act = () => file.UpdateProgress(invalidProgress);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*between 0 and 100*");
    }

    [Fact]
    public void Should_CompleteUpload_WithStorageLocation()
    {
        // Arrange
        var file = File.Create(_userId, _metadata, _fileSize, _fileType);
        file.StartUpload();
        var location = StorageLocation.Create("S3 Glacier", "s3://bucket/file.jpg");

        // Act
        file.CompleteUpload(location);

        // Assert
        file.Status.IsCompleted.Should().BeTrue();
        file.Location.Should().Be(location);
        file.UploadProgress.Should().Be(100);
    }

    [Fact]
    public void Should_RaiseFileUploadCompletedEvent_WhenUploadCompletes()
    {
        // Arrange
        var file = File.Create(_userId, _metadata, _fileSize, _fileType);
        file.StartUpload();
        var location = StorageLocation.Create("S3 Glacier", "s3://bucket/file.jpg");

        // Act
        file.CompleteUpload(location);

        // Assert
        file.DomainEvents.Should().Contain(e => e is FileUploadCompletedDomainEvent);
        var completedEvent = file.DomainEvents.OfType<FileUploadCompletedDomainEvent>().First();
        completedEvent.Location.Should().Be(location);
    }

    [Fact]
    public void Should_MarkAsArchived()
    {
        // Arrange
        var file = File.Create(_userId, _metadata, _fileSize, _fileType);
        file.StartUpload();
        var location = StorageLocation.Create("S3 Glacier", "s3://bucket/file.jpg");
        file.CompleteUpload(location);

        // Act
        file.MarkAsArchived();

        // Assert
        file.Status.IsArchived.Should().BeTrue();
    }

    [Fact]
    public void Should_RaiseFileArchivedEvent_WhenMarkedAsArchived()
    {
        // Arrange
        var file = File.Create(_userId, _metadata, _fileSize, _fileType);
        file.StartUpload();
        var location = StorageLocation.Create("S3 Glacier", "s3://bucket/file.jpg");
        file.CompleteUpload(location);

        // Act
        file.MarkAsArchived();

        // Assert
        file.DomainEvents.Should().Contain(e => e is FileArchivedDomainEvent);
    }

    [Fact]
    public void Should_PreventModificationAfterArchived()
    {
        // Arrange
        var file = File.Create(_userId, _metadata, _fileSize, _fileType);
        file.StartUpload();
        var location = StorageLocation.Create("S3 Glacier", "s3://bucket/file.jpg");
        file.CompleteUpload(location);
        file.MarkAsArchived();

        // Act
        Action act = () => file.StartUpload();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*archived*");
    }

    [Fact]
    public void Should_PreventStatusUpdateAfterArchived()
    {
        // Arrange
        var file = File.Create(_userId, _metadata, _fileSize, _fileType);
        file.StartUpload();
        var location = StorageLocation.Create("S3 Glacier", "s3://bucket/file.jpg");
        file.CompleteUpload(location);
        file.MarkAsArchived();

        // Act
        Action act = () => file.UpdateProgress(50);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*archived*");
    }

    [Fact]
    public void Should_HandleUploadFailure()
    {
        // Arrange
        var file = File.Create(_userId, _metadata, _fileSize, _fileType);
        file.StartUpload();

        // Act
        file.MarkAsFailed();

        // Assert
        file.Status.IsFailed.Should().BeTrue();
    }

    [Fact]
    public void Should_ClearDomainEvents()
    {
        // Arrange
        var file = File.Create(_userId, _metadata, _fileSize, _fileType);
        file.StartUpload();

        // Act
        var events = file.DomainEvents.ToList();
        file.ClearDomainEvents();

        // Assert
        file.DomainEvents.Should().BeEmpty();
        events.Should().NotBeEmpty(); // Original list should still have events
    }
}
