using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;
using File = FlexStorage.Domain.Entities.File;

namespace FlexStorage.Application.Interfaces.Repositories;

/// <summary>
/// Repository interface for File aggregate.
/// </summary>
public interface IFileRepository
{
    /// <summary>
    /// Adds a new file to the repository.
    /// </summary>
    Task AddAsync(File file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a file by its ID.
    /// </summary>
    Task<File?> GetByIdAsync(FileId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing file.
    /// </summary>
    Task UpdateAsync(File file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file (soft delete).
    /// </summary>
    Task DeleteAsync(FileId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets files by user ID with pagination.
    /// </summary>
    Task<PagedResult<File>> GetByUserIdAsync(
        UserId userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches files with filters.
    /// </summary>
    Task<PagedResult<File>> SearchAsync(
        FileSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file with the given hash exists.
    /// </summary>
    Task<File?> GetByHashAsync(string hash, CancellationToken cancellationToken = default);
}

/// <summary>
/// Paged result wrapper.
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int TotalCount { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public PagedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }
}

/// <summary>
/// Search criteria for files.
/// </summary>
public class FileSearchCriteria
{
    public UserId? UserId { get; init; }
    public string? FileName { get; init; }
    public FileCategory? Category { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public List<string>? Tags { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
