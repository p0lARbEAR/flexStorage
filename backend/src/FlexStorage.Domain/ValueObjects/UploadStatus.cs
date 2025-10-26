namespace FlexStorage.Domain.ValueObjects;

/// <summary>
/// Value object representing the upload lifecycle status of a file.
/// Implements a state machine with valid state transitions.
/// </summary>
public sealed class UploadStatus : IEquatable<UploadStatus>
{
    /// <summary>
    /// Represents the current state.
    /// </summary>
    public enum State
    {
        /// <summary>
        /// File upload is pending/queued.
        /// </summary>
        Pending,

        /// <summary>
        /// File is currently being uploaded.
        /// </summary>
        Uploading,

        /// <summary>
        /// File upload has completed successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// File upload has failed.
        /// </summary>
        Failed,

        /// <summary>
        /// File has been archived to cold storage.
        /// </summary>
        Archived
    }

    /// <summary>
    /// Gets the current state.
    /// </summary>
    public State CurrentState { get; }

    /// <summary>
    /// Gets the timestamp when this status was set.
    /// </summary>
    public DateTime ChangedAt { get; }

    /// <summary>
    /// Gets whether the status is Pending.
    /// </summary>
    public bool IsPending => CurrentState == State.Pending;

    /// <summary>
    /// Gets whether the status is Uploading.
    /// </summary>
    public bool IsUploading => CurrentState == State.Uploading;

    /// <summary>
    /// Gets whether the status is Completed.
    /// </summary>
    public bool IsCompleted => CurrentState == State.Completed;

    /// <summary>
    /// Gets whether the status is Failed.
    /// </summary>
    public bool IsFailed => CurrentState == State.Failed;

    /// <summary>
    /// Gets whether the status is Archived.
    /// </summary>
    public bool IsArchived => CurrentState == State.Archived;

    // EF Core constructor
    private UploadStatus()
    {
    }

    private UploadStatus(State state, DateTime changedAt)
    {
        CurrentState = state;
        ChangedAt = changedAt;
    }

    /// <summary>
    /// Creates a Pending status.
    /// </summary>
    public static UploadStatus Pending() => new(State.Pending, DateTime.UtcNow);

    /// <summary>
    /// Creates an Uploading status.
    /// </summary>
    public static UploadStatus Uploading() => new(State.Uploading, DateTime.UtcNow);

    /// <summary>
    /// Creates a Completed status.
    /// </summary>
    public static UploadStatus Completed() => new(State.Completed, DateTime.UtcNow);

    /// <summary>
    /// Creates a Failed status.
    /// </summary>
    public static UploadStatus Failed() => new(State.Failed, DateTime.UtcNow);

    /// <summary>
    /// Creates an Archived status.
    /// </summary>
    public static UploadStatus Archived() => new(State.Archived, DateTime.UtcNow);

    /// <summary>
    /// Transitions to a new status if the transition is valid.
    /// </summary>
    /// <param name="newStatus">The new status to transition to</param>
    /// <returns>The new status</returns>
    /// <exception cref="InvalidOperationException">Thrown when the transition is invalid</exception>
    public UploadStatus TransitionTo(UploadStatus newStatus)
    {
        // Cannot change status after archived (terminal state)
        if (IsArchived)
            throw new InvalidOperationException("Cannot change status after archived");

        // Validate state transition
        if (!IsValidTransition(CurrentState, newStatus.CurrentState))
        {
            throw new InvalidOperationException(
                $"Invalid state transition from {CurrentState} to {newStatus.CurrentState}");
        }

        return new UploadStatus(newStatus.CurrentState, DateTime.UtcNow);
    }

    private static bool IsValidTransition(State from, State to)
    {
        // Define valid state transitions
        return (from, to) switch
        {
            // From Pending
            (State.Pending, State.Uploading) => true,
            (State.Pending, State.Failed) => true,

            // From Uploading
            (State.Uploading, State.Completed) => true,
            (State.Uploading, State.Failed) => true,

            // From Completed
            (State.Completed, State.Archived) => true,
            (State.Completed, State.Failed) => true, // Edge case: verification failed

            // From Failed (allow retry)
            (State.Failed, State.Pending) => true,

            // From Archived (terminal state - no transitions allowed)
            (State.Archived, _) => false,

            // Same state (no-op, but allowed)
            _ when from == to => true,

            // All other transitions are invalid
            _ => false
        };
    }

    #region Equality

    public bool Equals(UploadStatus? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return CurrentState == other.CurrentState;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is UploadStatus other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (int)CurrentState;
    }

    public static bool operator ==(UploadStatus? left, UploadStatus? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(UploadStatus? left, UploadStatus? right)
    {
        return !Equals(left, right);
    }

    #endregion

    public override string ToString() => $"{CurrentState} (as of {ChangedAt:u})";
}
