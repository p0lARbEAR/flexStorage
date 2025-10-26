using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Application.Interfaces.Repositories;

/// <summary>
/// Repository interface for API keys.
/// </summary>
public interface IApiKeyRepository
{
    /// <summary>
    /// Gets an API key by its hashed value.
    /// </summary>
    Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all API keys for a user.
    /// </summary>
    Task<IReadOnlyList<ApiKey>> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new API key.
    /// </summary>
    Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing API key.
    /// </summary>
    void Update(ApiKey apiKey);

    /// <summary>
    /// Deletes an API key.
    /// </summary>
    void Delete(ApiKey apiKey);
}
