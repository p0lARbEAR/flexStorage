# FlexStorage Backend

**Version:** 1.0.0 (Phase 1 - MVP in progress)
**Architecture:** Domain-Driven Design (DDD)
**Framework:** .NET 8
**Testing:** xUnit, FluentAssertions, TDD

---

## Project Structure

```
backend/
├── src/
│   ├── FlexStorage.Domain/          # Core business logic (no dependencies)
│   │   ├── Entities/                # File, FileMetadata, etc.
│   │   ├── ValueObjects/            # FileSize, FileType, StorageLocation, etc.
│   │   ├── DomainServices/          # IStorageProvider, StorageProviderSelector
│   │   ├── DomainEvents/            # FileUploadStarted, FileArchived, etc.
│   │   └── Common/                  # Base classes, interfaces
│   ├── FlexStorage.Application/     # Use cases and orchestration
│   │   ├── Services/                # FileUploadService, ChunkedUploadService, etc.
│   │   ├── Interfaces/Repositories/ # IFileRepository, IUnitOfWork
│   │   ├── DTOs/                    # Request/Response objects
│   │   └── Mappings/                # AutoMapper profiles
│   ├── FlexStorage.Infrastructure/  # External dependencies
│   │   ├── Persistence/             # EF Core, repositories
│   │   ├── StorageProviders/        # S3, Backblaze implementations
│   │   ├── BackgroundJobs/          # Hangfire jobs
│   │   └── ExternalServices/        # OAuth2, webhooks
│   └── FlexStorage.API/             # HTTP endpoints
│       ├── Controllers/             # REST API controllers
│       ├── Middleware/              # Authentication, rate limiting
│       └── Filters/                 # Validation, error handling
├── tests/
│   ├── FlexStorage.Domain.Tests/
│   ├── FlexStorage.Application.Tests/
│   ├── FlexStorage.Infrastructure.Tests/
│   ├── FlexStorage.API.Tests/
│   └── FlexStorage.IntegrationTests/
└── plugins/                         # Storage provider plugins
```

---

## Prerequisites

### Required

- **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **PostgreSQL 15+** - For database (local or Docker)
- **Redis** (optional for Phase 2+) - For caching and job queues

### Optional (for full local development)

- **Docker** - For running PostgreSQL, Redis, LocalStack (S3 emulation)
- **AWS CLI** - For S3/Glacier provider testing
- **Backblaze B2 account** - For Backblaze provider testing

---

## Getting Started

### 1. Install .NET 8 SDK

```bash
# Check if .NET 8 is installed
dotnet --version

# If not installed, download from:
# https://dotnet.microsoft.com/download/dotnet/8.0
```

### 2. Restore Dependencies

```bash
cd backend
dotnet restore
```

### 3. Build Solution

```bash
dotnet build
```

### 4. Run Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/FlexStorage.Domain.Tests/

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

---

## Development Workflow (TDD)

We follow **Test-Driven Development (TDD)** with the **Red-Green-Refactor** cycle:

### Step 1: RED - Write Failing Test

```csharp
[Fact]
public void Should_CreateValidFileSize_WithBytes()
{
    // Arrange
    var bytes = 1024L;

    // Act
    var fileSize = FileSize.FromBytes(bytes);

    // Assert
    fileSize.Should().NotBeNull();
    fileSize.Bytes.Should().Be(bytes);
}
```

### Step 2: GREEN - Make Test Pass

```csharp
public sealed class FileSize
{
    public long Bytes { get; }

    private FileSize(long bytes)
    {
        Bytes = bytes;
    }

    public static FileSize FromBytes(long bytes) => new(bytes);
}
```

### Step 3: REFACTOR - Improve Code

Add validation, edge cases, etc.

### Step 4: COMMIT

```bash
git add .
git commit -m "feat: implement FileSize value object with validation"
```

**Repeat for each feature!**

---

## Current Progress

### ✅ Completed

- [x] Project structure setup
- [x] Domain Layer: FileSize value object with tests

### 🔄 In Progress

- [ ] Domain Layer: FileType value object
- [ ] Domain Layer: StorageLocation value object
- [ ] Domain Layer: UploadStatus value object

### 📋 Upcoming (Phase 1 - MVP)

See **TEST_PLAN.md** in root for complete test checklist.

---

## Running the Application

**Note:** The API is not yet runnable (Phase 1 in progress). Once Phase 1 (MVP) is complete:

### Using dotnet CLI

```bash
cd src/FlexStorage.API
dotnet run
```

### Using Docker

```bash
docker build -t flexstorage-api .
docker run -p 5000:5000 flexstorage-api
```

---

## Configuration

Configuration is in `src/FlexStorage.API/appsettings.json`:

```json
{
  "Authentication": {
    "ApiKey": {
      "Enabled": true,
      "Keys": {
        "dev-user-1": "user-guid-1"
      }
    }
  },
  "StorageProviders": {
    "S3GlacierDeep": {
      "Enabled": true,
      "Config": {
        "Region": "us-west-2",
        "BucketName": "flexstorage-glacier-deep",
        "AccessKeyId": "",
        "SecretAccessKey": ""
      }
    }
  },
  "Database": {
    "ConnectionString": "Host=localhost;Database=flexstorage;Username=postgres;Password=postgres"
  }
}
```

For sensitive values, use **User Secrets** or **environment variables**:

```bash
dotnet user-secrets set "StorageProviders:S3GlacierDeep:Config:AccessKeyId" "your-key"
```

---

## Testing

### Unit Tests

```bash
# Domain layer (no dependencies)
dotnet test tests/FlexStorage.Domain.Tests/

# Application layer (mocked dependencies)
dotnet test tests/FlexStorage.Application.Tests/
```

### Integration Tests

```bash
# Requires Docker for PostgreSQL, S3 emulation
dotnet test tests/FlexStorage.IntegrationTests/
```

### Coverage Report

```bash
dotnet test /p:CollectCoverage=true \
            /p:CoverletOutputFormat=opencover \
            /p:CoverletOutput=./coverage/

# Generate HTML report
reportgenerator -reports:coverage/coverage.opencover.xml \
                -targetdir:coverage/report \
                -reporttypes:Html
```

---

## Contributing

1. **Follow TDD**: Write tests first, then implementation
2. **Commit often**: After each GREEN phase
3. **Follow architecture**: Respect DDD layer boundaries
4. **Check coverage**: Aim for 90%+ overall, 100% on Domain layer
5. **Update TEST_PLAN.md**: Mark tests as ✅ when passing

---

## Architecture

FlexStorage follows **Clean Architecture** with **DDD** principles:

- **Domain Layer**: Pure C#, no external dependencies
- **Application Layer**: Orchestrates use cases, depends only on Domain
- **Infrastructure Layer**: Implements interfaces, external dependencies
- **API Layer**: HTTP endpoints, depends on Application

See **../ARCHITECTURE.md** for detailed architecture documentation.

---

## Phase 1 (MVP) Goals

### Features
- ✅ Simple file upload (< 10MB)
- ✅ File metadata storage
- ✅ S3 Glacier Deep Archive provider
- ✅ S3 Glacier Flexible Retrieval provider
- ✅ API Key authentication (local dev)
- ✅ File retrieval from Glacier
- ✅ Plugin interface (IStorageProvider)

### Timeline
Week 1-2

---

## Resources

- **Backend Spec**: `../BACKEND_SPEC.md`
- **Test Plan**: `../TEST_PLAN.md`
- **Architecture**: `../ARCHITECTURE.md`
- **Plugin Development**: `../PLUGIN_DEVELOPMENT.md`
- **App Spec** (future): `../APP_SPEC.md`

---

## Troubleshooting

### .NET SDK not found

```bash
# Download and install .NET 8 SDK
# https://dotnet.microsoft.com/download/dotnet/8.0
```

### Build errors

```bash
# Clean and rebuild
dotnet clean
dotnet build --no-incremental
```

### Test failures

```bash
# Run specific test with verbose output
dotnet test --logger "console;verbosity=detailed" --filter "Should_CreateValidFileSize_WithBytes"
```

---

**Last Updated:** 2025-10-25
**Maintainer:** FlexStorage Team
