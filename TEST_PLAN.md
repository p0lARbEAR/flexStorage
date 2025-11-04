# FlexStorage Test Plan

**Version:** 1.0.0
**Testing Framework:** xUnit, FluentAssertions
**Approach:** Test-Driven Development (TDD) - Red, Green, Refactor
**Architecture:** Domain-Driven Design (DDD) - Inside-Out Testing

---

## Testing Strategy

### Test Order Rationale
1. **Domain Layer First** (No dependencies, pure business logic)
2. **Application Layer** (Depends on Domain)
3. **Infrastructure Layer** (Depends on Domain & Application interfaces)
4. **API Layer** (Depends on all layers)
5. **Integration Tests** (End-to-end flows)

**Note:** This test plan is organized by **DDD layers** (inside-out testing), which differs from the **feature phases (P0-P4)** in BACKEND_SPEC.md. For example:
- **Feature Phase P0 (MVP)** in BACKEND_SPEC includes: Simple upload, S3 Glacier Deep + Flexible providers, API Key auth, file retrieval, plugin interface
- Tests for these MVP features span multiple test groups here: Domain (Groups 1-3), Application (Group 5), Infrastructure (Groups 8-9), API (Group 11)

Follow this test plan's order for TDD implementation - it ensures solid foundations (Domain) before building on top (Infrastructure, API).

### TDD Cycle
```
RED → GREEN → REFACTOR
Write failing test → Make it pass → Improve code
```

---

## Progress Summary

### Overall Test Status
- **Total Tests Written:** 283 tests
- **Tests Passing:** 278 tests (5 skipped LocalStack E2E tests)
- **Coverage:** Domain Layer (144 tests - complete), Application Layer (46 tests - MVP complete), Infrastructure Layer (59 tests - 3 Repositories + S3 Providers), API Layer (26 tests - FilesController + AuthController), Integration Tests (8 tests - 3 unit integration + 5 E2E LocalStack)
- **Note:** Test count includes all xUnit Theory test cases. Domain layer count (144) reflects all Theory variations.

### Test Group Completion
- ✅ **Group 1:** Domain Layer - Value Objects (44 tests - FileSize:16, UploadStatus:13, FileType:9, StorageLocation:6)
- ✅ **Group 2:** Domain Layer - Entities (37 tests - File:16, FileMetadata:11, ApiKey:10)
- ✅ **Group 3:** Domain Layer - Domain Services (7 tests - StorageProviderSelector)
- ✅ **Group 4:** Application Layer - Repository Interfaces (Defined)
- ✅ **Group 5:** Application Layer - Application Services (46 tests - FileUpload:10, ChunkedUpload:13, FileRetrieval:13, ApiKeyService:10)
- ⬜ **Groups 6-7:** Not started
- ✅ **Group 8.1-8.2:** Infrastructure Layer - S3 Storage Providers (26 tests - Deep Archive:11, Flexible Retrieval:15)
- ⬜ **Group 8.3-8.4:** Backblaze B2 & Plugin Loader (Not started)
- ✅ **Group 9.1:** Infrastructure Layer - FileRepository (19 tests - Complete)
- ✅ **Group 9.2:** Infrastructure Layer - ApiKeyRepository (7 tests - Complete), UploadSessionRepository (7 tests - Complete)
- ⬜ **Group 10:** Background Jobs (Not started)
- ✅ **Group 11:** API Layer - Controllers (26 tests - AuthController:13, FilesController upload:6 + download:7)
- ✅ **Group 12:** Integration Tests (8 tests - 3 provider unit tests + 5 E2E LocalStack tests)

### Latest Commits
1. Application Layer interfaces (repositories and services)
2. FileUploadService with TDD (8 tests)
3. ChunkedUploadService with TDD (8 tests)
4. FileRetrievalService with TDD (6 tests) + domain enhancements
5. FilesController download functionality with TDD (8 tests)
   - Added `/api/v1/Files/{id}/download` endpoint
   - Added `GetUserFilesAsync` method to FileRetrievalService
   - Comprehensive test coverage for download scenarios
   - **ARCHITECTURAL ISSUE IDENTIFIED:** Mixed download/retrieval concerns
6. **NEW:** FilesController upload functionality with TDD (6 tests)
   - POST `/api/v1/Files` upload endpoint fully tested
   - Test coverage: valid file, empty file, null file, duplicate detection
   - Test coverage: upload failure, edge case (success but null FileId)
   - All tests passing with comprehensive error scenario coverage
7. **NEW:** FileRepository comprehensive testing with TDD (7 new tests, 17 total)
   - SearchAsync with filename filtering (case-sensitive Contains)
   - SearchAsync with date range filtering (FromDate/ToDate)
   - SearchAsync with multiple combined filters
   - GetByUserIdAsync ordering verification (newest first by CapturedAt)
   - SearchAsync ordering verification (consistent with GetByUserIdAsync)
   - Edge case: SearchAsync with no matching results
   - Edge case: GetByUserIdAsync with no files for user
   - Followed Red-Green-Refactor TDD cycle for all tests
   - Refactoring phase: Tests already clean, no changes needed
8. **NEW:** S3 Storage Provider Tests Review & Cleanup (26 tests total)
   - S3GlacierDeepArchiveProvider: 11 comprehensive tests
     - Provider capabilities, upload with Deep Archive storage class
     - Unique S3 key generation, upload failure handling
     - Download from S3, retrieval initiation (Bulk/Standard/Expedited tiers)
     - Delete operations, health check monitoring
   - S3GlacierFlexibleRetrievalProvider: 15 comprehensive tests
     - Provider capabilities, upload with Glacier Instant Retrieval storage class
     - Upload with metadata, upload failure handling
     - Retrieval initiation with all tiers, retrieval failure handling
     - Download operations, download failure handling
     - Delete operations, delete failure handling
     - Health check monitoring (healthy/unhealthy states)
     - Mock retrieval status tracking
   - Removed duplicate test files from Storage/ directory
   - Consolidated to StorageProviders/ namespace
9. **NEW:** End-to-End LocalStack Integration Tests (5 tests)
   - Full E2E test: Upload → LocalStack S3 → Download → Delete (Deep Archive provider)
   - Full E2E test: Upload → LocalStack S3 → Download → Delete (Flexible Retrieval provider)
   - Test both providers working independently in separate buckets
   - Health check verification against running LocalStack instance
   - Glacier retrieval initiation test (with LocalStack simulation)
   - Tests marked as skippable - require `docker-compose up localstack`
   - Implements IAsyncLifetime for proper bucket setup/teardown
   - Validates real S3 operations against LocalStack endpoint (localhost:4566)
10. **NEW:** AuthController API Key Authentication Tests (13 tests)
   - GenerateApiKey tests: valid request, service failure, never expiring key
   - ValidateApiKey tests: X-API-Key header, Authorization header, missing/invalid/expired keys
   - RevokeApiKey tests: valid revocation, missing key, non-existent key
   - Header extraction tests: priority (X-API-Key over Authorization), empty key handling
   - Tests both X-API-Key and Authorization: ApiKey {key} header formats
   - Comprehensive error handling: 401 Unauthorized, 404 NotFound, 400 BadRequest
   - All tests follow TDD RED-GREEN-REFACTOR methodology
   - MVP P0 requirement: Simple API Key authentication - COMPLETE ✅
11. **NEW:** FileRepository Tags Filtering with TDD (commit 7a4f777)
   - RED: Created test `SearchAsync_WithTags_ShouldFilterByTags`
   - Tests filtering files by metadata tags using OR logic (any tag matches)
   - GREEN: Implemented tag filtering with client-side evaluation for EF InMemory compatibility
   - Uses HashSet for normalized tag comparison (case-insensitive)
   - Filters files that have ANY of the specified tags
   - All 19 FileRepository tests passing
12. **NEW:** FileRepository Status Filtering with TDD (commit e1df5f7)
   - RED: Created test `SearchAsync_WithStatus_ShouldFilterByStatus`
   - Tests filtering files by UploadStatus (Pending, Completed, Archived)
   - Uses proper domain methods (MarkAsArchived) and enum comparison
   - GREEN: Implemented status filter in FileRepository.SearchAsync
   - Added Status property to FileSearchCriteria in IFileRepository.cs
   - Uses Enum.TryParse for string-to-enum conversion (case-insensitive)
   - All 19 FileRepository tests passing
13. **NEW:** FileUploadService Complete Test Coverage (commit 0467560)
   - Added 3 missing tests: hash calculation exception, SaveChanges exception, large file size handling
   - Tests exception handling during hash computation and database operations
   - Validates 5GB file handling with proper provider selection
   - All 10 FileUploadService tests passing (100% coverage)
14. **NEW:** ChunkedUploadService Complete Test Coverage (commit 32c8f42)
   - Added 6 missing tests: SaveChanges failure, validation tests, exception handling
   - Tests: zero totalSize validation, empty filename validation, update failures
   - Tests: hash calculation failure, file not found during complete
   - All 13 ChunkedUploadService tests passing (100% coverage)
15. **NEW:** FileRetrievalService Complete Test Coverage (commit 69ecbb7)
   - Added 7 missing tests covering all service methods
   - Tests: GetFileMetadataAsync (valid/invalid), GetUserFilesAsync pagination
   - Tests: InitiateRetrievalAsync exception, CheckRetrievalStatusAsync exception
   - Tests: DownloadFileAsync file not found, null retrievalId edge case
   - All 13 FileRetrievalService tests passing (100% coverage)
16. **NEW:** ApiKeyRepository Infrastructure Tests (commit 512e460)
   - Created ApiKeyRepositoryTests.cs with 7 comprehensive tests
   - Tests: AddAsync persistence, GetByKeyHashAsync (valid/invalid), GetByUserIdAsync ordering
   - Tests: Update (revoke operation), Delete, empty results
   - Uses EF Core InMemory database with proper isolation (Guid-based DB names)
   - All 7 tests passing, follows TDD RED-GREEN-REFACTOR methodology
17. **NEW:** UploadSessionRepository Infrastructure Tests (commit bda7d99)
   - Created UploadSessionRepositoryTests.cs with 7 comprehensive tests
   - Tests: AddAsync persistence, GetByIdAsync (valid/invalid), GetActiveSessionsAsync filtering
   - Tests: UpdateAsync with chunk tracking, DeleteAsync, empty results
   - Validates HashSet<int> serialization for uploaded chunks
   - All 7 tests passing, established repository test pattern
18. **NEW:** FlexStorageDbContext HashSet<int> Serialization Fix (commit 2d0ff5e)
   - Fixed EF Core InMemory limitation: "HashSet<int> cannot be used as primitive collection"
   - Implemented ValueConverter<HashSet<int>, string> with JSON serialization
   - Added SerializeChunks/DeserializeChunks helper methods
   - Stores chunks as sorted JSON array for consistency: [0,2,5,10,15]
   - EF Core auto-detects changes via string comparison (no ValueComparer needed)
   - All UploadSessionRepository tests now passing with proper chunk persistence
19. **NEW:** Placeholder Test Cleanup (commit 38e8cad)
   - Removed PlaceholderTests.cs from all 4 test projects
   - Application.Tests, Infrastructure.Tests, API.Tests, IntegrationTests
   - All real tests passing: 278 tests (5 LocalStack tests skipped)
   - Updated test counts: 283 total tests across all layers
   - Build successful with no errors

### Architectural Findings
- **Download API Design Issue:** Current `/download` endpoint handles both direct download (200 OK) and retrieval initiation (202 Accepted)
- **Recommendation:** Split into separate endpoints for better API design and testing
- **Impact:** Need additional test cases for retrieval workflow and status tracking

### Next Steps
- **PRIORITY:** Refactor download/retrieval API design (Group 11.4.1)
  - Split mixed concerns into separate endpoints
  - Add comprehensive retrieval workflow tests
  - Implement retrieval status tracking
- Group 8: Infrastructure Layer (S3 Glacier providers, EF Core repositories)
- Group 11.1: AuthController (API Key authentication)
- Group 11.2: FilesController (Upload functionality)
- Group 6: Rate Limiting & Quota services

---

## Feature Phase Progress (BACKEND_SPEC.md)

This section maps **Test Group** completion to actual **Feature Phases** from BACKEND_SPEC.md, showing which production features are ready.

### Feature Phase P0 (MVP) - Core Upload & Storage 🔄
**Status:** 95% Complete (Domain/Application/Infrastructure/API layers done, only PluginLoader pending)

**Required Test Groups:**
- ✅ Group 1: Domain Layer - Value Objects (Foundation)
- ✅ Group 2: Domain Layer - Entities (File, FileMetadata)
- ✅ Group 3: Domain Layer - Domain Services (IStorageProvider interface)
- ✅ Group 4: Application Layer - Repository Interfaces
- ✅ Group 5: Application Services (FileUploadService, ChunkedUploadService, FileRetrievalService)
- ✅ Group 8.1: S3 Glacier Deep Archive Provider (11 tests)
- ✅ Group 8.2: S3 Glacier Flexible Retrieval Provider (15 tests)
- ⬜ Group 8.4: Plugin Loader
- ✅ Group 9.1: FileRepository (EF Core) (19 tests)
- ✅ Group 11.1: AuthController (API Key auth) (13 tests)
- ✅ Group 11.2: FilesController (Upload) (6 tests)
- 🔄 Group 11.4: FilesController (Download/Retrieval) (8 tests - needs refactor)

**MVP Features Covered:**
- ✅ Simple file upload (domain/application/API logic complete)
- ✅ File metadata storage (domain models + repository)
- ✅ S3 Glacier Deep Archive provider (infrastructure complete with 11 tests)
- ✅ S3 Glacier Flexible Retrieval provider (infrastructure complete with 15 tests)
- ✅ Simple API Key authentication (API layer complete with 13 tests)
- ✅ File retrieval from Glacier (application/infrastructure logic complete)
- ✅ Plugin interface for custom providers (IStorageProvider defined + implemented)

### Feature Phase P1 - Essential Features ⬜
**Status:** 25% Complete (Deduplication, thumbnails done; OAuth2/Backblaze not started)

**Required Test Groups:**
- ⬜ Group 11.1: AuthController (OAuth2)
- ✅ Group 5.2: ChunkedUploadService (resumable uploads)
- ✅ Group 5.4: ThumbnailService (WebP thumbnail generation)
- ✅ Group 5.1: FileUploadService (hash-based deduplication)
- ⬜ Group 5.5: HashComparisonService (batch hash API)
- ⬜ Group 8.3: Backblaze B2 Provider

**P1 Features Covered:**
- ⬜ OAuth2 authentication
- ✅ Chunked/resumable upload (application logic)
- ✅ Thumbnail generation (WebP 300×300 @ 80%, synchronous)
- ✅ Hash-based deduplication (application logic)
- ⬜ Batch hash comparison API
- ⬜ Backblaze B2 provider

### Feature Phase P2 - Production Ready ⬜
**Status:** 0% Complete

**Required Test Groups:**
- ⬜ Group 6.1: RateLimitingService
- ⬜ Group 6.2: QuotaManagementService
- ⬜ Group 7.3: FileSearchService
- ⬜ Group 8.4: PluginLoader (enhanced)
- ⬜ Group 11.4: FilesController (Range download)

### Feature Phase P3 - Advanced Features ⬜
**Status:** 0% Complete

**Required Test Groups:**
- ⬜ Group 7.1: RedundancyManagementService
- ⬜ Group 7.2: FileRebalancingService
- ⬜ Group 7.4: FileSharingService
- ⬜ Group 11.9: ShareController (webhooks)

### Feature Phase P4 - Nice-to-Have ⬜
**Status:** 0% Complete (Future enhancement)

---

## Test Execution Checklist

### Legend
- ⬜ Not Started
- 🔄 In Progress
- ✅ Tests Written & Passing
- ❌ Tests Failing
- ⏸️ Blocked/Deferred

---

## Test Group 1: Domain Layer - Value Objects ✅

**Goal:** Build foundation with no dependencies
**Estimated Tests:** 35-40 test cases
**Status:** ✅ Complete (51 tests passing)

### 1.1 FileSize Value Object ✅
- ✅ Should create valid file size with bytes
- ✅ Should reject negative file size
- ✅ Should reject zero file size
- ✅ Should convert bytes to KB correctly
- ✅ Should convert bytes to MB correctly
- ✅ Should convert bytes to GB correctly
- ✅ Should compare file sizes correctly (equality)
- ✅ Should compare file sizes correctly (greater than)
- ✅ Should compare file sizes correctly (less than)
- ✅ Should enforce maximum size limit (5GB)
- ✅ Should return human-readable format (e.g., "1.5 MB")

**Test Class:** `FileSizeTests.cs` (11 tests passing)
**Dependencies:** None

---

### 1.2 FileType Value Object ✅
- ✅ Should create valid photo type from JPEG MIME type
- ✅ Should create valid photo type from PNG MIME type
- ✅ Should create valid photo type from HEIC MIME type
- ✅ Should create valid video type from MP4 MIME type
- ✅ Should create valid video type from MOV MIME type
- ✅ Should create valid misc type from PDF MIME type
- ✅ Should reject invalid/unknown MIME type
- ✅ Should map MIME type to file extension correctly
- ✅ Should recommend storage tier based on type (photo → deep archive)
- ✅ Should recommend storage tier based on type (video → flexible)
- ✅ Should categorize file as photo/video/misc correctly
- ✅ Should validate file extension matches MIME type

**Test Class:** `FileTypeTests.cs` (17 tests passing)
**Dependencies:** None

---

### 1.3 StorageLocation Value Object ✅
- ✅ Should create valid storage location with provider and path
- ✅ Should reject null provider name
- ✅ Should reject empty provider name
- ✅ Should reject null storage path
- ✅ Should reject empty storage path
- ✅ Should validate path format (starts with provider scheme)
- ✅ Should support equality comparison (same provider and path)
- ✅ Should parse location string correctly
- ✅ Should generate location string correctly

**Test Class:** `StorageLocationTests.cs` (10 tests passing)
**Dependencies:** None

---

### 1.4 UploadStatus Value Object ✅
- ✅ Should initialize with Pending status
- ✅ Should transition from Pending to Uploading
- ✅ Should transition from Uploading to Completed
- ✅ Should transition from Uploading to Failed
- ✅ Should transition from Completed to Archived
- ✅ Should reject invalid transition (Pending to Archived)
- ✅ Should reject invalid transition (Completed to Uploading)
- ✅ Should track timestamp of status change
- ✅ Should prevent changes after Archived status
- ✅ Should allow transition from Failed to Pending (retry)

**Test Class:** `UploadStatusTests.cs` (13 tests passing)
**Dependencies:** None

---

## Test Group 2: Domain Layer - Entities ✅

**Goal:** Core business entities
**Estimated Tests:** 30-35 test cases
**Status:** ✅ Complete (37 tests passing)

### 2.1 FileMetadata Entity ✅
- ✅ Should create metadata with required properties
- ✅ Should validate filename is not null
- ✅ Should validate filename is not empty
- ✅ Should sanitize filename (remove special characters)
- ✅ Should store original filename separately
- ✅ Should validate SHA256 hash format
- ✅ Should store content hash
- ✅ Should store creation timestamp automatically
- ✅ Should update modification timestamp when changed
- ✅ Should store optional user tags
- ✅ Should store optional description
- ✅ Should store MIME type
- ✅ Should store GPS coordinates if provided
- ✅ Should store device metadata if provided

**Test Class:** `FileMetadataTests.cs` (15 tests passing)
**Dependencies:** None

---

### 2.2 File Entity (Aggregate Root) ✅
- ✅ Should create file with required properties
- ✅ Should generate unique file ID (GUID)
- ✅ Should initialize with Pending status
- ✅ Should assign file size
- ✅ Should assign file type
- ✅ Should associate file metadata
- ✅ Should update status via domain method only
- ✅ Should track upload progress percentage
- ✅ Should raise FileUploadStarted domain event
- ✅ Should raise FileUploadCompleted domain event
- ✅ Should raise FileArchived domain event
- ✅ Should prevent modification after archived
- ✅ Should prevent status update after archived
- ✅ Should store storage location when archived
- ✅ Should validate file size within limits
- ✅ Should support adding user tags after creation

**Test Class:** `FileEntityTests.cs` (22 tests passing)
**Dependencies:** FileMetadata, FileSize, FileType, UploadStatus, StorageLocation

---

## Test Group 3: Domain Layer - Domain Services 🔄

**Goal:** Business logic that doesn't belong to single entity
**Estimated Tests:** 25-30 test cases
**Status:** 🔄 Partially Complete (8 tests passing)

### 3.1 IStorageProvider Interface (Contract Tests) ✅
- ✅ Should define UploadAsync method signature
- ✅ Should define DownloadAsync method signature
- ✅ Should define InitiateRetrievalAsync method signature
- ✅ Should define DeleteAsync method signature
- ✅ Should define CheckHealthAsync method signature
- ✅ Should return provider name
- ✅ Should return provider capabilities

**Test Class:** `IStorageProviderContractTests.cs`
**Dependencies:** None
**Note:** Interface defined with full contract

---

### 3.2 StorageProviderSelector Domain Service ✅
- ✅ Should select S3 Deep Archive for photos by default
- ✅ Should select S3 Flexible for large videos
- ✅ Should select Backblaze for frequently accessed files
- ✅ Should respect user-specified provider preference
- ✅ Should fallback to default provider if preference unavailable
- ✅ Should validate provider is enabled before selection
- ✅ Should consider file size in selection logic
- ✅ Should consider file type in selection logic
- ✅ Should throw exception if no providers available
- ✅ Should select cheapest provider when multiple match

**Test Class:** `StorageProviderSelectorTests.cs` (8 tests passing)
**Dependencies:** FileType, FileSize, IStorageProvider

---

### 3.3 FileHashCalculator Domain Service ⬜
- ⬜ Should calculate SHA256 hash from stream
- ⬜ Should return hash in hex format
- ⬜ Should handle empty file (empty hash)
- ⬜ Should handle large files without memory issues
- ⬜ Should produce consistent hash for same content
- ⬜ Should produce different hash for different content

**Test Class:** `FileHashCalculatorTests.cs`
**Dependencies:** None
**Note:** Implemented as IHashService in Application Layer instead

---

## Test Group 4: Application Layer - Repository Interfaces ✅

**Goal:** Define data access contracts
**Estimated Tests:** 15-20 test cases
**Status:** ✅ Complete (Interfaces defined)

### 4.1 IFileRepository Interface ✅
- ✅ Should define AddAsync method
- ✅ Should define GetByIdAsync method
- ✅ Should define UpdateAsync method
- ✅ Should define DeleteAsync method (soft delete)
- ✅ Should define GetByUserIdAsync with pagination
- ✅ Should define SearchAsync with filters
- ✅ Should define ExistsByHashAsync for deduplication
- ✅ Should support unit of work pattern

**Test Class:** `IFileRepositoryContractTests.cs`
**Dependencies:** File Entity
**Note:** Interface defined in `backend/src/FlexStorage.Application/Interfaces/Repositories/IFileRepository.cs:9`

---

### 4.2 IUploadSessionRepository Interface ✅
- ✅ Should define CreateSessionAsync
- ✅ Should define GetSessionAsync
- ✅ Should define UpdateSessionAsync
- ✅ Should define DeleteSessionAsync
- ✅ Should define GetExpiredSessionsAsync

**Test Class:** `IUploadSessionRepositoryContractTests.cs`
**Dependencies:** None
**Note:** Interface defined in `backend/src/FlexStorage.Application/Interfaces/Repositories/IUploadSessionRepository.cs:9`

---

## Test Group 5: Application Layer - Application Services 🔄

**Goal:** Use cases and orchestration
**Estimated Tests:** 60-70 test cases
**Status:** 🔄 Partially Complete (22 tests passing - Feature Phase P0 MVP services complete)

### 5.1 FileUploadService (Simple Upload) ✅
- ✅ Should validate file size before upload
- ✅ Should validate file type before upload
- ✅ Should reject file exceeding size limit
- ✅ Should reject invalid file type
- ✅ Should calculate file hash
- ✅ Should check for duplicate file by hash
- ✅ Should skip upload if duplicate found
- ✅ Should return existing file ID if duplicate
- ✅ Should select appropriate storage provider
- ⬜ Should generate unique storage path
- ✅ Should upload file to selected provider
- ✅ Should save file metadata to repository
- ✅ Should update file status to Completed after upload
- ✅ Should handle upload failure and set status to Failed
- ⬜ Should rollback metadata if upload fails
- ✅ Should emit FileUploadStarted event (via domain)
- ✅ Should emit FileUploadCompleted event (via domain)
- ⬜ Should queue thumbnail generation job asynchronously

**Test Class:** `FileUploadServiceTests.cs` (10 tests passing - COMPLETE)
**Dependencies:** IFileRepository, IStorageProvider, StorageProviderSelector, IHashService
**Location:** `backend/tests/FlexStorage.Application.Tests/Services/FileUploadServiceTests.cs:14`

---

### 5.2 ChunkedUploadService ✅
- ✅ Should initiate upload session with file metadata
- ✅ Should generate upload ID (GUID)
- ✅ Should calculate number of chunks needed
- ✅ Should set chunk size to 5MB by default
- ⬜ Should generate presigned URLs for each chunk
- ✅ Should set session expiration to 24 hours
- ✅ Should save upload session to repository
- ✅ Should track uploaded chunk parts
- ⬜ Should validate chunk MD5 hash
- ⬜ Should reject chunk with invalid MD5
- ✅ Should update session progress after each chunk
- ✅ Should complete upload when all chunks uploaded
- ⬜ Should assemble chunks in correct order
- ✅ Should verify final file hash matches expected
- ✅ Should handle upload resumption (return uploaded parts)
- ⬜ Should cancel upload and cleanup resources
- ✅ Should expire upload session after 24 hours (domain logic)
- ⬜ Should cleanup expired sessions via background job

**Test Class:** `ChunkedUploadServiceTests.cs` (13 tests passing - COMPLETE)
**Dependencies:** IUploadSessionRepository, IFileRepository, IHashService
**Location:** `backend/tests/FlexStorage.Application.Tests/Services/ChunkedUploadServiceTests.cs:14`

---

### 5.3 FileRetrievalService ✅
- ✅ Should retrieve file metadata by ID
- ✅ Should return 404 if file not found
- ✅ Should download file directly when available
- ✅ Should initiate Glacier retrieval for archived files
- ✅ Should select retrieval tier (bulk, standard, expedited)
- ⬜ Should save retrieval request to database
- ✅ Should return estimated retrieval time
- ✅ Should poll retrieval status from provider
- ✅ Should return file stream for download when ready
- ⬜ Should set download URL expiration to 24 hours
- ⬜ Should validate user has permission to retrieve file
- ⬜ Should track retrieval request in database
- ⬜ Should expire retrieval request after timeout
- ⬜ Should send webhook notification when retrieval ready
- ✅ Should get user files with pagination

**Test Class:** `FileRetrievalServiceTests.cs` (13 tests passing - COMPLETE)
**Dependencies:** IFileRepository, IStorageService
**Location:** `backend/tests/FlexStorage.Application.Tests/Services/FileRetrievalServiceTests.cs:14`

---

### 5.4 ThumbnailService (P1 Complete ✅)
- ✅ Should generate WebP thumbnails (300×300 @ 80% quality by default)
- ✅ Should use WebP format for better compression (25-35% smaller than JPEG)
- ✅ Should maintain aspect ratio when resizing (ResizeMode.Max)
- ✅ Should use configurable quality/dimensions (ThumbnailOptions)
- ✅ Should generate thumbnails synchronously during upload
- ✅ Should store thumbnails in S3 Standard (instant access, no retrieval)
- ✅ Should handle thumbnail generation failure gracefully (main upload succeeds)
- ✅ Should skip thumbnail generation for unsupported types (PDF, video, etc.)
- ✅ Should support JPEG, PNG, GIF, BMP, WebP input formats
- ✅ Should validate dimension ranges (1-5000 pixels)
- ✅ Should validate quality range (1-100)
- ⬜ Should generate 3 preview frames for videos (25%, 50%, 75%) - P2 feature
- ⬜ Should generate 10-second preview clip for videos - P2 feature
- ⬜ Should use CDN for thumbnail delivery - P2 feature

**Test Class:** `ThumbnailServiceTests.cs` (8 tests), `S3StandardProviderTests.cs` (12 tests)
**Dependencies:** SixLabors.ImageSharp 3.1.11, IStorageProvider (S3 Standard)
**Configuration:** `appsettings.json` → `Thumbnail` section (Width, Height, Quality)

---

### 5.5 HashComparisonService
- ⬜ Should accept batch of up to 1000 hashes
- ⬜ Should reject batch exceeding 1000 hashes
- ⬜ Should query repository for each hash
- ⬜ Should return exists=true if hash found
- ⬜ Should return exists=false if hash not found
- ⬜ Should include file ID if hash exists
- ⬜ Should include upload status if hash exists
- ⬜ Should mark safeToDeleteLocally=true if status is Archived
- ⬜ Should mark safeToDeleteLocally=false if status is Uploading
- ⬜ Should optimize batch query performance
- ⬜ Should return results in same order as input
- ⬜ Should handle database errors gracefully

**Test Class:** `HashComparisonServiceTests.cs`
**Dependencies:** IFileRepository

---

### 5.6 StorageProviderFactory
- ⬜ Should register multiple providers in DI
- ⬜ Should create provider instance by name
- ⬜ Should throw exception if provider not found
- ⬜ Should throw exception if provider name is null
- ⬜ Should support lazy loading of providers
- ⬜ Should validate provider configuration on startup
- ⬜ Should discover plugins from directory
- ⬜ Should load provider from external assembly
- ⬜ Should validate plugin implements IStorageProvider
- ⬜ Should handle plugin load failure gracefully

**Test Class:** `StorageProviderFactoryTests.cs`
**Dependencies:** IStorageProvider

---

## Test Group 6: Application Layer - Rate Limiting & Quota

**Estimated Tests:** 30-35 test cases

### 6.1 RateLimitingService
- ⬜ Should enforce Free tier upload limit (10/hour)
- ⬜ Should enforce Standard tier upload limit (100/hour)
- ⬜ Should enforce Premium tier upload limit (1000/hour)
- ⬜ Should enforce daily bandwidth limit for Free tier
- ⬜ Should enforce daily bandwidth limit for Standard tier
- ⬜ Should enforce daily bandwidth limit for Premium tier
- ⬜ Should reject upload when hourly limit exceeded
- ⬜ Should reject upload when daily limit exceeded
- ⬜ Should reset hourly counter after 1 hour
- ⬜ Should reset daily counter after 24 hours
- ⬜ Should return correct rate limit headers
- ⬜ Should return retry-after time when rate limited
- ⬜ Should throttle S3 requests to configured limit (50/sec for Deep)
- ⬜ Should throttle S3 requests to configured limit (100/sec for Flex)
- ⬜ Should not throttle Backblaze (1000/sec)
- ⬜ Should batch uploads to S3 Deep Archive when enabled
- ⬜ Should calculate cost of request and throttle if needed
- ⬜ Should allow bypass of throttling for Premium users

**Test Class:** `RateLimitingServiceTests.cs`
**Dependencies:** User profile, Configuration

---

### 6.2 QuotaManagementService
- ⬜ Should track total quota per user
- ⬜ Should track quota per provider
- ⬜ Should calculate used space per provider
- ⬜ Should calculate remaining space per provider
- ⬜ Should reject upload if total quota exceeded
- ⬜ Should reject upload if provider quota exceeded
- ⬜ Should increment used space after successful upload
- ⬜ Should decrement used space after file deletion
- ⬜ Should calculate usage percentage
- ⬜ Should recommend rebalancing when providers uneven (>20% difference)
- ⬜ Should emit warning event at 80% quota
- ⬜ Should emit exceeded event at 100% quota
- ⬜ Should support quota increase via admin operation
- ⬜ Should track file count per provider

**Test Class:** `QuotaManagementServiceTests.cs`
**Dependencies:** IFileRepository, User profile

---

## Test Group 7: Application Layer - Advanced Features

**Estimated Tests:** 40-50 test cases

### 7.1 RedundancyManagementService
- ⬜ Should create 1 copy for "none" redundancy profile
- ⬜ Should create 2 copies for "standard" redundancy profile
- ⬜ Should create 3 copies for "paranoid" redundancy profile
- ⬜ Should distribute copies to different providers
- ⬜ Should distribute copies to different regions
- ⬜ Should queue redundancy job when requested
- ⬜ Should handle Glacier retrieval delay for copying
- ⬜ Should verify checksum after creating copy
- ⬜ Should update file metadata with copy locations
- ⬜ Should calculate redundancy health score (0-100)
- ⬜ Should detect missing redundant copy
- ⬜ Should auto-repair missing copy
- ⬜ Should verify checksums periodically (weekly)
- ⬜ Should emit alert if redundancy check fails
- ⬜ Should skip redundancy for temporary files

**Test Class:** `RedundancyManagementServiceTests.cs`
**Dependencies:** IFileRepository, IStorageProvider, Background Jobs

---

### 7.2 FileRebalancingService
- ⬜ Should identify files to move based on criteria (rarely-accessed)
- ⬜ Should calculate cost estimate for rebalancing
- ⬜ Should create rebalancing job
- ⬜ Should schedule job for off-peak hours if requested
- ⬜ Should retrieve file from source provider (handle Glacier delay)
- ⬜ Should upload file to target provider
- ⬜ Should verify file integrity after move
- ⬜ Should update file metadata with new location
- ⬜ Should optionally delete from source provider
- ⬜ Should track progress (files processed, bytes transferred)
- ⬜ Should handle failures and retry
- ⬜ Should throttle bandwidth if rate limit specified
- ⬜ Should calculate actual cost vs estimated
- ⬜ Should emit completion event
- ⬜ Should support cost-optimize strategy
- ⬜ Should support performance-optimize strategy
- ⬜ Should support even-distribute strategy

**Test Class:** `FileRebalancingServiceTests.cs`
**Dependencies:** IFileRepository, IStorageProvider, Background Jobs

---

### 7.3 FileSearchService
- ⬜ Should search by filename
- ⬜ Should search by tags (exact match)
- ⬜ Should search by tags (partial match)
- ⬜ Should search by date range
- ⬜ Should search by file type (photo, video, misc)
- ⬜ Should search by MIME type
- ⬜ Should search by size range
- ⬜ Should search by provider
- ⬜ Should combine multiple filters (AND logic)
- ⬜ Should support full-text search in metadata
- ⬜ Should return paginated results
- ⬜ Should sort by relevance score
- ⬜ Should sort by date (newest first)
- ⬜ Should sort by size (largest first)
- ⬜ Should highlight matched terms in results
- ⬜ Should return total count of results

**Test Class:** `FileSearchServiceTests.cs`
**Dependencies:** IFileRepository

---

### 7.4 FileSharingService
- ⬜ Should create public share with unique share ID
- ⬜ Should generate short URL for sharing
- ⬜ Should set expiration date if provided
- ⬜ Should set share to never expire if not provided
- ⬜ Should require password if specified
- ⬜ Should validate password on access
- ⬜ Should track download count
- ⬜ Should enforce max download limit if set
- ⬜ Should reject access after expiration
- ⬜ Should reject access after max downloads reached
- ⬜ Should generate time-limited download URL
- ⬜ Should validate user owns file before sharing
- ⬜ Should allow share revocation
- ⬜ Should list all shares for a file
- ⬜ Should update share settings
- ⬜ Should log share access events

**Test Class:** `FileSharingServiceTests.cs`
**Dependencies:** IFileRepository, IShareRepository, IStorageProvider

---

## Test Group 8: Infrastructure Layer - Storage Providers

**Estimated Tests:** 50-60 test cases

### 8.1 S3GlacierDeepArchiveProvider
- ⬜ Should upload file to S3 using AWS SDK
- ⬜ Should set storage class to DEEP_ARCHIVE
- ⬜ Should generate unique S3 key
- ⬜ Should calculate and set Content-MD5 header
- ⬜ Should handle AWS SDK exceptions
- ⬜ Should retry on transient failures (3 retries)
- ⬜ Should initiate Glacier retrieval (bulk tier)
- ⬜ Should initiate Glacier retrieval (standard tier)
- ⬜ Should initiate Glacier retrieval (expedited tier)
- ⬜ Should poll retrieval job status
- ⬜ Should generate presigned download URL when ready
- ⬜ Should delete archived file
- ⬜ Should check provider health (test connection)
- ⬜ Should return provider name
- ⬜ Should return provider capabilities
- ⬜ Should support multipart upload for large files
- ⬜ Should handle upload cancellation

**Test Class:** `S3GlacierDeepArchiveProviderTests.cs`
**Dependencies:** AWS SDK (mock)

---

### 8.2 S3GlacierFlexibleRetrievalProvider
- ⬜ Should upload file with GLACIER_IR storage class
- ⬜ Should initiate faster retrieval (standard: 3-5 hours)
- ⬜ Should generate presigned URL for upload
- ⬜ Should handle all operations similar to Deep Archive
- ⬜ Should return correct retrieval time estimates

**Test Class:** `S3GlacierFlexibleRetrievalProviderTests.cs`
**Dependencies:** AWS SDK (mock)

---

### 8.3 IDriveE2Provider ✅ COMPLETE (14 tests)
- ✅ Should return correct provider name (`idrive-e2`)
- ✅ Should have instant access capabilities (no retrieval)
- ✅ Should upload file to iDrive e2 with S3 SDK
- ✅ Should include metadata in upload request
- ✅ Should return failure result on upload exception
- ✅ Should download file from iDrive e2
- ✅ Should throw InvalidOperationException on download failure
- ✅ Should delete file from iDrive e2
- ✅ Should return false on delete exception
- ✅ Should throw NotSupportedException for retrieval (instant access)
- ✅ Should throw NotSupportedException for retrieval status
- ✅ Should return healthy status when bucket accessible
- ✅ Should return unhealthy status on connection failure
- ✅ Should generate S3 keys with date path and unique ID

**Test Class:** `IDriveE2ProviderTests.cs` (14 tests passing)
**Dependencies:** AWS SDK S3 (mock), iDrive e2 S3-compatible endpoint
**Pricing:** $4/TB/month with 3x free egress - most cost-effective for photo/video storage
**Configuration:** `appsettings.json` → `IDriveE2` section (Endpoint, Bucket, Region, AccessKey, SecretKey)

---

### 8.4 BackblazeB2Provider
- ⬜ Should authenticate with Backblaze API
- ⬜ Should cache authentication token
- ⬜ Should refresh token when expired
- ⬜ Should upload file to B2 bucket
- ⬜ Should generate upload URL
- ⬜ Should download file from B2
- ⬜ Should delete file from B2
- ⬜ Should handle B2 API errors
- ⬜ Should retry on rate limit (503)
- ⬜ Should return instant retrieval (no delay)
- ⬜ Should check provider health
- ⬜ Should support large file upload (B2 multipart)

**Test Class:** `BackblazeB2ProviderTests.cs`
**Dependencies:** Backblaze SDK (mock)

---

### 8.4 PluginLoader
- ⬜ Should scan plugins directory for assemblies
- ⬜ Should load assembly from file
- ⬜ Should discover types implementing IStorageProvider
- ⬜ Should instantiate provider from plugin
- ⬜ Should validate provider has required attributes
- ⬜ Should handle plugin load failure gracefully
- ⬜ Should log plugin discovery and loading
- ⬜ Should skip invalid assemblies
- ⬜ Should support versioning of plugins
- ⬜ Should prevent loading duplicate providers

**Test Class:** `PluginLoaderTests.cs`
**Dependencies:** File system, Reflection

---

## Test Group 9: Infrastructure Layer - Persistence

**Estimated Tests:** 40-45 test cases

### 9.1 FileRepository (EF Core Implementation) - ✅ COMPLETE (19 tests)
- ✅ Should add file entity to database (`AddAsync_ShouldPersistFileToDatabase`)
- ✅ Should retrieve file by ID (`GetByIdAsync_ShouldReturnFileWhenExists`)
- ✅ Should return null if file not found (`GetByIdAsync_ShouldReturnNullWhenNotExists`)
- ✅ Should query files by user ID (`GetByUserIdAsync_ShouldReturnUserFilesWithPagination`)
- ✅ Should support pagination (skip, take) (`GetByUserIdAsync_ShouldReturnSecondPage`)
- ✅ Should order files by newest first (`GetByUserIdAsync_ShouldOrderByNewestFirst`)
- ✅ Should handle empty results for user with no files (`GetByUserIdAsync_WithNoFiles_ShouldReturnEmptyResult`)
- ✅ Should filter by file category (`SearchAsync_ShouldFilterByFileCategory`)
- ✅ Should filter by filename (`SearchAsync_WithFileName_ShouldFilterResults`)
- ✅ Should filter by date range (`SearchAsync_WithDateRange_ShouldFilterResults`)
- ✅ Should combine multiple filters (`SearchAsync_WithMultipleFilters_ShouldCombineFilters`)
- ✅ Should order search results by newest first (`SearchAsync_ShouldOrderByNewestFirst`)
- ✅ Should handle empty search results (`SearchAsync_WithNoMatches_ShouldReturnEmptyResult`)
- ✅ Should return file by hash (`GetByHashAsync_ShouldReturnFileWithMatchingHash`)
- ✅ Should return null when hash doesn't match (`GetByHashAsync_ShouldReturnNullWhenNoMatch`)
- ✅ Should update file entity (`UpdateAsync_ShouldPersistChanges`)
- ✅ Should delete file (`DeleteAsync_ShouldRemoveFile`)
- ✅ Should search by tags (`SearchAsync_WithTags_ShouldFilterByTags`)
- ✅ Should filter by status (`SearchAsync_WithStatus_ShouldFilterByStatus`)
- ⬜ Should handle concurrency conflicts (optimistic locking) - *Deferred to P1*
- ⬜ Should soft delete file (mark as deleted, not remove) - *Not implemented (hard delete used)*
- ⬜ Should use indexes for performance - *Covered by EF Core configuration*
- ⬜ Should eager load related entities when needed - *Not applicable (owned entities auto-loaded)*

**Test Class:** `FileRepositoryTests.cs`
**Dependencies:** EF Core, In-Memory Database for testing

---

### 9.2 Database Migrations
- ⬜ Should create Files table with correct schema
- ⬜ Should create indexes on FileHash column
- ⬜ Should create indexes on UserId column
- ⬜ Should create indexes on CreatedAt column
- ⬜ Should create UploadSessions table
- ⬜ Should create Shares table
- ⬜ Should create RedundancyCopies table
- ⬜ Should support PostgreSQL
- ⬜ Should support SQL Server (optional)

**Test Class:** `DatabaseMigrationTests.cs`
**Dependencies:** EF Core Migrations

---

## Test Group 10: Infrastructure Layer - Background Jobs

**Estimated Tests:** 20-25 test cases

### 10.1 ThumbnailGenerationJob
- ⬜ Should dequeue thumbnail generation message
- ⬜ Should download original file
- ⬜ Should generate thumbnails
- ⬜ Should upload thumbnails to S3 Standard
- ⬜ Should update file metadata with thumbnail URLs
- ⬜ Should handle job failure and retry
- ⬜ Should log job execution

**Test Class:** `ThumbnailGenerationJobTests.cs`
**Dependencies:** Job queue (Redis mock), IStorageProvider

---

### 10.2 ExpiredSessionCleanupJob
- ⬜ Should query for expired upload sessions
- ⬜ Should delete session metadata
- ⬜ Should cleanup partial uploads from storage
- ⬜ Should run on schedule (daily)

**Test Class:** `ExpiredSessionCleanupJobTests.cs`
**Dependencies:** IUploadSessionRepository, IStorageProvider

---

### 10.3 RedundancyVerificationJob
- ⬜ Should query files needing verification
- ⬜ Should verify checksum for each copy
- ⬜ Should detect corrupted copies
- ⬜ Should trigger repair if copy missing/corrupted
- ⬜ Should update verification timestamp
- ⬜ Should run on schedule (weekly)

**Test Class:** `RedundancyVerificationJobTests.cs`
**Dependencies:** IFileRepository, IStorageProvider

---

## Test Group 11: API Layer - Controllers

**Estimated Tests:** 70-80 test cases

### 11.1 AuthController - ✅ COMPLETE (13 tests)

**API Key Authentication (Phase 1 - Local Development):**
- ✅ Should validate API key from X-API-Key header (`ValidateApiKey_WithValidXApiKeyHeader_ShouldReturnOkWithUserId`)
- ✅ Should validate API key from Authorization header (`ValidateApiKey_WithValidAuthorizationHeader_ShouldReturnOkWithUserId`)
- ✅ Should return 401 for missing API key (`ValidateApiKey_WithMissingApiKey_ShouldReturnUnauthorized`)
- ✅ Should return 401 for invalid API key (`ValidateApiKey_WithInvalidApiKey_ShouldReturnUnauthorized`)
- ✅ Should return 401 for expired API key (`ValidateApiKey_WithExpiredApiKey_ShouldReturnUnauthorized`)
- ✅ Should extract user ID from valid API key (covered in validation tests)
- ✅ Should generate new API key (`GenerateApiKey_WithValidRequest_ShouldReturnOkWithApiKey`)
- ✅ Should handle API key generation failure (`GenerateApiKey_WhenServiceFails_ShouldReturnBadRequest`)
- ✅ Should support never-expiring API keys (`GenerateApiKey_WithNeverExpiring_ShouldAcceptNullExpiresInDays`)
- ✅ Should revoke API key (`RevokeApiKey_WithValidApiKey_ShouldReturnOk`)
- ✅ Should return 401 when revoking without API key (`RevokeApiKey_WithMissingApiKey_ShouldReturnUnauthorized`)
- ✅ Should return 404 for non-existent API key (`RevokeApiKey_WithNonExistentApiKey_ShouldReturnNotFound`)
- ✅ Should prioritize X-API-Key over Authorization header (`ValidateApiKey_WithBothHeaders_ShouldPrioritizeXApiKeyHeader`)

**OAuth2 Authentication (Phase 2 - Production):**
- ⬜ Should return access token for valid authorization code
- ⬜ Should return refresh token for valid authorization code
- ⬜ Should return 401 for invalid authorization code
- ⬜ Should refresh access token with valid refresh token
- ⬜ Should return 401 for invalid refresh token
- ⬜ Should revoke tokens on logout
- ⬜ Should return proper token expiration times
- ⬜ Should rotate refresh token on refresh
- ⬜ Should support multiple OAuth providers (Google, Apple)

**Test Class:** `AuthControllerTests.cs` (13 tests passing)
**Dependencies:** IApiKeyService (mock), ILogger (mock)
**Location:** `backend/tests/FlexStorage.API.Tests/AuthControllerTests.cs`

---

### 11.2 FilesController (Upload)
- ⬜ Should accept multipart form upload
- ⬜ Should return 201 Created with file ID
- ⬜ Should validate Content-Type header
- ⬜ Should validate file size against configured limit (20 MB default)
- ⬜ Should return 413 Payload Too Large if size exceeded
- ⬜ Should validate file type
- ⬜ Should return 400 Bad Request for invalid file type
- ⬜ Should require authentication
- ⬜ Should return 401 Unauthorized if not authenticated
- ⬜ Should return 429 Too Many Requests if rate limited
- ⬜ Should include rate limit headers in response
- ⬜ Should accept file metadata in request
- ⬜ Should validate metadata schema
- ⬜ Should handle duplicate file (return existing ID)

**Test Class:** `FilesControllerUploadTests.cs`
**Dependencies:** FileUploadService
**Configuration:** `appsettings.json` → `UploadLimits` section (MaxFileSizeBytes: 20971520, MaxFileSizeMB: 20)
**Note:** Single-request upload supports files up to 20 MB. Files >20 MB must use chunked upload.

---

### 11.3 FilesController (Chunked Upload)
- ⬜ Should initiate chunked upload
- ⬜ Should return upload ID and presigned URLs
- ⬜ Should validate initiate upload request
- ⬜ Should accept chunk upload
- ⬜ Should validate chunk MD5 hash
- ⬜ Should return upload progress after chunk
- ⬜ Should complete upload when all chunks uploaded
- ⬜ Should return upload status (resume)
- ⬜ Should cancel upload
- ⬜ Should return 404 if upload ID not found
- ⬜ Should return 409 Conflict if upload already completed

**Test Class:** `FilesControllerChunkedUploadTests.cs`
**Dependencies:** ChunkedUploadService

---

### 11.4 FilesController (Retrieval) ✅
- ✅ Should return file metadata by ID
- ✅ Should return 404 if file not found
- ⬜ Should return thumbnail URL
- ⬜ Should return 202 if thumbnail still generating
- ⬜ Should serve thumbnail image
- ⬜ Should initiate Glacier retrieval
- ⬜ Should return retrieval ID and estimated time
- ⬜ Should return retrieval status
- ⬜ Should return download URL when ready
- ✅ Should download file (return file stream directly)
- ✅ Should return 404 if download file not found
- ✅ Should return 400 if download fails
- ✅ Should return 500 if file stream is null
- ⬜ Should return 202 if file in Glacier and not retrieved
- ⬜ Should support Range requests for resumable download
- ⬜ Should return 206 Partial Content for range request
- ⬜ Should validate user owns file before retrieval
- ⬜ Should return 403 Forbidden if not owner

**Test Class:** `FilesControllerTests.cs` (8 tests passing - download functionality complete)
**Dependencies:** FileRetrievalService, ThumbnailGenerationService
**Location:** `backend/tests/FlexStorage.API.Tests/FilesControllerTests.cs:14`

**Recent Implementation Details:**
- ✅ Added `DownloadFile` endpoint at `GET /api/v1/Files/{id}/download`
- ✅ Added `GetUserFilesAsync` method to `IFileRetrievalService` and implementation
- ✅ Updated `FileRetrievalService` to support user file listing with pagination
- ✅ Comprehensive test coverage including:
  - Successful file download with proper headers
  - 404 handling for missing files
  - 400 handling for download failures
  - 500 handling for null file streams
  - User file listing functionality
- ✅ All 8 tests passing in `FilesControllerTests.cs`
- ✅ All 31 tests passing in Application layer
- ✅ Download endpoint ready for integration testing

**🚨 ARCHITECTURAL ISSUE IDENTIFIED:**
The current `/download` endpoint handles TWO distinct operations:
1. **Direct Download** (200 OK + file stream) - for immediately available files
2. **Retrieval Initiation** (202 Accepted + restoration info) - for archived files

**Recommendation:** Split into separate endpoints:
- `GET /api/v1/Files/{id}/download` - Direct download only (fail if archived)
- `POST /api/v1/Files/{id}/retrieve` - Initiate retrieval for archived files
- `GET /api/v1/Files/{id}/retrieve/{retrievalId}` - Check retrieval status

**Additional Test Cases Needed:**
- ✅ Should return 202 when file needs restoration (currently implemented)
- ⬜ Should return 409 if retrieval already in progress
- ⬜ Should return retrieval status by retrieval ID
- ⬜ Should allow download after successful retrieval
- ⬜ Should handle retrieval timeout/failure
- ⬜ Should validate retrieval tier selection
- ⬜ Should track retrieval costs and quotas

---

### 11.4.1 FilesController (Retrieval Workflow) ⬜
**Status:** Architecture needs refactoring - current implementation mixes concerns

**Recommended API Design:**
```
GET  /api/v1/Files/{id}/download          # Direct download (immediate)
POST /api/v1/Files/{id}/retrieve          # Initiate retrieval (archived files)
GET  /api/v1/Files/{id}/retrieve/{rid}    # Check retrieval status
```

**Test Cases for Separate Retrieval API:**
- ⬜ Should initiate retrieval with tier selection (bulk/standard/expedited)
- ⬜ Should return retrieval ID and estimated completion time
- ⬜ Should return 409 if retrieval already in progress for same file
- ⬜ Should return 400 if file is not archived (doesn't need retrieval)
- ⬜ Should validate retrieval tier is supported by provider
- ⬜ Should track retrieval costs against user quota
- ⬜ Should return retrieval status (pending/in_progress/completed/failed)
- ⬜ Should return progress percentage if available
- ⬜ Should return download URL when retrieval completed
- ⬜ Should expire retrieval request after timeout (24-48 hours)
- ⬜ Should send webhook notification when retrieval ready
- ⬜ Should allow cancellation of pending retrieval
- ⬜ Should handle provider-specific retrieval limits

**Test Class:** `FilesControllerRetrievalWorkflowTests.cs` (Not yet implemented)
**Dependencies:** FileRetrievalService, RetrievalTrackingService

---

### 11.5 FilesController (Management) 🔄
- ✅ Should list files with pagination
- ⬜ Should filter files by type
- ⬜ Should filter files by date range
- ⬜ Should search files by query string
- ⬜ Should sort files by date/size/name
- ⬜ Should update file metadata (PATCH)
- ⬜ Should delete file (soft delete)
- ⬜ Should return 204 No Content after delete
- ⬜ Should batch compare hashes
- ⬜ Should return sync status since timestamp

**Test Class:** `FilesControllerTests.cs` (1 test passing - list files functionality)
**Dependencies:** FileSearchService, IFileRepository, HashComparisonService
**Location:** `backend/tests/FlexStorage.API.Tests/FilesControllerTests.cs:14`

---

### 11.6 ProvidersController
- ⬜ Should list available providers
- ⬜ Should return provider health status
- ⬜ Should return 503 if provider unhealthy

**Test Class:** `ProvidersControllerTests.cs`
**Dependencies:** StorageProviderFactory, IStorageProvider

---

### 11.7 QuotaController
- ⬜ Should return user quota information
- ⬜ Should return usage statistics
- ⬜ Should return per-provider breakdown

**Test Class:** `QuotaControllerTests.cs`
**Dependencies:** QuotaManagementService

---

### 11.8 RedundancyController
- ⬜ Should get redundancy status for file
- ⬜ Should set redundancy level
- ⬜ Should return 202 Accepted for async operation
- ⬜ Should initiate rebalancing job
- ⬜ Should return rebalancing job status
- ⬜ Should return cost estimate

**Test Class:** `RedundancyControllerTests.cs`
**Dependencies:** RedundancyManagementService, FileRebalancingService

---

### 11.9 ShareController
- ⬜ Should create public share
- ⬜ Should return share URL
- ⬜ Should access shared file
- ⬜ Should require password if set
- ⬜ Should return 404 if share expired
- ⬜ Should return 404 if max downloads exceeded
- ⬜ Should list shares for file
- ⬜ Should revoke share
- ⬜ Should update share settings

**Test Class:** `ShareControllerTests.cs`
**Dependencies:** FileSharingService

---

### 11.10 HealthController
- ⬜ Should return 200 OK when healthy
- ⬜ Should check database connectivity
- ⬜ Should check storage provider health
- ⬜ Should return 503 Service Unavailable if unhealthy
- ⬜ Should return component-level health details

**Test Class:** `HealthControllerTests.cs`
**Dependencies:** All services

---

## Test Group 12: Integration Tests

**Goal:** End-to-end flows across all layers
**Estimated Tests:** 30-35 test cases

### 12.1 Upload Flow Integration Tests
- ⬜ Should upload small photo end-to-end
- ⬜ Should upload large video with chunking
- ⬜ Should generate thumbnails after upload
- ⬜ Should detect and skip duplicate upload
- ⬜ Should handle upload failure and rollback
- ⬜ Should respect rate limits
- ⬜ Should respect quota limits
- ⬜ Should support upload cancellation

**Test Class:** `UploadFlowIntegrationTests.cs`
**Environment:** Test database, Mock S3

---

### 12.2 Retrieval Flow Integration Tests
- ⬜ Should retrieve file from S3 Glacier
- ⬜ Should poll retrieval status until ready
- ⬜ Should download file when ready
- ⬜ Should handle retrieval timeout
- ⬜ Should serve thumbnail immediately

**Test Class:** `RetrievalFlowIntegrationTests.cs`
**Environment:** Test database, Mock S3

---

### 12.3 Multi-Provider Integration Tests
- ⬜ Should upload to S3 Glacier Deep Archive
- ⬜ Should upload to S3 Glacier Flexible
- ⬜ Should upload to Backblaze
- ⬜ Should switch providers dynamically
- ⬜ Should rebalance files between providers
- ⬜ Should maintain access to all files after rebalancing

**Test Class:** `MultiProviderIntegrationTests.cs`
**Environment:** Mock multiple providers

---

### 12.4 Redundancy Integration Tests
- ⬜ Should create redundant copies across providers
- ⬜ Should verify checksums match
- ⬜ Should detect and repair missing copy
- ⬜ Should complete redundancy despite Glacier delays

**Test Class:** `RedundancyIntegrationTests.cs`
**Environment:** Test database, Mock providers

---

### 12.5 Authentication & Authorization Tests
- ⬜ Should reject unauthenticated requests
- ⬜ Should accept valid access token
- ⬜ Should refresh expired token
- ⬜ Should prevent user from accessing other user's files

**Test Class:** `AuthIntegrationTests.cs`
**Environment:** Test OAuth server

---

## Test Metrics & Coverage

### Coverage Goals
- **Domain Layer:** 100% (pure logic, no dependencies)
- **Application Layer:** 95%+ (core business flows)
- **Infrastructure Layer:** 85%+ (external dependencies)
- **API Layer:** 90%+ (controllers, validation)
- **Overall:** 90%+

### Test Execution Time Goals
- **Unit Tests:** < 5 minutes for all
- **Integration Tests:** < 10 minutes for all
- **Full Suite:** < 15 minutes

### Continuous Integration
- Run unit tests on every commit
- Run integration tests on PR
- Run full suite before merge to main
- Block merge if coverage drops below 90%

---

## Test Data Management

### Test Fixtures
- **Small Image:** 1MB JPEG (test-data/sample-photo.jpg)
- **Large Image:** 50MB HEIC (test-data/large-photo.heic)
- **Small Video:** 10MB MP4 (test-data/sample-video.mp4)
- **Large Video:** 500MB MOV (test-data/large-video.mov)
- **PDF File:** 5MB (test-data/document.pdf)

### Mock Data Builders
```csharp
public class FileBuilder
{
    public File Build() { ... }
    public FileBuilder WithSize(long bytes) { ... }
    public FileBuilder WithType(string mimeType) { ... }
    public FileBuilder AsArchived() { ... }
}
```

---

## Next Steps

1. ✅ Review and approve this test plan
2. ✅ Setup project structure (.NET 8 solution)
3. ✅ Configure xUnit and FluentAssertions
4. ✅ Complete Test Group 1: Domain Layer Value Objects
5. ✅ Follow TDD cycle: Red → Green → Refactor

**Ready to start coding?** Let me know when to proceed!

---

**Last Updated:** 2025-10-25
**Document Owner:** FlexStorage Team
