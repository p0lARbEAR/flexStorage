using FlexStorage.Application.Interfaces.Repositories;
using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace FlexStorage.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for API keys using Entity Framework Core.
/// </summary>
public class ApiKeyRepository : IApiKeyRepository
{
    private readonly FlexStorageDbContext _context;

    public ApiKeyRepository(FlexStorageDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        return await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash, cancellationToken);
    }

    public async Task<IReadOnlyList<ApiKey>> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        return await _context.ApiKeys
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        await _context.ApiKeys.AddAsync(apiKey, cancellationToken);
    }

    public void Update(ApiKey apiKey)
    {
        _context.ApiKeys.Update(apiKey);
    }

    public void Delete(ApiKey apiKey)
    {
        _context.ApiKeys.Remove(apiKey);
    }
}
