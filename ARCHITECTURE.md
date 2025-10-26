# FlexStorage Backend Architecture

**Version:** 1.0.0
**Architecture Pattern:** Domain-Driven Design (DDD)
**Technology:** .NET 8, C#, ASP.NET Core
**Testing:** xUnit, FluentAssertions

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [DDD Layers](#ddd-layers)
3. [Project Structure](#project-structure)
4. [Design Patterns](#design-patterns)
5. [Dependency Flow](#dependency-flow)
6. [Data Flow](#data-flow)
7. [Technology Stack](#technology-stack)
8. [Deployment Architecture](#deployment-architecture)

---

## Architecture Overview

FlexStorage follows **Domain-Driven Design (DDD)** principles with **Clean Architecture** to ensure:
- **Separation of Concerns**: Each layer has a single responsibility
- **Testability**: Domain logic is independent and easily testable
- **Maintainability**: Changes in one layer don't cascade to others
- **Flexibility**: Easy to swap infrastructure components (databases, storage providers)

### Core Principles

1. **Domain-Centric**: Business logic is the heart of the application
2. **Dependency Inversion**: High-level modules don't depend on low-level modules
3. **Interface Segregation**: Small, focused interfaces
4. **Test-Driven Development**: Tests drive the design
5. **Plugin Architecture**: Easy to extend with new storage providers

---

## DDD Layers

```
┌─────────────────────────────────────────────────┐
│              API Layer (Controllers)            │
│  HTTP Endpoints, Request/Response, Validation   │
└────────────────┬────────────────────────────────┘
                 │ depends on
                 ▼
┌─────────────────────────────────────────────────┐
│         Application Layer (Use Cases)           │
│  Application Services, DTOs, Interfaces         │
└────────────────┬────────────────────────────────┘
                 │ depends on
                 ▼
┌─────────────────────────────────────────────────┐
│         Domain Layer (Business Logic)           │
│  Entities, Value Objects, Domain Services       │
│  ★ NO DEPENDENCIES ON OTHER LAYERS ★           │
└─────────────────────────────────────────────────┘
                 ▲
                 │ implements
                 │
┌─────────────────────────────────────────────────┐
│     Infrastructure Layer (Implementation)       │
│  Repositories, Storage Providers, External APIs │
└─────────────────────────────────────────────────┘
```

---

### 1. Domain Layer

**Purpose:** Core business logic and rules
**Dependencies:** NONE (pure C# - no external libraries except primitives)

**Components:**

#### Value Objects
- `FileSize` - Represents file size with validation and conversion
- `FileType` - Represents file type/MIME type with categorization
- `StorageLocation` - Represents where a file is stored
- `UploadStatus` - Represents upload lifecycle states
- `FileHash` - Represents SHA256 hash with validation

**Characteristics:**
- Immutable
- Self-validating
- No identity (equality by value)
- Rich behavior (not anemic)

```csharp
// Example: FileSize Value Object
public sealed class FileSize : IEquatable<FileSize>, IComparable<FileSize>
{
    private const long MaxSize = 5L * 1024 * 1024 * 1024; // 5GB

    public long Bytes { get; }

    private FileSize(long bytes)
    {
        if (bytes <= 0)
            throw new ArgumentException("File size must be greater than zero", nameof(bytes));
        if (bytes > MaxSize)
            throw new ArgumentException($"File size cannot exceed {MaxSize} bytes", nameof(bytes));

        Bytes = bytes;
    }

    public static FileSize FromBytes(long bytes) => new(bytes);

    public double ToMegabytes() => Bytes / (1024.0 * 1024.0);
    public double ToGigabytes() => Bytes / (1024.0 * 1024.0 * 1024.0);

    public string ToHumanReadable() => Bytes switch
    {
        < 1024 => $"{Bytes} B",
        < 1024 * 1024 => $"{Bytes / 1024.0:F2} KB",
        < 1024 * 1024 * 1024 => $"{Bytes / (1024.0 * 1024.0):F2} MB",
        _ => $"{Bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
    };

    // Equality, comparison, operators...
}
```

#### Entities
- `File` (Aggregate Root) - The core file entity with upload lifecycle
- `FileMetadata` - File metadata (name, hash, timestamps, tags)
- `UploadSession` - Tracks chunked upload progress
- `RetrievalRequest` - Tracks Glacier retrieval requests

**Characteristics:**
- Has unique identity (ID)
- Mutable through domain methods only
- Enforces invariants
- Raises domain events

```csharp
// Example: File Entity (Aggregate Root)
public sealed class File : Entity, IAggregateRoot
{
    public FileId Id { get; private set; }
    public UserId UserId { get; private set; }
    public FileMetadata Metadata { get; private set; }
    public FileSize Size { get; private set; }
    public FileType Type { get; private set; }
    public UploadStatus Status { get; private set; }
    public StorageLocation? Location { get; private set; }
    public int UploadProgress { get; private set; } // 0-100

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private File() { } // EF Core

    public static File Create(UserId userId, FileMetadata metadata, FileSize size, FileType type)
    {
        var file = new File
        {
            Id = FileId.New(),
            UserId = userId,
            Metadata = metadata,
            Size = size,
            Type = type,
            Status = UploadStatus.Pending(),
            UploadProgress = 0
        };

        file.RaiseDomainEvent(new FileCreatedDomainEvent(file.Id, userId));
        return file;
    }

    public void StartUpload()
    {
        EnsureNotArchived();
        Status = Status.TransitionTo(UploadStatus.Uploading());
        RaiseDomainEvent(new FileUploadStartedDomainEvent(Id));
    }

    public void UpdateProgress(int progress)
    {
        if (progress < 0 || progress > 100)
            throw new ArgumentException("Progress must be between 0 and 100");

        UploadProgress = progress;
    }

    public void CompleteUpload(StorageLocation location)
    {
        EnsureNotArchived();
        Location = location;
        Status = Status.TransitionTo(UploadStatus.Completed());
        UploadProgress = 100;
        RaiseDomainEvent(new FileUploadCompletedDomainEvent(Id, location));
    }

    public void MarkAsArchived()
    {
        Status = Status.TransitionTo(UploadStatus.Archived());
        RaiseDomainEvent(new FileArchivedDomainEvent(Id, Location!));
    }

    private void EnsureNotArchived()
    {
        if (Status.IsArchived)
            throw new InvalidOperationException("Cannot modify archived file");
    }

    private void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
}
```

#### Domain Services
- `IStorageProvider` (interface) - Contract for storage providers
- `StorageProviderSelector` - Selects appropriate provider based on file characteristics
- `FileHashCalculator` - Calculates SHA256 hash of file streams

**Characteristics:**
- Stateless
- Operates on multiple entities/value objects
- Contains logic that doesn't belong to a single entity

```csharp
// Example: Domain Service
public interface IStorageProvider
{
    string ProviderName { get; }
    ProviderCapabilities Capabilities { get; }

    Task<UploadResult> UploadAsync(Stream fileStream, UploadOptions options, CancellationToken cancellationToken);
    Task<Stream> DownloadAsync(StorageLocation location, CancellationToken cancellationToken);
    Task<RetrievalResult> InitiateRetrievalAsync(StorageLocation location, RetrievalTier tier, CancellationToken cancellationToken);
    Task<RetrievalStatus> GetRetrievalStatusAsync(string retrievalId, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(StorageLocation location, CancellationToken cancellationToken);
    Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken);
}

public class StorageProviderSelector
{
    public IStorageProvider SelectProvider(FileType fileType, FileSize fileSize, string? userPreference = null)
    {
        // Domain logic for selecting optimal provider
        // Based on file characteristics and user preferences
    }
}
```

#### Domain Events
- `FileCreatedDomainEvent`
- `FileUploadStartedDomainEvent`
- `FileUploadCompletedDomainEvent`
- `FileArchivedDomainEvent`
- `FileDeletedDomainEvent`

---

### 2. Application Layer

**Purpose:** Orchestrates use cases, coordinates domain objects
**Dependencies:** Domain Layer only

**Components:**

#### Application Services (Use Cases)
- `FileUploadService` - Handles file upload orchestration
- `ChunkedUploadService` - Manages chunked uploads
- `FileRetrievalService` - Handles file retrieval and downloads
- `ThumbnailGenerationService` - Generates and caches thumbnails
- `HashComparisonService` - Batch hash comparison for deduplication
- `FileSearchService` - Search and filter files
- `QuotaManagementService` - Track and enforce quotas
- `RateLimitingService` - Enforce rate limits
- `RedundancyManagementService` - Manage file redundancy
- `FileRebalancingService` - Rebalance files between providers
- `FileSharingService` - Manage public file sharing

```csharp
// Example: Application Service
public class FileUploadService
{
    private readonly IFileRepository _fileRepository;
    private readonly IStorageProviderFactory _storageProviderFactory;
    private readonly StorageProviderSelector _providerSelector;
    private readonly FileHashCalculator _hashCalculator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FileUploadService> _logger;

    public FileUploadService(
        IFileRepository fileRepository,
        IStorageProviderFactory storageProviderFactory,
        StorageProviderSelector providerSelector,
        FileHashCalculator hashCalculator,
        IUnitOfWork unitOfWork,
        ILogger<FileUploadService> logger)
    {
        _fileRepository = fileRepository;
        _storageProviderFactory = storageProviderFactory;
        _providerSelector = providerSelector;
        _hashCalculator = hashCalculator;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<UploadFileResult> UploadFileAsync(
        UploadFileCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Validate
        var fileSize = FileSize.FromBytes(command.FileSize);
        var fileType = FileType.FromMimeType(command.MimeType);

        // 2. Calculate hash
        var hash = await _hashCalculator.CalculateHashAsync(command.FileStream, cancellationToken);

        // 3. Check for duplicate
        var existingFile = await _fileRepository.GetByHashAsync(hash, cancellationToken);
        if (existingFile != null)
        {
            _logger.LogInformation("Duplicate file detected: {Hash}", hash);
            return UploadFileResult.Duplicate(existingFile.Id);
        }

        // 4. Create file entity
        var metadata = FileMetadata.Create(command.FileName, hash, command.CapturedAt);
        var file = File.Create(command.UserId, metadata, fileSize, fileType);

        // 5. Select storage provider
        var provider = _providerSelector.SelectProvider(fileType, fileSize, command.ProviderPreference);

        // 6. Upload to storage
        file.StartUpload();
        await _fileRepository.AddAsync(file, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var uploadResult = await provider.UploadAsync(
                command.FileStream,
                new UploadOptions
                {
                    FileName = metadata.SanitizedFileName,
                    ContentType = fileType.MimeType
                },
                cancellationToken);

            file.CompleteUpload(uploadResult.Location);
            file.MarkAsArchived();

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 7. Queue thumbnail generation (async)
            await QueueThumbnailGenerationAsync(file.Id, cancellationToken);

            return UploadFileResult.Success(file.Id, uploadResult.Location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed for file {FileId}", file.Id);
            file.MarkAsFailed();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}
```

#### DTOs (Data Transfer Objects)
- Request DTOs: `UploadFileRequest`, `InitiateChunkedUploadRequest`, etc.
- Response DTOs: `FileDto`, `UploadFileResponse`, `FileListResponse`, etc.
- Command/Query objects for CQRS pattern (optional)

#### Repository Interfaces
- `IFileRepository`
- `IUploadSessionRepository`
- `IRetrievalRequestRepository`
- `IShareRepository`
- `IUnitOfWork`

---

### 3. Infrastructure Layer

**Purpose:** Implements interfaces, handles external dependencies
**Dependencies:** Domain Layer, Application Layer (interfaces only)

**Components:**

#### Persistence
- **Entity Framework Core**
  - `FlexStorageDbContext`
  - Entity configurations
  - Migrations
  - Repository implementations

```csharp
// Example: Repository Implementation
public class FileRepository : IFileRepository
{
    private readonly FlexStorageDbContext _context;

    public FileRepository(FlexStorageDbContext context)
    {
        _context = context;
    }

    public async Task<File?> GetByIdAsync(FileId id, CancellationToken cancellationToken)
    {
        return await _context.Files
            .Include(f => f.Metadata)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task<File?> GetByHashAsync(FileHash hash, CancellationToken cancellationToken)
    {
        return await _context.Files
            .FirstOrDefaultAsync(f => f.Metadata.Hash == hash, cancellationToken);
    }

    public async Task AddAsync(File file, CancellationToken cancellationToken)
    {
        await _context.Files.AddAsync(file, cancellationToken);
    }

    public async Task<PagedResult<File>> GetByUserIdAsync(
        UserId userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _context.Files.Where(f => f.UserId == userId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(f => f.Metadata.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<File>(items, total, page, pageSize);
    }
}
```

#### Storage Providers
- `S3GlacierDeepArchiveProvider`
- `S3GlacierFlexibleRetrievalProvider`
- `BackblazeB2Provider`
- `LocalFileSystemProvider` (for testing)
- `StorageProviderFactory`

```csharp
// Example: Storage Provider Implementation
public class S3GlacierDeepArchiveProvider : IStorageProvider
{
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonGlacier _glacierClient;
    private readonly S3ProviderOptions _options;
    private readonly ILogger<S3GlacierDeepArchiveProvider> _logger;

    public string ProviderName => "S3 Glacier Deep Archive";

    public ProviderCapabilities Capabilities => new()
    {
        SupportsInstantAccess = false,
        SupportsRetrieval = true,
        SupportsDeletion = true,
        MinRetrievalTime = TimeSpan.FromHours(12),
        MaxRetrievalTime = TimeSpan.FromHours(48)
    };

    public async Task<UploadResult> UploadAsync(
        Stream fileStream,
        UploadOptions options,
        CancellationToken cancellationToken)
    {
        var key = GenerateS3Key(options.FileName);

        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = fileStream,
            StorageClass = S3StorageClass.DeepArchive,
            ContentType = options.ContentType
        };

        await _s3Client.PutObjectAsync(request, cancellationToken);

        var location = StorageLocation.Create(ProviderName, $"s3://{_options.BucketName}/{key}");

        return new UploadResult
        {
            Location = location,
            UploadedAt = DateTime.UtcNow
        };
    }

    public async Task<RetrievalResult> InitiateRetrievalAsync(
        StorageLocation location,
        RetrievalTier tier,
        CancellationToken cancellationToken)
    {
        // Initiate Glacier restore
        var s3Key = ExtractS3Key(location);

        var request = new RestoreObjectRequest
        {
            BucketName = _options.BucketName,
            Key = s3Key,
            Days = 1, // Keep restored copy for 1 day
            RetrievalTier = tier switch
            {
                RetrievalTier.Bulk => GlacierJobTier.Bulk,
                RetrievalTier.Standard => GlacierJobTier.Standard,
                RetrievalTier.Expedited => GlacierJobTier.Expedited,
                _ => throw new ArgumentException($"Unknown tier: {tier}")
            }
        };

        await _s3Client.RestoreObjectAsync(request, cancellationToken);

        return new RetrievalResult
        {
            RetrievalId = Guid.NewGuid().ToString(),
            EstimatedCompletionTime = tier switch
            {
                RetrievalTier.Bulk => TimeSpan.FromHours(12),
                RetrievalTier.Standard => TimeSpan.FromHours(5),
                RetrievalTier.Expedited => TimeSpan.FromMinutes(5),
                _ => TimeSpan.FromHours(12)
            }
        };
    }

    // Other methods...
}
```

#### Background Jobs
- `ThumbnailGenerationJob` - Generates thumbnails asynchronously
- `ExpiredSessionCleanupJob` - Cleans up expired upload sessions
- `RedundancyVerificationJob` - Verifies file redundancy periodically

#### Plugin Loader
- `PluginLoader` - Discovers and loads storage provider plugins

---

### 4. API Layer

**Purpose:** HTTP endpoints, request/response handling
**Dependencies:** Application Layer

**Components:**

#### Controllers
- `AuthController` - Authentication endpoints
- `FilesController` - File upload/download/management
- `ProvidersController` - Storage provider management
- `QuotaController` - Quota information
- `RedundancyController` - Redundancy management
- `ShareController` - File sharing
- `HealthController` - Health checks

```csharp
// Example: API Controller
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly FileUploadService _uploadService;
    private readonly FileRetrievalService _retrievalService;
    private readonly IMapper _mapper;

    [HttpPost]
    [RequestSizeLimit(10_485_760)] // 10MB for simple upload
    public async Task<ActionResult<UploadFileResponse>> UploadFile(
        [FromForm] UploadFileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var command = new UploadFileCommand
        {
            UserId = userId,
            FileName = request.File.FileName,
            FileSize = request.File.Length,
            MimeType = request.File.ContentType,
            FileStream = request.File.OpenReadStream(),
            CapturedAt = request.Metadata?.CapturedAt,
            Tags = request.Metadata?.Tags
        };

        var result = await _uploadService.UploadFileAsync(command, cancellationToken);

        if (result.IsDuplicate)
        {
            return Ok(new UploadFileResponse
            {
                FileId = result.FileId.Value,
                IsDuplicate = true,
                Message = "File already exists"
            });
        }

        return CreatedAtAction(
            nameof(GetFile),
            new { id = result.FileId.Value },
            new UploadFileResponse
            {
                FileId = result.FileId.Value,
                Status = "completed",
                StorageLocation = result.Location.ToString()
            });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FileDto>> GetFile(
        Guid id,
        CancellationToken cancellationToken)
    {
        var file = await _retrievalService.GetFileMetadataAsync(
            FileId.From(id),
            cancellationToken);

        if (file == null)
            return NotFound();

        return Ok(_mapper.Map<FileDto>(file));
    }
}
```

#### Middleware
- `ExceptionHandlingMiddleware` - Global exception handling
- `RateLimitingMiddleware` - Rate limit enforcement
- `RequestLoggingMiddleware` - Request/response logging

#### Filters & Attributes
- `ValidateModelStateAttribute` - Model validation
- `RateLimitAttribute` - Rate limiting per endpoint
- `RequireQuotaAttribute` - Quota enforcement

---

## Project Structure

```
FlexStorage/
├── src/
│   ├── FlexStorage.Domain/                     # Domain Layer
│   │   ├── Entities/
│   │   │   ├── File.cs
│   │   │   ├── FileMetadata.cs
│   │   │   ├── UploadSession.cs
│   │   │   └── RetrievalRequest.cs
│   │   ├── ValueObjects/
│   │   │   ├── FileSize.cs
│   │   │   ├── FileType.cs
│   │   │   ├── StorageLocation.cs
│   │   │   ├── UploadStatus.cs
│   │   │   └── FileHash.cs
│   │   ├── DomainServices/
│   │   │   ├── IStorageProvider.cs
│   │   │   ├── StorageProviderSelector.cs
│   │   │   └── FileHashCalculator.cs
│   │   ├── DomainEvents/
│   │   │   ├── FileCreatedDomainEvent.cs
│   │   │   ├── FileUploadStartedDomainEvent.cs
│   │   │   ├── FileUploadCompletedDomainEvent.cs
│   │   │   └── FileArchivedDomainEvent.cs
│   │   ├── Common/
│   │   │   ├── Entity.cs
│   │   │   ├── ValueObject.cs
│   │   │   ├── IAggregateRoot.cs
│   │   │   └── IDomainEvent.cs
│   │   └── FlexStorage.Domain.csproj
│   │
│   ├── FlexStorage.Application/                # Application Layer
│   │   ├── Services/
│   │   │   ├── FileUploadService.cs
│   │   │   ├── ChunkedUploadService.cs
│   │   │   ├── FileRetrievalService.cs
│   │   │   ├── ThumbnailGenerationService.cs
│   │   │   ├── HashComparisonService.cs
│   │   │   ├── QuotaManagementService.cs
│   │   │   ├── RateLimitingService.cs
│   │   │   └── ...
│   │   ├── Interfaces/
│   │   │   ├── Repositories/
│   │   │   │   ├── IFileRepository.cs
│   │   │   │   ├── IUploadSessionRepository.cs
│   │   │   │   └── IUnitOfWork.cs
│   │   │   └── IStorageProviderFactory.cs
│   │   ├── DTOs/
│   │   │   ├── Requests/
│   │   │   │   ├── UploadFileRequest.cs
│   │   │   │   └── InitiateChunkedUploadRequest.cs
│   │   │   ├── Responses/
│   │   │   │   ├── FileDto.cs
│   │   │   │   └── UploadFileResponse.cs
│   │   │   └── Commands/
│   │   │       └── UploadFileCommand.cs
│   │   ├── Mappings/
│   │   │   └── AutoMapperProfile.cs
│   │   └── FlexStorage.Application.csproj
│   │
│   ├── FlexStorage.Infrastructure/             # Infrastructure Layer
│   │   ├── Persistence/
│   │   │   ├── FlexStorageDbContext.cs
│   │   │   ├── Repositories/
│   │   │   │   ├── FileRepository.cs
│   │   │   │   └── UploadSessionRepository.cs
│   │   │   ├── Configurations/
│   │   │   │   ├── FileConfiguration.cs
│   │   │   │   └── FileMetadataConfiguration.cs
│   │   │   └── Migrations/
│   │   ├── StorageProviders/
│   │   │   ├── S3GlacierDeepArchiveProvider.cs
│   │   │   ├── S3GlacierFlexibleRetrievalProvider.cs
│   │   │   ├── BackblazeB2Provider.cs
│   │   │   ├── LocalFileSystemProvider.cs
│   │   │   ├── StorageProviderFactory.cs
│   │   │   └── PluginLoader.cs
│   │   ├── BackgroundJobs/
│   │   │   ├── ThumbnailGenerationJob.cs
│   │   │   ├── ExpiredSessionCleanupJob.cs
│   │   │   └── RedundancyVerificationJob.cs
│   │   ├── ExternalServices/
│   │   │   ├── OAuth2Service.cs
│   │   │   └── WebhookNotificationService.cs
│   │   └── FlexStorage.Infrastructure.csproj
│   │
│   ├── FlexStorage.API/                         # API Layer
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs
│   │   │   ├── FilesController.cs
│   │   │   ├── ProvidersController.cs
│   │   │   ├── QuotaController.cs
│   │   │   └── HealthController.cs
│   │   ├── Middleware/
│   │   │   ├── ExceptionHandlingMiddleware.cs
│   │   │   ├── RateLimitingMiddleware.cs
│   │   │   └── RequestLoggingMiddleware.cs
│   │   ├── Filters/
│   │   │   └── ValidateModelStateAttribute.cs
│   │   ├── Extensions/
│   │   │   └── ServiceCollectionExtensions.cs
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── FlexStorage.API.csproj
│   │
│   └── FlexStorage.Shared/                      # Shared Utilities
│       ├── Exceptions/
│       ├── Extensions/
│       └── FlexStorage.Shared.csproj
│
├── tests/
│   ├── FlexStorage.Domain.Tests/
│   │   ├── ValueObjects/
│   │   │   ├── FileSizeTests.cs
│   │   │   ├── FileTypeTests.cs
│   │   │   └── ...
│   │   ├── Entities/
│   │   │   ├── FileEntityTests.cs
│   │   │   └── ...
│   │   └── DomainServices/
│   │
│   ├── FlexStorage.Application.Tests/
│   │   ├── Services/
│   │   │   ├── FileUploadServiceTests.cs
│   │   │   └── ...
│   │
│   ├── FlexStorage.Infrastructure.Tests/
│   │   ├── Repositories/
│   │   ├── StorageProviders/
│   │   └── ...
│   │
│   ├── FlexStorage.API.Tests/
│   │   ├── Controllers/
│   │   └── ...
│   │
│   └── FlexStorage.IntegrationTests/
│       ├── UploadFlowTests.cs
│       ├── RetrievalFlowTests.cs
│       └── ...
│
├── plugins/                                     # Storage Provider Plugins
│   └── FlexStorage.Provider.CustomProvider/
│
├── docs/
│   ├── BACKEND_SPEC.md
│   ├── TEST_PLAN.md
│   ├── APP_SPEC.md
│   └── ARCHITECTURE.md
│
├── .github/
│   └── workflows/
│       ├── ci.yml
│       └── cd.yml
│
├── FlexStorage.sln
├── README.md
├── .gitignore
└── LICENSE
```

---

## Design Patterns

### 1. Domain-Driven Design (DDD)
- Aggregate Roots (File)
- Value Objects (FileSize, FileType)
- Domain Events
- Repository Pattern

### 2. CQRS (Command Query Responsibility Segregation)
- Commands for writes (UploadFileCommand)
- Queries for reads (GetFileQuery)
- Separate read/write models (optional optimization)

### 3. Unit of Work
- Transactional consistency
- Batch save changes

### 4. Factory Pattern
- StorageProviderFactory
- Plugin instantiation

### 5. Strategy Pattern
- Different storage providers implement IStorageProvider
- Interchangeable at runtime

### 6. Plugin Architecture
- Load providers from external assemblies
- Extend functionality without modifying core

### 7. Repository Pattern
- Abstraction over data access
- Testable without database

### 8. Dependency Injection
- All dependencies injected via constructor
- Configured in Program.cs

### 9. Specification Pattern (Optional)
- Complex queries
- File search filters

---

## Dependency Flow

```
API Layer
    ↓ depends on
Application Layer
    ↓ depends on
Domain Layer
    ↑ implemented by
Infrastructure Layer
```

**Key Rule:** Dependencies point inward toward Domain

**Allowed:**
- ✅ API → Application
- ✅ API → Domain
- ✅ Application → Domain
- ✅ Infrastructure → Domain
- ✅ Infrastructure → Application (interfaces only)

**Forbidden:**
- ❌ Domain → Application
- ❌ Domain → Infrastructure
- ❌ Domain → API

---

## Data Flow

### Upload Flow

```
User (Mobile App)
    ↓ HTTP POST
API Controller
    ↓ Map to Command
Application Service (FileUploadService)
    ↓ Create Entity
Domain Entity (File)
    ↓ Save
Repository Interface (IFileRepository)
    ↓ Implement
Infrastructure Repository
    ↓ Persist
Database (PostgreSQL)

    AND

Application Service
    ↓ Select Provider
Storage Provider Selector (Domain Service)
    ↓ Get Provider
Storage Provider Factory (Infrastructure)
    ↓ Upload
Storage Provider Implementation
    ↓ Store
Cloud Storage (S3 Glacier / Backblaze)
```

### Retrieval Flow

```
User Request
    ↓ HTTP GET
API Controller
    ↓ Query
Application Service (FileRetrievalService)
    ↓ Load Entity
Repository
    ↓ Return
Domain Entity (File)
    ↓ Check Status
Application Service
    ↓ If in Glacier
Storage Provider
    ↓ Initiate Retrieval
AWS Glacier
    ↓ Poll Status
Application Service
    ↓ When Ready
Generate Download URL
    ↓ Return
User Downloads File
```

---

## Technology Stack

### Core
- **.NET 8** - Latest LTS version
- **C# 12** - Latest language features
- **ASP.NET Core 8** - Web API framework

### Data Access
- **Entity Framework Core 8** - ORM
- **PostgreSQL** - Primary database
- **Npgsql** - PostgreSQL driver

### Testing
- **xUnit** - Test framework
- **FluentAssertions** - Assertion library
- **Moq** - Mocking framework
- **Bogus** - Test data generation
- **Testcontainers** - Integration tests with Docker

### Cloud Storage
- **AWSSDK.S3** - Amazon S3 SDK
- **AWSSDK.Glacier** - Amazon Glacier SDK
- **Backblaze.Client** - Backblaze B2 SDK

### Background Jobs
- **Hangfire** - Background job processing (or)
- **Quartz.NET** - Job scheduling

### Caching
- **Redis** - Distributed cache
- **StackExchange.Redis** - Redis client

### Authentication
- **Microsoft.AspNetCore.Authentication.JwtBearer** - JWT auth
- **IdentityServer** or **Auth0** - OAuth2 provider

### Logging
- **Serilog** - Structured logging
- **Serilog.Sinks.Console** - Console output
- **Serilog.Sinks.File** - File output
- **Serilog.Sinks.Seq** - Centralized logging

### Monitoring
- **Prometheus** - Metrics
- **Grafana** - Dashboards
- **OpenTelemetry** - Distributed tracing

### API Documentation
- **Swashbuckle** - Swagger/OpenAPI

### Other
- **AutoMapper** - Object mapping
- **FluentValidation** - Request validation
- **MediatR** - CQRS implementation (optional)
- **Polly** - Resilience and retry policies

---

## Deployment Architecture

```
┌─────────────────────────────────────────────────┐
│               Load Balancer                     │
│            (AWS ALB / Nginx)                    │
└────────────────┬────────────────────────────────┘
                 │
    ┌────────────┴────────────┐
    ▼                         ▼
┌─────────┐             ┌─────────┐
│ API #1  │             │ API #2  │
│ (Pod)   │             │ (Pod)   │
└────┬────┘             └────┬────┘
     │                       │
     └───────────┬───────────┘
                 ▼
        ┌────────────────┐
        │   PostgreSQL   │
        │   (Primary)    │
        └────────────────┘
                 │
                 ▼
        ┌────────────────┐
        │   PostgreSQL   │
        │   (Replica)    │
        └────────────────┘

     ┌───────────┬───────────┐
     ▼           ▼           ▼
┌─────────┐ ┌─────────┐ ┌─────────┐
│  Redis  │ │Hangfire │ │  Seq    │
│ (Cache) │ │ (Jobs)  │ │  (Logs) │
└─────────┘ └─────────┘ └─────────┘

     ┌───────────┬───────────┬──────────┐
     ▼           ▼           ▼          ▼
┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
│S3 Glacier│ │S3 Glacier│ │Backblaze │ │CloudFront│
│   Deep   │ │   Flex   │ │    B2    │ │  (CDN)   │
└──────────┘ └──────────┘ └──────────┘ └──────────┘
```

### Container Strategy
- Docker containers for API
- Kubernetes for orchestration
- Horizontal scaling based on load

### Database
- PostgreSQL with replication
- Connection pooling
- Automated backups

### Caching
- Redis for distributed cache
- Thumbnail URLs cached
- User quota cached

### Job Processing
- Hangfire for background jobs
- Separate worker processes
- Job retry policies

---

## Security Considerations

1. **Authentication:** OAuth2 with JWT tokens
2. **Authorization:** Role-based access control (RBAC)
3. **Encryption:** TLS 1.3 for all API calls
4. **Data at Rest:** S3 server-side encryption (SSE-S3)
5. **Secrets Management:** AWS Secrets Manager or Azure Key Vault
6. **Input Validation:** FluentValidation on all requests
7. **SQL Injection:** Parameterized queries via EF Core
8. **Rate Limiting:** Prevent abuse
9. **CORS:** Configured for specific origins

---

## Performance Optimization

1. **Async/Await:** All I/O operations asynchronous
2. **Caching:** Redis for frequently accessed data
3. **CDN:** CloudFront for thumbnail delivery
4. **Connection Pooling:** Database connections
5. **Pagination:** All list endpoints paginated
6. **Indexing:** Database indexes on frequently queried columns
7. **Lazy Loading:** Disabled in EF Core (explicit eager loading)
8. **Compression:** Gzip for API responses
9. **Chunked Upload:** For large files
10. **Background Jobs:** Offload heavy processing

---

**Last Updated:** 2025-10-25
**Document Owner:** FlexStorage Team
