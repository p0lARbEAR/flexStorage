using FlexStorage.Application.Interfaces.Repositories;
using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace FlexStorage.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IUploadSessionRepository.
/// </summary>
public class UploadSessionRepository : IUploadSessionRepository
{
    private readonly FlexStorageDbContext _context;

    public UploadSessionRepository(FlexStorageDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(UploadSession session, CancellationToken cancellationToken = default)
    {
        await _context.UploadSessions.AddAsync(session, cancellationToken);
    }

    public async Task<UploadSession?> GetByIdAsync(
        UploadSessionId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.UploadSessions
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<UploadSession>> GetActiveSessionsAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.UploadSessions
            .Where(s => s.UserId == userId && s.CompletedAt == null && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(UploadSession session, CancellationToken cancellationToken = default)
    {
        _context.UploadSessions.Update(session);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(UploadSessionId id, CancellationToken cancellationToken = default)
    {
        var session = await GetByIdAsync(id, cancellationToken);
        if (session != null)
        {
            _context.UploadSessions.Remove(session);
        }
    }
}
