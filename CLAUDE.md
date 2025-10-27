# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

FlexStorage is a photo/video archival service backend built with **.NET 8** using **Domain-Driven Design (DDD)** and **Test-Driven Development (TDD)**. The system provides long-term cold storage using AWS S3 Glacier with a plugin architecture for extensible storage providers.

**Key Technologies:** C# 12, ASP.NET Core 8, Entity Framework Core 8, xUnit, FluentAssertions, AWS S3 SDK, LocalStack

## Architecture

### DDD Layer Structure

```
Domain Layer (No dependencies)
   ↑
Application Layer (Depends on Domain)
   ↑
Infrastructure Layer (Implements Domain/Application interfaces)
   ↑
API Layer (HTTP endpoints)
```

**Critical Rule:** Dependencies flow inward. Domain layer has ZERO external dependencies.

### Project Locations

- **Domain:** `backend/src/FlexStorage.Domain/` - Pure C# business logic
- **Application:** `backend/src/FlexStorage.Application/` - Use cases and orchestration
- **Infrastructure:** `backend/src/FlexStorage.Infrastructure/` - EF Core, S3 providers, external services
- **API:** `backend/src/FlexStorage.API/` - ASP.NET Core controllers
- **Tests:** `backend/tests/FlexStorage.{Layer}.Tests/` - Organized by layer

## Development Commands

### Build and Run

```bash
# Build entire solution
cd backend
dotnet build

# Run API with LocalStack (recommended for development)
./scripts/start-localstack.sh    # Start LocalStack first
./scripts/run-dev.sh              # Run API with dev environment

# Run API directly
cd backend
dotnet run --project src/FlexStorage.API
```

### Testing

```bash
# Run all tests
cd backend
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test tests/FlexStorage.Domain.Tests/
dotnet test tests/FlexStorage.Application.Tests/
dotnet test tests/FlexStorage.Infrastructure.Tests/
dotnet test tests/FlexStorage.API.Tests/
dotnet test tests/FlexStorage.IntegrationTests/

# Run single test class
dotnet test --filter "FullyQualifiedName~FileSizeTests"

# Run single test method
dotnet test --filter "FullyQualifiedName~FileSizeTests.FromBytes_WithValidSize_ShouldCreateFileSize"

# Run integration tests (requires LocalStack)
./scripts/start-localstack.sh
dotnet test tests/FlexStorage.IntegrationTests/ --filter "FullyQualifiedName~LocalStackS3IntegrationTests"
```

### LocalStack (AWS Simulation)

```bash
# Start LocalStack with S3/Glacier
./scripts/start-localstack.sh

# Stop LocalStack
./scripts/stop-localstack.sh

# Initialize/reset buckets
./scripts/init-localstack.sh

# Verify LocalStack health
curl http://localhost:4566/_localstack/health

# List S3 buckets
aws s3 ls --endpoint-url=http://localhost:4566
```

## TDD Workflow

**ALWAYS follow Red-Green-Refactor cycle:**

1. **RED:** Write failing test first
2. **GREEN:** Write minimal code to make it pass (verify implementation exists and works)
3. **REFACTOR:** Improve code without changing behavior

### Test Organization

Tests follow the **AAA pattern** (Arrange-Act-Assert):

```csharp
[Fact]
public void MethodName_Condition_ExpectedResult()
{
    // Arrange
    var input = CreateTestData();

    // Act
    var result = _sut.MethodUnderTest(input);

    // Assert
    result.Should().Be(expectedValue);
}
```

**Test Naming:** Use `MethodName_Condition_ExpectedResult` format for clarity.

### Test User ID

**Always use this consistent test user ID in tests:**
```csharp
var userId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");
```

## Code Patterns

### Value Objects

Value objects are **immutable** and **self-validating**:

```csharp
public sealed class FileSize : IEquatable<FileSize>
{
    public long Bytes { get; }

    private FileSize(long bytes)
    {
        if (bytes <= 0)
            throw new ArgumentException("...");
        Bytes = bytes;
    }

    public static FileSize FromBytes(long bytes) => new(bytes);
}
```

### Entities

Entities have **identity** and enforce **invariants**:

```csharp
public sealed class File : Entity, IAggregateRoot
{
    public FileId Id { get; private set; }

    // Private constructor for EF Core
    private File() { }

    // Factory method
    public static File Create(...) { }

    // Domain methods only
    public void CompleteUpload(StorageLocation location) { }
}
```

### Repository Pattern

Repositories use **async/await** and **CancellationToken**:

```csharp
public interface IFileRepository
{
    Task<File?> GetByIdAsync(FileId id, CancellationToken cancellationToken);
    Task AddAsync(File file, CancellationToken cancellationToken);
    Task<PagedResult<File>> GetByUserIdAsync(UserId userId, int page, int pageSize, CancellationToken cancellationToken);
}
```

### Storage Providers

All storage providers implement `IStorageProvider`:

```csharp
public interface IStorageProvider
{
    string ProviderName { get; }
    ProviderCapabilities Capabilities { get; }

    Task<UploadResult> UploadAsync(Stream fileStream, UploadOptions options, CancellationToken cancellationToken);
    Task<Stream> DownloadAsync(StorageLocation location, CancellationToken cancellationToken);
    Task<RetrievalResult> InitiateRetrievalAsync(StorageLocation location, RetrievalTier tier, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(StorageLocation location, CancellationToken cancellationToken);
    Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken);
}
```

## Commit Guidelines

### Commit Message Format

```
<type>: <description>

[optional body]
```

**Types:** `feat`, `fix`, `refactor`, `test`, `docs`, `chore`

### Small, Focused Commits

- **Make small commits:** One logical change per commit
- **Commit frequently:** After each test passes (GREEN phase)
- **Push when requested:** Don't push automatically unless asked

**Example workflow:**
```bash
git add FileRepositoryTests.cs
git commit -m "test: add GetByIdAsync_ShouldReturnFileWhenExists test"

git add FileRepository.cs
git commit -m "feat: implement GetByIdAsync in FileRepository"
```

## Important Patterns from Recent Work

### 1. Helper Methods in Tests

Create flexible helper methods with optional parameters:

```csharp
private File CreateTestFile(
    UserId? userId = null,
    string? fileName = null,
    DateTime? capturedAt = null)
{
    return File.Create(
        userId ?? UserId.New(),
        FileMetadata.Create(fileName ?? "test.jpg", "hash", capturedAt),
        FileSize.FromBytes(1024),
        FileType.FromMimeType("image/jpeg")
    );
}
```

### 2. Integration Test Setup

Use `IAsyncLifetime` for proper setup/teardown:

```csharp
public class LocalStackS3IntegrationTests : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Create test buckets
        await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = "test-bucket" });
    }

    public async Task DisposeAsync()
    {
        // Cleanup resources
    }
}
```

### 3. Skippable Tests

Mark integration tests that require external services:

```csharp
[Fact(Skip = "Requires LocalStack running - run manually with docker-compose up")]
public async Task FullE2ETest()
{
    // Test implementation
}
```

## Key Files to Reference

- **ARCHITECTURE.md** - Detailed DDD architecture and layer responsibilities
- **TEST_PLAN.md** - Complete test strategy, current progress (182 tests), and test organization
- **BACKEND_SPEC.md** - Feature specifications and MVP requirements
- **LOCAL_TESTING.md** - LocalStack setup and API testing instructions
- **PLUGIN_DEVELOPMENT.md** - Guide for creating custom storage provider plugins

## Configuration

### Development Environment

The API uses **appsettings.Development.json** with LocalStack configuration:

```json
{
  "AWS": {
    "ServiceURL": "http://localhost:4566",
    "AccessKey": "test",
    "SecretKey": "test",
    "ForcePathStyle": true
  }
}
```

### Environment Variables

```bash
ASPNETCORE_ENVIRONMENT=Development
AWS_ACCESS_KEY_ID=test
AWS_SECRET_ACCESS_KEY=test
AWS_ENDPOINT_URL=http://localhost:4566
```

## Common Issues

### LocalStack Connection Errors

**Problem:** "Unable to get IAM security credentials"
**Solution:**
1. Verify LocalStack is running: `docker ps | grep localstack`
2. Ensure `ForcePathStyle = true` in S3 client config
3. Check endpoint: `http://localhost:4566`

### Test Failures

**Problem:** Integration tests fail
**Solution:**
1. Start LocalStack: `./scripts/start-localstack.sh`
2. Verify buckets exist: `aws s3 ls --endpoint-url=http://localhost:4566`
3. Check test attributes for `Skip` parameter

## Current Test Status

**Total: 182 tests** (as of latest commit)

- Domain Layer: 88 tests ✅
- Application Layer: 30 tests ✅ (MVP complete)
- Infrastructure Layer: 43 tests ✅ (FileRepository + S3 Providers)
- API Layer: 13 tests ✅ (FilesController)
- Integration Tests: 8 tests ✅ (3 unit + 5 E2E)

See TEST_PLAN.md for detailed breakdown and next steps.

## Working with This Codebase

### When Adding New Features

1. **Start with Domain Layer** - Add value objects, entities, or domain services
2. **Write tests first** (TDD) - Follow RED-GREEN-REFACTOR
3. **Add Application Layer** - Create service interfaces and implementations
4. **Implement Infrastructure** - Add repository/provider implementations
5. **Add API endpoints** - Create controllers with proper validation
6. **Update TEST_PLAN.md** - Document new tests added

### When Fixing Bugs

1. **Write a failing test** that reproduces the bug
2. **Fix the bug** to make the test pass
3. **Verify** all existing tests still pass
4. **Commit** with descriptive message: `fix: description of bug fixed`

### Code Review Checklist

- [ ] Tests follow TDD (RED-GREEN-REFACTOR)
- [ ] Consistent test user ID used
- [ ] Dependencies flow inward (Domain has no dependencies)
- [ ] Async/await with CancellationToken
- [ ] Value objects are immutable
- [ ] Entities enforce invariants
- [ ] Small, focused commits
- [ ] TEST_PLAN.md updated if tests added

## API Testing Quick Reference

```bash
# Generate API key
POST http://localhost:5000/api/auth/apikey
Content-Type: application/json
{"userId": "123e4567-e89b-12d3-a456-426614174000", "description": "Test"}

# Upload file
POST http://localhost:5000/api/files/upload
X-API-Key: fsk_...
Content-Type: multipart/form-data

# List files
GET http://localhost:5000/api/files
X-API-Key: fsk_...

# Download file
GET http://localhost:5000/api/files/{id}/download
X-API-Key: fsk_...
```

Swagger UI available at: http://localhost:5000/swagger

## Solution Structure

```
backend/
├── src/
│   ├── FlexStorage.Domain/           # Core business logic (no dependencies)
│   ├── FlexStorage.Application/      # Use cases and orchestration
│   ├── FlexStorage.Infrastructure/   # EF Core, S3, external services
│   └── FlexStorage.API/             # Controllers, middleware
└── tests/
    ├── FlexStorage.Domain.Tests/
    ├── FlexStorage.Application.Tests/
    ├── FlexStorage.Infrastructure.Tests/
    ├── FlexStorage.API.Tests/
    └── FlexStorage.IntegrationTests/  # E2E tests with LocalStack
```

## Documentation Links

- Architecture details: `ARCHITECTURE.md`
- Feature specifications: `BACKEND_SPEC.md`
- Test strategy: `TEST_PLAN.md`
- Local development: `LOCAL_TESTING.md`
- Plugin development: `PLUGIN_DEVELOPMENT.md`
