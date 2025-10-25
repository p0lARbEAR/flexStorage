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

### TDD Cycle
```
RED → GREEN → REFACTOR
Write failing test → Make it pass → Improve code
```

---

## Test Execution Checklist

### Legend
- ⬜ Not Started
- 🔄 In Progress
- ✅ Tests Written & Passing
- ❌ Tests Failing
- ⏸️ Blocked/Deferred

---

## Phase 1: Domain Layer - Value Objects

**Goal:** Build foundation with no dependencies
**Estimated Tests:** 35-40 test cases

### 1.1 FileSize Value Object
- ⬜ Should create valid file size with bytes
- ⬜ Should reject negative file size
- ⬜ Should reject zero file size
- ⬜ Should convert bytes to KB correctly
- ⬜ Should convert bytes to MB correctly
- ⬜ Should convert bytes to GB correctly
- ⬜ Should compare file sizes correctly (equality)
- ⬜ Should compare file sizes correctly (greater than)
- ⬜ Should compare file sizes correctly (less than)
- ⬜ Should enforce maximum size limit (5GB)
- ⬜ Should return human-readable format (e.g., "1.5 MB")

**Test Class:** `FileSizeTests.cs`
**Dependencies:** None

---

### 1.2 FileType Value Object
- ⬜ Should create valid photo type from JPEG MIME type
- ⬜ Should create valid photo type from PNG MIME type
- ⬜ Should create valid photo type from HEIC MIME type
- ⬜ Should create valid video type from MP4 MIME type
- ⬜ Should create valid video type from MOV MIME type
- ⬜ Should create valid misc type from PDF MIME type
- ⬜ Should reject invalid/unknown MIME type
- ⬜ Should map MIME type to file extension correctly
- ⬜ Should recommend storage tier based on type (photo → deep archive)
- ⬜ Should recommend storage tier based on type (video → flexible)
- ⬜ Should categorize file as photo/video/misc correctly
- ⬜ Should validate file extension matches MIME type

**Test Class:** `FileTypeTests.cs`
**Dependencies:** None

---

### 1.3 StorageLocation Value Object
- ⬜ Should create valid storage location with provider and path
- ⬜ Should reject null provider name
- ⬜ Should reject empty provider name
- ⬜ Should reject null storage path
- ⬜ Should reject empty storage path
- ⬜ Should validate path format (starts with provider scheme)
- ⬜ Should support equality comparison (same provider and path)
- ⬜ Should parse location string correctly
- ⬜ Should generate location string correctly

**Test Class:** `StorageLocationTests.cs`
**Dependencies:** None

---

### 1.4 UploadStatus Value Object
- ⬜ Should initialize with Pending status
- ⬜ Should transition from Pending to Uploading
- ⬜ Should transition from Uploading to Completed
- ⬜ Should transition from Uploading to Failed
- ⬜ Should transition from Completed to Archived
- ⬜ Should reject invalid transition (Pending to Archived)
- ⬜ Should reject invalid transition (Completed to Uploading)
- ⬜ Should track timestamp of status change
- ⬜ Should prevent changes after Archived status
- ⬜ Should allow transition from Failed to Pending (retry)

**Test Class:** `UploadStatusTests.cs`
**Dependencies:** None

---

## Phase 2: Domain Layer - Entities

**Goal:** Core business entities
**Estimated Tests:** 30-35 test cases

### 2.1 FileMetadata Entity
- ⬜ Should create metadata with required properties
- ⬜ Should validate filename is not null
- ⬜ Should validate filename is not empty
- ⬜ Should sanitize filename (remove special characters)
- ⬜ Should store original filename separately
- ⬜ Should validate SHA256 hash format
- ⬜ Should store content hash
- ⬜ Should store creation timestamp automatically
- ⬜ Should update modification timestamp when changed
- ⬜ Should store optional user tags
- ⬜ Should store optional description
- ⬜ Should store MIME type
- ⬜ Should store GPS coordinates if provided
- ⬜ Should store device metadata if provided

**Test Class:** `FileMetadataTests.cs`
**Dependencies:** None

---

### 2.2 File Entity (Aggregate Root)
- ⬜ Should create file with required properties
- ⬜ Should generate unique file ID (GUID)
- ⬜ Should initialize with Pending status
- ⬜ Should assign file size
- ⬜ Should assign file type
- ⬜ Should associate file metadata
- ⬜ Should update status via domain method only
- ⬜ Should track upload progress percentage
- ⬜ Should raise FileUploadStarted domain event
- ⬜ Should raise FileUploadCompleted domain event
- ⬜ Should raise FileArchived domain event
- ⬜ Should prevent modification after archived
- ⬜ Should prevent status update after archived
- ⬜ Should store storage location when archived
- ⬜ Should validate file size within limits
- ⬜ Should support adding user tags after creation

**Test Class:** `FileEntityTests.cs`
**Dependencies:** FileMetadata, FileSize, FileType, UploadStatus, StorageLocation

---

## Phase 3: Domain Layer - Domain Services

**Goal:** Business logic that doesn't belong to single entity
**Estimated Tests:** 25-30 test cases

### 3.1 IStorageProvider Interface (Contract Tests)
- ⬜ Should define UploadAsync method signature
- ⬜ Should define DownloadAsync method signature
- ⬜ Should define InitiateRetrievalAsync method signature
- ⬜ Should define DeleteAsync method signature
- ⬜ Should define CheckHealthAsync method signature
- ⬜ Should return provider name
- ⬜ Should return provider capabilities

**Test Class:** `IStorageProviderContractTests.cs`
**Dependencies:** None
**Note:** Contract test, not implementation

---

### 3.2 StorageProviderSelector Domain Service
- ⬜ Should select S3 Deep Archive for photos by default
- ⬜ Should select S3 Flexible for large videos
- ⬜ Should select Backblaze for frequently accessed files
- ⬜ Should respect user-specified provider preference
- ⬜ Should fallback to default provider if preference unavailable
- ⬜ Should validate provider is enabled before selection
- ⬜ Should consider file size in selection logic
- ⬜ Should consider file type in selection logic
- ⬜ Should throw exception if no providers available
- ⬜ Should select cheapest provider when multiple match

**Test Class:** `StorageProviderSelectorTests.cs`
**Dependencies:** FileType, FileSize, IStorageProvider

---

### 3.3 FileHashCalculator Domain Service
- ⬜ Should calculate SHA256 hash from stream
- ⬜ Should return hash in hex format
- ⬜ Should handle empty file (empty hash)
- ⬜ Should handle large files without memory issues
- ⬜ Should produce consistent hash for same content
- ⬜ Should produce different hash for different content

**Test Class:** `FileHashCalculatorTests.cs`
**Dependencies:** None

---

## Phase 4: Application Layer - Repository Interfaces

**Goal:** Define data access contracts
**Estimated Tests:** 15-20 test cases

### 4.1 IFileRepository Interface
- ⬜ Should define AddAsync method
- ⬜ Should define GetByIdAsync method
- ⬜ Should define UpdateAsync method
- ⬜ Should define DeleteAsync method (soft delete)
- ⬜ Should define GetByUserIdAsync with pagination
- ⬜ Should define SearchAsync with filters
- ⬜ Should define ExistsByHashAsync for deduplication
- ⬜ Should support unit of work pattern

**Test Class:** `IFileRepositoryContractTests.cs`
**Dependencies:** File Entity
**Note:** Interface only, implementation tested in Infrastructure

---

### 4.2 IUploadSessionRepository Interface
- ⬜ Should define CreateSessionAsync
- ⬜ Should define GetSessionAsync
- ⬜ Should define UpdateSessionAsync
- ⬜ Should define DeleteSessionAsync
- ⬜ Should define GetExpiredSessionsAsync

**Test Class:** `IUploadSessionRepositoryContractTests.cs`
**Dependencies:** None

---

## Phase 5: Application Layer - Application Services

**Goal:** Use cases and orchestration
**Estimated Tests:** 60-70 test cases

### 5.1 FileUploadService (Simple Upload)
- ⬜ Should validate file size before upload
- ⬜ Should validate file type before upload
- ⬜ Should reject file exceeding size limit
- ⬜ Should reject invalid file type
- ⬜ Should calculate file hash
- ⬜ Should check for duplicate file by hash
- ⬜ Should skip upload if duplicate found
- ⬜ Should return existing file ID if duplicate
- ⬜ Should select appropriate storage provider
- ⬜ Should generate unique storage path
- ⬜ Should upload file to selected provider
- ⬜ Should save file metadata to repository
- ⬜ Should update file status to Completed after upload
- ⬜ Should handle upload failure and set status to Failed
- ⬜ Should rollback metadata if upload fails
- ⬜ Should emit FileUploadStarted event
- ⬜ Should emit FileUploadCompleted event
- ⬜ Should queue thumbnail generation job asynchronously

**Test Class:** `FileUploadServiceTests.cs`
**Dependencies:** IFileRepository, IStorageProvider, StorageProviderSelector, FileHashCalculator

---

### 5.2 ChunkedUploadService
- ⬜ Should initiate upload session with file metadata
- ⬜ Should generate upload ID (GUID)
- ⬜ Should calculate number of chunks needed
- ⬜ Should set chunk size to 5MB by default
- ⬜ Should generate presigned URLs for each chunk
- ⬜ Should set session expiration to 24 hours
- ⬜ Should save upload session to repository
- ⬜ Should track uploaded chunk parts
- ⬜ Should validate chunk MD5 hash
- ⬜ Should reject chunk with invalid MD5
- ⬜ Should update session progress after each chunk
- ⬜ Should complete upload when all chunks uploaded
- ⬜ Should assemble chunks in correct order
- ⬜ Should verify final file hash matches expected
- ⬜ Should handle upload resumption (return uploaded parts)
- ⬜ Should cancel upload and cleanup resources
- ⬜ Should expire upload session after 24 hours
- ⬜ Should cleanup expired sessions via background job

**Test Class:** `ChunkedUploadServiceTests.cs`
**Dependencies:** IUploadSessionRepository, IFileRepository, IStorageProvider

---

### 5.3 FileRetrievalService
- ⬜ Should retrieve file metadata by ID
- ⬜ Should return 404 if file not found
- ⬜ Should generate direct download URL for non-Glacier files
- ⬜ Should initiate Glacier retrieval for archived files
- ⬜ Should select retrieval tier (bulk, standard, expedited)
- ⬜ Should save retrieval request to database
- ⬜ Should return estimated retrieval time
- ⬜ Should poll retrieval status from provider
- ⬜ Should generate time-limited download URL when ready
- ⬜ Should set download URL expiration to 24 hours
- ⬜ Should validate user has permission to retrieve file
- ⬜ Should track retrieval request in database
- ⬜ Should expire retrieval request after timeout
- ⬜ Should send webhook notification when retrieval ready

**Test Class:** `FileRetrievalServiceTests.cs`
**Dependencies:** IFileRepository, IStorageProvider

---

### 5.4 ThumbnailGenerationService
- ⬜ Should generate 3 thumbnail sizes for images (150, 300, 600)
- ⬜ Should use WebP format for thumbnails
- ⬜ Should maintain aspect ratio when resizing
- ⬜ Should set quality to 85% by default
- ⬜ Should generate thumbnails asynchronously via job queue
- ⬜ Should store thumbnails in S3 Standard (not Glacier)
- ⬜ Should use CDN for thumbnail delivery
- ⬜ Should support lazy generation (on first request, then cache)
- ⬜ Should generate 3 preview frames for videos (25%, 50%, 75%)
- ⬜ Should generate 10-second preview clip for videos
- ⬜ Should handle thumbnail generation failure gracefully
- ⬜ Should skip thumbnail generation for unsupported types
- ⬜ Should cache thumbnail URLs in file metadata
- ⬜ Should return 202 if thumbnail still generating
- ⬜ Should return cached thumbnail URL if available

**Test Class:** `ThumbnailGenerationServiceTests.cs`
**Dependencies:** IFileRepository, IStorageProvider, Job Queue Interface

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

## Phase 6: Application Layer - Rate Limiting & Quota

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

## Phase 7: Application Layer - Advanced Features

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

## Phase 8: Infrastructure Layer - Storage Providers

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

### 8.3 BackblazeB2Provider
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

## Phase 9: Infrastructure Layer - Persistence

**Estimated Tests:** 40-45 test cases

### 9.1 FileRepository (EF Core Implementation)
- ⬜ Should add file entity to database
- ⬜ Should generate unique ID on insert
- ⬜ Should retrieve file by ID
- ⬜ Should return null if file not found
- ⬜ Should update file entity
- ⬜ Should handle concurrency conflicts (optimistic locking)
- ⬜ Should soft delete file (mark as deleted, not remove)
- ⬜ Should query files by user ID
- ⬜ Should support pagination (skip, take)
- ⬜ Should filter by file type
- ⬜ Should filter by status
- ⬜ Should filter by date range
- ⬜ Should search by filename (case-insensitive)
- ⬜ Should search by tags
- ⬜ Should check if hash exists
- ⬜ Should return file by hash
- ⬜ Should use indexes for performance
- ⬜ Should eager load related entities when needed
- ⬜ Should support unit of work (SaveChanges)

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

## Phase 10: Infrastructure Layer - Background Jobs

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

## Phase 11: API Layer - Controllers

**Estimated Tests:** 70-80 test cases

### 11.1 AuthController
- ⬜ Should return access token for valid authorization code
- ⬜ Should return refresh token for valid authorization code
- ⬜ Should return 401 for invalid authorization code
- ⬜ Should refresh access token with valid refresh token
- ⬜ Should return 401 for invalid refresh token
- ⬜ Should revoke tokens on logout
- ⬜ Should return proper token expiration times

**Test Class:** `AuthControllerTests.cs`
**Dependencies:** OAuth2 service (mock)

---

### 11.2 FilesController (Upload)
- ⬜ Should accept multipart form upload
- ⬜ Should return 201 Created with file ID
- ⬜ Should validate Content-Type header
- ⬜ Should validate file size against user tier limit
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

### 11.4 FilesController (Retrieval)
- ⬜ Should return file metadata by ID
- ⬜ Should return 404 if file not found
- ⬜ Should return thumbnail URL
- ⬜ Should return 202 if thumbnail still generating
- ⬜ Should serve thumbnail image
- ⬜ Should initiate Glacier retrieval
- ⬜ Should return retrieval ID and estimated time
- ⬜ Should return retrieval status
- ⬜ Should return download URL when ready
- ⬜ Should download file (redirect to presigned URL)
- ⬜ Should return 202 if file in Glacier and not retrieved
- ⬜ Should support Range requests for resumable download
- ⬜ Should return 206 Partial Content for range request
- ⬜ Should validate user owns file before retrieval
- ⬜ Should return 403 Forbidden if not owner

**Test Class:** `FilesControllerRetrievalTests.cs`
**Dependencies:** FileRetrievalService, ThumbnailGenerationService

---

### 11.5 FilesController (Management)
- ⬜ Should list files with pagination
- ⬜ Should filter files by type
- ⬜ Should filter files by date range
- ⬜ Should search files by query string
- ⬜ Should sort files by date/size/name
- ⬜ Should update file metadata (PATCH)
- ⬜ Should delete file (soft delete)
- ⬜ Should return 204 No Content after delete
- ⬜ Should batch compare hashes
- ⬜ Should return sync status since timestamp

**Test Class:** `FilesControllerManagementTests.cs`
**Dependencies:** FileSearchService, IFileRepository, HashComparisonService

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

## Phase 12: Integration Tests

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
2. ⬜ Setup project structure (.NET 8 solution)
3. ⬜ Configure xUnit and FluentAssertions
4. ⬜ Start Phase 1: Domain Layer Value Objects
5. ⬜ Follow TDD cycle: Red → Green → Refactor

**Ready to start coding?** Let me know when to proceed!

---

**Last Updated:** 2025-10-25
**Document Owner:** FlexStorage Team
