using FluentAssertions;
using FlexStorage.Domain.ValueObjects;
using Xunit;

namespace FlexStorage.Domain.Tests.ValueObjects;

public class UploadStatusTests
{
    [Fact]
    public void Should_InitializeWithPendingStatus()
    {
        // Act
        var status = UploadStatus.Pending();

        // Assert
        status.Should().NotBeNull();
        status.IsPending.Should().BeTrue();
        status.IsUploading.Should().BeFalse();
        status.IsCompleted.Should().BeFalse();
        status.IsFailed.Should().BeFalse();
        status.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void Should_TransitionFromPending_ToUploading()
    {
        // Arrange
        var status = UploadStatus.Pending();

        // Act
        var newStatus = status.TransitionTo(UploadStatus.Uploading());

        // Assert
        newStatus.IsUploading.Should().BeTrue();
        newStatus.IsPending.Should().BeFalse();
    }

    [Fact]
    public void Should_TransitionFromUploading_ToCompleted()
    {
        // Arrange
        var status = UploadStatus.Uploading();

        // Act
        var newStatus = status.TransitionTo(UploadStatus.Completed());

        // Assert
        newStatus.IsCompleted.Should().BeTrue();
        newStatus.IsUploading.Should().BeFalse();
    }

    [Fact]
    public void Should_TransitionFromUploading_ToFailed()
    {
        // Arrange
        var status = UploadStatus.Uploading();

        // Act
        var newStatus = status.TransitionTo(UploadStatus.Failed());

        // Assert
        newStatus.IsFailed.Should().BeTrue();
        newStatus.IsUploading.Should().BeFalse();
    }

    [Fact]
    public void Should_TransitionFromCompleted_ToArchived()
    {
        // Arrange
        var status = UploadStatus.Completed();

        // Act
        var newStatus = status.TransitionTo(UploadStatus.Archived());

        // Assert
        newStatus.IsArchived.Should().BeTrue();
        newStatus.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void Should_RejectInvalidTransition_PendingToArchived()
    {
        // Arrange
        var status = UploadStatus.Pending();

        // Act
        Action act = () => status.TransitionTo(UploadStatus.Archived());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid state transition*");
    }

    [Fact]
    public void Should_RejectInvalidTransition_CompletedToUploading()
    {
        // Arrange
        var status = UploadStatus.Completed();

        // Act
        Action act = () => status.TransitionTo(UploadStatus.Uploading());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid state transition*");
    }

    [Fact]
    public void Should_TrackTimestampOfStatusChange()
    {
        // Arrange
        var beforeTime = DateTime.UtcNow;
        var status = UploadStatus.Pending();

        // Act
        System.Threading.Thread.Sleep(10); // Ensure time difference
        var newStatus = status.TransitionTo(UploadStatus.Uploading());

        // Assert
        newStatus.ChangedAt.Should().BeAfter(beforeTime);
        newStatus.ChangedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Should_PreventChangesAfterArchived()
    {
        // Arrange
        var status = UploadStatus.Archived();

        // Act
        Action act = () => status.TransitionTo(UploadStatus.Uploading());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot change status after archived*");
    }

    [Fact]
    public void Should_AllowTransitionFromFailed_ToPending_ForRetry()
    {
        // Arrange
        var status = UploadStatus.Failed();

        // Act
        var newStatus = status.TransitionTo(UploadStatus.Pending());

        // Assert
        newStatus.IsPending.Should().BeTrue();
        newStatus.IsFailed.Should().BeFalse();
    }

    [Fact]
    public void Should_CompareStatuses_Equality()
    {
        // Arrange
        var status1 = UploadStatus.Pending();
        var status2 = UploadStatus.Pending();

        // Act & Assert
        status1.Should().Be(status2);
        (status1 == status2).Should().BeTrue();
    }

    [Fact]
    public void Should_CompareStatuses_Inequality()
    {
        // Arrange
        var status1 = UploadStatus.Pending();
        var status2 = UploadStatus.Uploading();

        // Act & Assert
        status1.Should().NotBe(status2);
        (status1 != status2).Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnCorrectStringRepresentation()
    {
        // Arrange
        var pendingStatus = UploadStatus.Pending();
        var uploadingStatus = UploadStatus.Uploading();
        var completedStatus = UploadStatus.Completed();
        var failedStatus = UploadStatus.Failed();
        var archivedStatus = UploadStatus.Archived();

        // Act & Assert
        pendingStatus.ToString().Should().Contain("Pending");
        uploadingStatus.ToString().Should().Contain("Uploading");
        completedStatus.ToString().Should().Contain("Completed");
        failedStatus.ToString().Should().Contain("Failed");
        archivedStatus.ToString().Should().Contain("Archived");
    }
}
