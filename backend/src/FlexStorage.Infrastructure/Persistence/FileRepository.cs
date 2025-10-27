using FlexStorage.Application.Interfaces.Repositories;
using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using File = FlexStorage.Domain.Entities.File;

namespace FlexStorage.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IFileRepository.
/// </summary>
public class FileRepository : IFileRepository
{
    private readonly FlexStorageDbContext _context;

    public FileRepository(FlexStorageDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(File file, CancellationToken cancellationToken = default)
    {
        await _context.Files.AddAsync(file, cancellationToken);
    }

    public async Task<File?> GetByIdAsync(FileId id, CancellationToken cancellationToken = default)
    {
        return await _context.Files
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(File file, CancellationToken cancellationToken = default)
    {
        _context.Files.Update(file);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(FileId id, CancellationToken cancellationToken = default)
    {
        var file = await GetByIdAsync(id, cancellationToken);
        if (file != null)
        {
            _context.Files.Remove(file);
        }
    }

    public async Task<PagedResult<File>> GetByUserIdAsync(
        UserId userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Files
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.Metadata.CapturedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<File>(items, totalCount, page, pageSize);
    }

    public async Task<PagedResult<File>> SearchAsync(
        FileSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Files.AsQueryable();

        // Apply filters
        if (criteria.UserId != null)
        {
            query = query.Where(f => f.UserId == criteria.UserId);
        }

        if (!string.IsNullOrWhiteSpace(criteria.FileName))
        {
            query = query.Where(f => f.Metadata.OriginalFileName.Contains(criteria.FileName));
        }

        if (criteria.Category != null)
        {
            // Filter by MIME type category
            query = query.Where(f => f.Type.Category == criteria.Category.Value);
        }

        if (criteria.FromDate != null)
        {
            query = query.Where(f => f.Metadata.CapturedAt >= criteria.FromDate.Value);
        }

        if (criteria.ToDate != null)
        {
            query = query.Where(f => f.Metadata.CapturedAt <= criteria.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Status))
        {
            if (Enum.TryParse<UploadStatus.State>(criteria.Status, ignoreCase: true, out var statusEnum))
            {
                query = query.Where(f => f.Status.CurrentState == statusEnum);
            }
        }

        // Order by captured date (newest first)
        query = query.OrderByDescending(f => f.Metadata.CapturedAt);

        // Handle tags filtering with client-side evaluation (for InMemory compatibility)
        IEnumerable<File> filteredItems;
        int totalCount;

        if (criteria.Tags != null && criteria.Tags.Any())
        {
            // Load all matching records first (before tag filtering)
            var allItems = await query.ToListAsync(cancellationToken);

            // Normalize search tags for comparison
            var normalizedSearchTags = criteria.Tags
                .Select(t => t.Trim().ToLowerInvariant())
                .ToHashSet();

            // Filter files that have ANY of the specified tags (OR logic) - client-side
            filteredItems = allItems
                .Where(f => f.Metadata.Tags.Any(t => normalizedSearchTags.Contains(t)))
                .ToList();

            totalCount = filteredItems.Count();

            // Apply pagination
            filteredItems = filteredItems
                .Skip((criteria.Page - 1) * criteria.PageSize)
                .Take(criteria.PageSize);
        }
        else
        {
            // No tag filtering - execute query normally
            totalCount = await query.CountAsync(cancellationToken);

            filteredItems = await query
                .Skip((criteria.Page - 1) * criteria.PageSize)
                .Take(criteria.PageSize)
                .ToListAsync(cancellationToken);
        }

        return new PagedResult<File>(filteredItems.ToList(), totalCount, criteria.Page, criteria.PageSize);
    }

    public async Task<File?> GetByHashAsync(string hash, CancellationToken cancellationToken = default)
    {
        return await _context.Files
            .FirstOrDefaultAsync(f => f.Metadata.Hash == hash, cancellationToken);
    }
}
