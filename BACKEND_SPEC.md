# FlexStorage Backend API Specification

**Version:** 1.0.0
**Technology Stack:** .NET 8, C#, xUnit, FluentAssertions
**Architecture:** Domain-Driven Design (DDD) with Test-Driven Development (TDD)
**Purpose:** Photo, video, and file upload service with multi-provider cloud storage (S3 Glacier, Backblaze, etc.)

---

## Table of Contents
1. [Feature Priority Matrix](#feature-priority-matrix)
2. [Phase 1: MVP - Core Upload & Storage](#phase-1-mvp---core-upload--storage)
3. [Phase 2: Essential Features](#phase-2-essential-features)
4. [Phase 3: Production Ready](#phase-3-production-ready)
5. [Phase 4: Advanced Features](#phase-4-advanced-features)
6. [Phase 5: Nice-to-Have](#phase-5-nice-to-have)
7. [API Endpoints Reference](#api-endpoints-reference)
8. [Configuration](#configuration)

---

## Feature Priority Matrix

| Priority | Feature | Complexity | Status |
|----------|---------|------------|--------|
| P0 | Simple file upload (< 10MB) | Low | ⬜ Not Started |
| P0 | File metadata storage | Low | ⬜ Not Started |
| P0 | S3 Glacier Deep Archive provider | Medium | ⬜ Not Started |
| P0 | Basic authentication (OAuth2) | Medium | ⬜ Not Started |
| P0 | File retrieval from Glacier | Medium | ⬜ Not Started |
| P1 | Chunked/resumable upload | Medium | ⬜ Not Started |
| P1 | Thumbnail generation & caching | Medium | ⬜ Not Started |
| P1 | Hash-based deduplication | Medium | ⬜ Not Started |
| P1 | Batch hash comparison API | Low | ⬜ Not Started |
| P1 | S3 Glacier Flexible Retrieval provider | Low | ⬜ Not Started |
| P1 | Backblaze B2 provider | Medium | ⬜ Not Started |
| P2 | 3-tier rate limiting | Medium | ⬜ Not Started |
| P2 | Quota management (multi-provider) | High | ⬜ Not Started |
| P2 | File search with tags | Medium | ⬜ Not Started |
| P2 | Provider plugin architecture | High | ⬜ Not Started |
| P2 | Range/resume download support | Low | ⬜ Not Started |
| P3 | File redundancy management | High | ⬜ Not Started |
| P3 | Provider rebalancing | High | ⬜ Not Started |
| P3 | Public file sharing | Medium | ⬜ Not Started |
| P3 | Webhook notifications | Low | ⬜ Not Started |
| P4 | AI-powered auto-tagging | High | ⬜ Not Started |
| P4 | Advanced analytics dashboard | Medium | ⬜ Not Started |

---

## Phase 1: MVP - Core Upload & Storage

**Goal:** Basic file upload and archival to single provider
**Timeline:** Week 1-2
**Dependencies:** None

### Features

#### 1.1 Simple File Upload (< 10MB)
- **Endpoint:** `POST /files`
- **Input:** Multipart form with file + metadata
- **Output:** File ID, status
- **Storage:** Single provider (S3 Glacier Deep Archive)
- **Validation:**
  - Max file size: 10MB
  - Allowed types: image/*, video/*, application/*
  - Filename sanitization
  - SHA256 hash calculation

#### 1.2 File Metadata Storage
- Store file information in database:
  - File ID (GUID)
  - Original filename
  - Sanitized filename
  - File size (bytes)
  - MIME type
  - SHA256 hash
  - Upload timestamp
  - User ID
  - Storage location
  - Status (Pending, Uploading, Completed, Failed, Archived)

#### 1.3 S3 Glacier Deep Archive Provider
- **Operations:**
  - Upload file to S3 Glacier Deep Archive
  - Verify upload success
  - Store location reference
- **Configuration:**
  - AWS credentials (from appsettings/secrets)
  - Bucket name
  - Region
  - Storage class: DEEP_ARCHIVE

#### 1.4 Basic OAuth2 Authentication
- **Grant Types:**
  - Authorization Code (for initial login)
  - Refresh Token (for silent refresh)
- **Token Management:**
  - Access token (1 hour expiry)
  - Refresh token (90 days expiry)
  - Token rotation on refresh
- **Endpoints:**
  - `POST /auth/token` - Get tokens
  - `POST /auth/refresh` - Refresh access token
  - `POST /auth/revoke` - Logout

#### 1.5 File Retrieval (Glacier)
- **Endpoint:** `POST /files/{fileId}/retrieve`
- **Retrieval Tiers:**
  - Bulk: 5-12 hours (cheapest)
  - Standard: 3-5 hours
  - Expedited: 1-5 minutes (if available)
- **Process:**
  - Initiate retrieval request
  - Poll retrieval status
  - Get download URL when ready
  - Auto-expire download URL after 24 hours

#### 1.6 Get File Metadata
- **Endpoint:** `GET /files/{fileId}`
- **Response:**
  - All file metadata
  - Current status
  - Storage location
  - Retrieval status (if applicable)

---

## Phase 2: Essential Features

**Goal:** Support large files, multiple providers, efficiency
**Timeline:** Week 3-4
**Dependencies:** Phase 1 complete

### Features

#### 2.1 Chunked/Resumable Upload
- **Endpoint:** `POST /files/upload/initiate`
- **Chunk Size:** 5MB (configurable)
- **Flow:**
  1. Initiate upload → get upload ID + presigned URLs
  2. Upload chunks → `PUT /files/upload/{uploadId}/parts/{partNumber}`
  3. Complete upload → `POST /files/upload/{uploadId}/complete`
  4. Resume capability → `GET /files/upload/{uploadId}` returns uploaded parts
  5. Cancel upload → `DELETE /files/upload/{uploadId}`
- **Features:**
  - MD5 verification per chunk
  - Upload expiration (24 hours)
  - Track upload progress
  - Support files up to 5GB

#### 2.2 Thumbnail Generation & Caching
- **Trigger:** Automatic on file upload (before Glacier archival)
- **Image Thumbnails:**
  - Small: 150x150px
  - Medium: 300x300px
  - Large: 600x600px
  - Format: JPEG, quality 85%
- **Video Thumbnails:**
  - 3 preview frames (at 25%, 50%, 75% of duration)
  - 10-second preview clip (first 10s, compressed)
- **Storage:**
  - S3 Standard (NOT Glacier) for instant access
  - CDN integration (CloudFront)
- **Endpoint:** `GET /files/{fileId}/thumbnail?size=medium`
- **Permanence:** Thumbnails never expire

#### 2.3 Hash-Based Deduplication
- **Process:**
  1. Client calculates SHA256 hash before upload
  2. Send hash in initiate upload request
  3. Backend checks if hash exists
  4. If exists:
     - Skip upload
     - Link to existing file
     - Return existing file ID
     - Save bandwidth + storage
  5. If not exists:
     - Proceed with upload
- **Benefits:**
  - Save upload bandwidth
  - Save storage costs
  - Faster "upload" experience

#### 2.4 Batch Hash Comparison API
- **Endpoint:** `POST /files/compare-hashes`
- **Input:** Array of up to 1000 hashes + filenames
- **Output:** For each hash:
  - Exists in cloud: true/false
  - File ID (if exists)
  - Upload status
  - Safe to delete locally: true/false
- **Use Case:** Mobile app can safely delete local files after confirming upload

#### 2.5 Additional Storage Providers
- **S3 Glacier Flexible Retrieval:**
  - Faster retrieval (3-5 hours standard)
  - Higher cost than Deep Archive
  - Good for occasionally accessed files
- **Backblaze B2:**
  - Instant access (no retrieval delay)
  - Competitive pricing
  - Good for frequently accessed files
  - Free API calls

#### 2.6 Sync Status Endpoint
- **Endpoint:** `GET /files/sync-status?since=2025-10-20T00:00:00Z`
- **Purpose:** Mobile app periodic sync
- **Response:**
  - All files uploaded/modified since timestamp
  - File hashes
  - Current status
- **Pagination:** Support large file counts

---

## Phase 3: Production Ready

**Goal:** Performance, limits, multi-provider management
**Timeline:** Week 5-6
**Dependencies:** Phase 2 complete

### Features

#### 3.1 3-Tier Rate Limiting
- **Tiers:**
  - **Free:** 10 uploads/hour, 50/day, 1GB bandwidth/day, 100MB max file
  - **Standard:** 100/hour, 1000/day, 10GB bandwidth/day, 1GB max file
  - **Premium:** 1000/hour, 10000/day, 100GB bandwidth/day, 5GB max file
- **Provider Rate Limits:**
  - S3: Max 100 requests/sec (configurable)
  - S3 Deep: Max 50 requests/sec, batch uploads enabled
  - Backblaze: Max 1000 requests/sec
  - Cost-aware throttling (configurable)
- **Response Headers:**
  - X-RateLimit-Limit
  - X-RateLimit-Remaining
  - X-RateLimit-Reset
  - Retry-After (on 429)
- **Configuration:** Per-user profile, per-provider limits

#### 3.2 Quota Management (Multi-Provider)
- **Tracking:**
  - Total quota (e.g., 2TB)
  - Per-provider quota (e.g., 1TB S3 Deep, 500GB S3 Flex, 500GB Backblaze)
  - Used space per provider
  - Remaining space per provider
  - File count per provider
- **Endpoint:** `GET /users/me/quota`
- **Enforcement:**
  - Reject upload if quota exceeded
  - Recommend rebalancing when uneven
- **Visualization:**
  - Usage percentage per provider
  - Recommendations for optimization

#### 3.3 File Search with Tags
- **Auto-Generated Tags:**
  - Type: photo, video, screenshot, misc
  - Format: jpeg, png, heic, mp4, mov, etc.
  - Date: year, month, date
  - Size category: small, medium, large
  - Location: city/region (if GPS provided)
- **User Tags:**
  - Custom tags (optional)
  - Add/update via PATCH endpoint
- **Search Endpoint:** `GET /files/search?q=beach&tags=vacation&dateFrom=2025-01-01`
- **Full-Text Search:**
  - Search in tags
  - Search in filename
  - Search in user-provided description
- **Response:** Ranked results with relevance score

#### 3.4 Provider Plugin Architecture
- **Goal:** Easy addition of new storage providers
- **Interface:** `IStorageProvider`
  ```csharp
  public interface IStorageProvider
  {
      string ProviderName { get; }
      Task<UploadResult> UploadAsync(Stream fileStream, UploadOptions options);
      Task<Stream> DownloadAsync(string location);
      Task<RetrievalResult> InitiateRetrievalAsync(string location, RetrievalTier tier);
      Task<bool> DeleteAsync(string location);
      Task<HealthStatus> CheckHealthAsync();
  }
  ```
- **Plugin Loading:**
  - Discover plugins from `/plugins` directory
  - Load provider assemblies at startup
  - Validate provider implements interface
  - Register in DI container
- **Configuration:**
  - Per-provider configuration in appsettings.json
  - Hot-reload support (future)

#### 3.5 Range/Resume Download Support
- **HTTP Range Requests:**
  - Support `Range: bytes=0-1048575` header
  - Return `206 Partial Content`
  - Include `Content-Range` header
- **Use Case:**
  - Resume interrupted downloads
  - Stream large video files
  - Download specific portions

#### 3.6 File Listing with Pagination & Filtering
- **Endpoint:** `GET /files?page=1&pageSize=50&mimeType=image/*&status=archived`
- **Filters:**
  - MIME type
  - Status
  - Tags
  - Date range
  - File size range
  - Provider
- **Sorting:**
  - Created date (default)
  - File size
  - Filename
  - Modified date
- **Pagination:**
  - Page number + page size
  - Total pages + total items
  - Next/previous page links

---

## Phase 4: Advanced Features

**Goal:** Redundancy, optimization, advanced management
**Timeline:** Week 7-8
**Dependencies:** Phase 3 complete

### Features

#### 4.1 File Redundancy Management
- **Redundancy Profiles:**
  - **None:** 1 copy, single provider
  - **Standard:** 2 copies, different providers/regions
  - **Paranoid:** 3 copies, cross-provider + cross-region
- **Configuration:** `POST /files/{fileId}/redundancy`
- **Verification:**
  - Periodic checksum verification (weekly)
  - Detect missing/corrupted copies
  - Auto-repair missing copies
- **Status:** `GET /files/{fileId}/redundancy`
  - List all copies
  - Checksum verification status
  - Redundancy health score (0-100)
- **Background Jobs:**
  - Auto-create redundant copies
  - Handle Glacier retrieval delays
  - Verify integrity

#### 4.2 Provider Rebalancing
- **Strategies:**
  - **Cost-optimize:** Move to cheapest provider
  - **Performance-optimize:** Move to fastest provider
  - **Even-distribute:** Balance usage across providers
- **Endpoint:** `POST /files/rebalance`
- **Input:**
  - Strategy
  - Source/target providers
  - File selection criteria (rarely-accessed, large-files, all)
  - Max files/size to move
  - Schedule (immediate or scheduled)
  - Rate limit (bandwidth throttle)
- **Process:**
  1. Identify files to move
  2. Calculate cost estimate
  3. Schedule job
  4. For each file:
     - Initiate retrieval (if Glacier)
     - Wait for retrieval
     - Upload to target provider
     - Verify integrity
     - Update metadata
     - Delete from source (optional)
  5. Track progress
- **Status:** `GET /jobs/rebalance/{jobId}`
  - Progress (files, bytes)
  - Estimated completion
  - Errors
  - Cost tracking

#### 4.3 Public File Sharing
- **Endpoint:** `POST /files/{fileId}/share`
- **Options:**
  - Expiration date (optional)
  - Password protection (optional)
  - Max download count (optional)
  - Allow in public gallery (future)
- **Response:**
  - Share ID
  - Public URL: `https://share.flexstorage.com/{shareId}`
  - Short URL: `https://flx.st/{short}`
- **Access:** `GET /share/{shareId}`
  - Password prompt if required
  - Track download count
  - Check expiration
  - Generate time-limited download URL
- **Management:**
  - List shares: `GET /files/{fileId}/shares`
  - Revoke share: `DELETE /shares/{shareId}`
  - Update share: `PATCH /shares/{shareId}`

#### 4.4 Webhook Notifications
- **Events:**
  - `file.uploaded` - File upload completed
  - `file.archived` - File archived to storage
  - `retrieval.initiated` - Glacier retrieval started
  - `retrieval.ready` - File ready for download
  - `file.deleted` - File deleted
  - `redundancy.failed` - Redundant copy verification failed
  - `quota.warning` - 80% quota reached
  - `quota.exceeded` - Quota exceeded
- **Registration:** `POST /webhooks`
- **Payload:**
  ```json
  {
    "event": "retrieval.ready",
    "timestamp": "2025-10-25T11:00:00Z",
    "data": {
      "fileId": "file_3nB8vC1kL",
      "retrievalId": "ret_9kL3mN7pQ",
      "downloadUrl": "https://download.flexstorage.com/...",
      "expiresAt": "2025-10-26T11:00:00Z"
    }
  }
  ```
- **Signature:** HMAC-SHA256 for verification
- **Retry:** Exponential backoff on failure

#### 4.5 Batch Operations
- **Endpoint:** `POST /files/batch`
- **Operations:**
  - Delete multiple files
  - Update tags on multiple files
  - Initiate retrieval for multiple files
  - Change redundancy level
  - Move to different provider
- **Input:**
  - Operation type
  - Array of file IDs (up to 1000)
  - Operation parameters
- **Response:**
  - Batch job ID
  - Status tracking endpoint
- **Processing:**
  - Async background job
  - Track success/failure per file
  - Partial success support

---

## Phase 5: Nice-to-Have

**Goal:** Premium features, optimization, analytics
**Timeline:** Future iterations
**Dependencies:** Phase 4 complete

### Features

#### 5.1 AI-Powered Auto-Tagging
- **Image Recognition:**
  - Detect scenes (beach, mountain, indoor, outdoor)
  - Detect objects (person, car, dog, food)
  - Detect colors (dominant colors)
  - Quality assessment (blurry, overexposed, etc.)
  - Face count (no face recognition for privacy)
- **Video Analysis:**
  - Scene detection
  - Motion detection
  - Audio detection (speech, music)
- **Integration:**
  - Plugin architecture
  - Support multiple AI providers (AWS Rekognition, Google Vision, Azure)
  - Async processing (after upload)
- **Privacy:**
  - User opt-in required
  - No face recognition/identification
  - No data sharing with AI provider beyond processing

#### 5.2 Advanced Analytics Dashboard
- **User Analytics:**
  - Total storage used (trend over time)
  - Upload frequency (daily/weekly/monthly)
  - File type distribution
  - Provider cost breakdown
  - Bandwidth usage
  - Most accessed files
  - Redundancy health score
- **System Analytics:**
  - Total users
  - Total storage across all providers
  - Provider health status
  - Upload success/failure rates
  - Retrieval request patterns
  - API performance metrics
- **Cost Analytics:**
  - Monthly cost per provider
  - Cost per GB
  - Retrieval costs
  - Projected costs
  - Savings from deduplication

#### 5.3 Smart Storage Recommendations
- **ML-based Suggestions:**
  - "Move rarely accessed files to Deep Archive to save $X/month"
  - "These files are accessed frequently, move to Backblaze for instant access"
  - "Enable redundancy for these important files"
  - "Delete files you haven't accessed in 2 years"
- **Auto-optimization:**
  - User approves recommendations
  - System executes optimizations
  - Track savings

#### 5.4 Video Transcoding
- **Generate Multiple Qualities:**
  - 1080p, 720p, 480p, 360p
  - Different codecs (H.264, H.265)
  - Adaptive bitrate streaming (HLS/DASH)
- **Storage:**
  - Original in Glacier
  - Transcoded versions in S3 Standard or Backblaze
- **Use Case:**
  - Stream videos without full retrieval
  - Bandwidth optimization for mobile

#### 5.5 Timeline/Gallery View
- **Organization:**
  - Group files by date (year, month, day)
  - Location-based grouping (if GPS data)
  - Event detection (trips, gatherings)
- **Endpoint:** `GET /files/timeline?year=2025&month=10`
- **Response:**
  - Grouped files
  - Representative thumbnails
  - Count per group

#### 5.6 Collaborative Albums
- **Features:**
  - Create album
  - Add files to album
  - Share album with other users
  - Collaborative upload (multiple users)
  - Comments on files
- **Permissions:**
  - Owner, Editor, Viewer roles
  - Public/private albums

---

## API Endpoints Reference

### Authentication
```
POST   /auth/token           # Get access token
POST   /auth/refresh         # Refresh access token
POST   /auth/revoke          # Logout/revoke token
```

### File Upload
```
POST   /files                      # Simple upload (< 10MB)
POST   /files/upload/initiate      # Initiate chunked upload
PUT    /files/upload/{uploadId}/parts/{partNumber}  # Upload chunk
POST   /files/upload/{uploadId}/complete            # Complete upload
GET    /files/upload/{uploadId}                     # Get upload status
DELETE /files/upload/{uploadId}                     # Cancel upload
```

### File Retrieval
```
GET    /files/{fileId}                    # Get file metadata
GET    /files/{fileId}/thumbnail          # Get thumbnail
POST   /files/{fileId}/retrieve           # Initiate Glacier retrieval
GET    /files/{fileId}/retrieve/{retId}   # Get retrieval status
GET    /files/{fileId}/download           # Download file
```

### File Management
```
GET    /files                    # List files (paginated)
GET    /files/search             # Search files
PATCH  /files/{fileId}           # Update file metadata
DELETE /files/{fileId}           # Delete file
POST   /files/batch              # Batch operations
POST   /files/compare-hashes     # Compare hash batch
GET    /files/sync-status        # Sync status
```

### Redundancy & Rebalancing
```
GET    /files/{fileId}/redundancy     # Get redundancy status
POST   /files/{fileId}/redundancy     # Set redundancy level
POST   /files/rebalance                # Initiate rebalancing
GET    /jobs/rebalance/{jobId}        # Get rebalance status
```

### Sharing
```
POST   /files/{fileId}/share      # Create public share
GET    /files/{fileId}/shares     # List shares
GET    /share/{shareId}           # Access shared file
PATCH  /shares/{shareId}          # Update share
DELETE /shares/{shareId}          # Revoke share
```

### Storage Providers
```
GET    /providers                    # List available providers
GET    /providers/{providerId}/health  # Get provider health
```

### User & Quota
```
GET    /users/me/quota        # Get quota information
GET    /users/me/stats        # Get usage statistics
```

### Webhooks
```
POST   /webhooks              # Register webhook
GET    /webhooks              # List webhooks
DELETE /webhooks/{webhookId} # Delete webhook
```

### System
```
GET    /health                # Health check
```

---

## Configuration

### appsettings.json Structure

```json
{
  "Authentication": {
    "OAuth2": {
      "Authority": "https://auth.flexstorage.com",
      "Audience": "flexstorage-api",
      "AccessTokenExpiration": 3600,
      "RefreshTokenExpiration": 7776000
    }
  },

  "RateLimiting": {
    "Profiles": {
      "Free": {
        "UploadsPerHour": 10,
        "UploadsPerDay": 50,
        "BandwidthPerDay": 1073741824,
        "MaxFileSize": 104857600,
        "ConcurrentUploads": 1
      },
      "Standard": {
        "UploadsPerHour": 100,
        "UploadsPerDay": 1000,
        "BandwidthPerDay": 10737418240,
        "MaxFileSize": 1073741824,
        "ConcurrentUploads": 3
      },
      "Premium": {
        "UploadsPerHour": 1000,
        "UploadsPerDay": 10000,
        "BandwidthPerDay": 107374182400,
        "MaxFileSize": 5368709120,
        "ConcurrentUploads": 10
      }
    }
  },

  "StorageProviders": {
    "S3GlacierDeep": {
      "Enabled": true,
      "MaxRequestsPerSecond": 50,
      "CostPerRequest": 0.00002,
      "ThrottleToSaveCost": true,
      "BatchUploads": true,
      "Config": {
        "Region": "us-west-2",
        "BucketName": "flexstorage-glacier-deep",
        "StorageClass": "DEEP_ARCHIVE"
      }
    },
    "S3GlacierFlexible": {
      "Enabled": true,
      "MaxRequestsPerSecond": 100,
      "Config": {
        "Region": "us-west-2",
        "BucketName": "flexstorage-glacier-flex",
        "StorageClass": "GLACIER_IR"
      }
    },
    "Backblaze": {
      "Enabled": true,
      "MaxRequestsPerSecond": 1000,
      "Config": {
        "AccountId": "",
        "ApplicationKey": "",
        "BucketName": "flexstorage-b2"
      }
    }
  },

  "Thumbnails": {
    "AutoGenerate": true,
    "Sizes": [150, 300, 600],
    "Quality": 85,
    "Format": "jpeg",
    "StorageProvider": "S3Standard",
    "CDNEnabled": true,
    "CDNUrl": "https://cdn.flexstorage.com"
  },

  "Redundancy": {
    "DefaultProfile": "none",
    "AutoVerify": true,
    "VerificationInterval": "7.00:00:00"
  },

  "Database": {
    "ConnectionString": "",
    "Provider": "PostgreSQL"
  }
}
```

---

## Success Criteria

### Phase 1 (MVP)
- ✅ Can upload file < 10MB
- ✅ File is stored in S3 Glacier Deep Archive
- ✅ Can retrieve file metadata
- ✅ Can initiate and complete Glacier retrieval
- ✅ OAuth2 authentication working

### Phase 2 (Essential)
- ✅ Can upload file up to 5GB with chunking
- ✅ Thumbnails generated automatically
- ✅ Duplicate files detected and skipped
- ✅ Mobile app can verify uploaded files via hash API
- ✅ Multiple storage providers working

### Phase 3 (Production)
- ✅ Rate limiting enforced
- ✅ Quota tracking per provider
- ✅ File search working
- ✅ Plugin architecture allows adding new providers

### Phase 4 (Advanced)
- ✅ Files can have redundant copies
- ✅ Files can be rebalanced between providers
- ✅ Public sharing working
- ✅ Webhooks delivering notifications

---

**Last Updated:** 2025-10-25
**Document Owner:** FlexStorage Team
