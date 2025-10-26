using FlexStorage.Application.Interfaces.Repositories;

namespace FlexStorage.Infrastructure.Persistence;

/// <summary>
/// Unit of Work implementation for managing transactions.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly FlexStorageDbContext _context;
    private readonly Lazy<IFileRepository> _fileRepository;
    private readonly Lazy<IUploadSessionRepository> _uploadSessionRepository;
    private readonly Lazy<IApiKeyRepository> _apiKeyRepository;

    public UnitOfWork(FlexStorageDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        _fileRepository = new Lazy<IFileRepository>(() => new FileRepository(_context));
        _uploadSessionRepository = new Lazy<IUploadSessionRepository>(() => new UploadSessionRepository(_context));
        _apiKeyRepository = new Lazy<IApiKeyRepository>(() => new ApiKeyRepository(_context));
    }

    public IFileRepository Files => _fileRepository.Value;

    public IUploadSessionRepository UploadSessions => _uploadSessionRepository.Value;

    public IApiKeyRepository ApiKeys => _apiKeyRepository.Value;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _context.Database.CommitTransactionAsync(cancellationToken);
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _context.Database.RollbackTransactionAsync(cancellationToken);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
