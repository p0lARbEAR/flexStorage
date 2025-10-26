using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Application.Interfaces.Repositories;

/// <summary>
/// Repository interface for UploadSession entity.
/// </summary>
public interface IUploadSessionRepository
{
    /// <summary>
    /// Adds a new upload session.
    /// </summary>
    Task AddAsync(UploadSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an upload session by ID.
    /// </summary>
    Task<UploadSession?> GetByIdAsync(UploadSessionId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active sessions for a user.
    /// </summary>
    Task<IReadOnlyList<UploadSession>> GetActiveSessionsAsync(
        UserId userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing upload session.
    /// </summary>
    Task UpdateAsync(UploadSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an upload session.
    /// </summary>
    Task DeleteAsync(UploadSessionId id, CancellationToken cancellationToken = default);
}
