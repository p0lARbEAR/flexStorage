using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Domain.Entities;

/// <summary>
/// Represents an API key for authenticating users.
/// </summary>
public class ApiKey
{
    /// <summary>
    /// The unique identifier for this API key.
    /// </summary>
    public ApiKeyId Id { get; private set; }

    /// <summary>
    /// The user this API key belongs to.
    /// </summary>
    public UserId UserId { get; private set; }

    /// <summary>
    /// The actual API key value (hashed in database).
    /// </summary>
    public string KeyHash { get; private set; }

    /// <summary>
    /// When this API key was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// When this API key expires (null = never expires).
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>
    /// When this API key was last used.
    /// </summary>
    public DateTime? LastUsedAt { get; private set; }

    /// <summary>
    /// Whether this API key has been revoked.
    /// </summary>
    public bool IsRevoked { get; private set; }

    /// <summary>
    /// Optional description/name for this API key.
    /// </summary>
    public string? Description { get; private set; }

    // EF Core constructor
    private ApiKey() 
    {
        Id = null!;
        UserId = null!;
        KeyHash = null!;
    }

    private ApiKey(
        ApiKeyId id,
        UserId userId,
        string keyHash,
        DateTime createdAt,
        DateTime? expiresAt,
        string? description)
    {
        Id = id;
        UserId = userId;
        KeyHash = keyHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        Description = description;
        IsRevoked = false;
        LastUsedAt = null;
    }

    /// <summary>
    /// Creates a new API key.
    /// </summary>
    public static ApiKey Create(
        UserId userId,
        string keyHash,
        DateTime? expiresAt = null,
        string? description = null)
    {
        if (userId == null)
            throw new ArgumentNullException(nameof(userId));

        if (string.IsNullOrWhiteSpace(keyHash))
            throw new ArgumentException("Key hash cannot be empty", nameof(keyHash));

        return new ApiKey(
            ApiKeyId.New(),
            userId,
            keyHash,
            DateTime.UtcNow,
            expiresAt,
            description);
    }

    /// <summary>
    /// Checks if this API key is valid (not expired, not revoked).
    /// </summary>
    public bool IsValid()
    {
        if (IsRevoked)
            return false;

        if (ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow)
            return false;

        return true;
    }

    /// <summary>
    /// Revokes this API key.
    /// </summary>
    public void Revoke()
    {
        IsRevoked = true;
    }

    /// <summary>
    /// Updates the last used timestamp.
    /// </summary>
    public void UpdateLastUsed()
    {
        LastUsedAt = DateTime.UtcNow;
    }
}
