# FlexStorage API Documentation

**Version:** 1.0.0
**Base URL:** `https://api.flexstorage.com` (production) or `http://localhost:5000` (development)
**OpenAPI Spec:** [`docs/openapi.json`](docs/openapi.json)
**Interactive Docs:** `/swagger` (Development mode only)

---

## 🤖 For LLM-Assisted Development

This API is designed to work seamlessly with AI coding assistants (Claude, ChatGPT, Cursor, etc.).

### How to Use with LLMs:

1. **Provide the OpenAPI spec to your LLM:**
   ```
   "Here's the FlexStorage API spec: [paste docs/openapi.json]
   Help me integrate file upload into my React Native app"
   ```

2. **LLMs can:**
   - ✅ Generate client code (TypeScript, Swift, Kotlin)
   - ✅ Create API wrapper functions
   - ✅ Implement authentication flow
   - ✅ Handle error cases
   - ✅ Suggest optimal workflows

3. **Why LLMs need openapi.json:**
   - ❌ LLMs cannot access live Swagger UI URLs
   - ❌ LLMs cannot browse `http://localhost:5000/swagger`
   - ✅ LLMs CAN read the OpenAPI spec file
   - ✅ LLMs understand OpenAPI format natively

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Authentication](#authentication)
3. [Core Workflows](#core-workflows)
4. [Storage Classes](#storage-classes)
5. [Error Handling](#error-handling)
6. [Rate Limiting](#rate-limiting)
7. [Best Practices](#best-practices)
8. [Endpoints Reference](#endpoints-reference)

---

## Architecture Overview

```
┌─────────────┐
│   Client    │
│  (Mobile)   │
└──────┬──────┘
       │ HTTPS
       ↓
┌────────────────────────────────────┐
│        FlexStorage API             │
│  ┌──────────┐  ┌──────────────┐    │
│  │   Auth   │  │ File Upload  │    │
│  └──────────┘  └──────────────┘    │
└──────┬──────────────────┬──────────┘
       │                  │
       ↓                  ↓
┌──────────────┐  ┌─────────────────┐
│   Database   │  │   S3 Storage    │
│  (Metadata)  │  │  - Deep Archive │
│              │  │  - Flexible     │
│              │  │  - Standard     │
└──────────────┘  └─────────────────┘
```

### Key Concepts:

- **Glacier Archives** (Original files)
  - Stored in S3 Glacier Deep Archive or Flexible Retrieval
  - Very low cost ($0.00099/GB/month for Deep Archive)
  - Requires 12-48 hours for retrieval (Deep) or 3-5 hours (Flexible)

- **Thumbnails** (Always instant access)
  - Stored in S3 Standard
  - 300×300 pixels, WebP @ 80% quality (~10-15 KB)
  - Instant download, no retrieval needed

- **Hash-based Deduplication**
  - SHA-256 hash calculated on upload
  - Duplicate files return existing fileId (no re-upload)

---

## Authentication

### API Key (Development/Testing)

Simple API key authentication for development:

```http
POST /api/Auth/apikey
Content-Type: application/json

{
  "userId": "your-user-id",
  "description": "My test app"
}

Response:
{
  "apiKey": "fs_live_abc123...",
  "expiresAt": "2025-12-31T23:59:59Z"
}
```

**Use the API key in all requests:**

```http
GET /api/v1/Files
X-API-Key: fs_live_abc123...
```

### OAuth2 (Production) - Coming in P1

OAuth2 authentication flow (not yet implemented):

```
1. Redirect user to: /auth/oauth2/authorize?client_id=xxx&redirect_uri=xxx
2. User logs in and approves
3. Callback: redirect_uri?code=xxx
4. Exchange code for token: POST /auth/oauth2/token
5. Use Bearer token: Authorization: Bearer {access_token}
```

---

## Core Workflows

### 1. Upload Small File (<20MB)

**Single request upload for small files (photos, documents):**

```http
POST /api/v1/Files/upload
X-API-Key: your-api-key
Content-Type: multipart/form-data

file: [binary data]
userId: 123e4567-e89b-12d3-a456-426614174000
capturedAt: 2025-01-15T10:30:00Z (optional)
```

**Response:**

```json
{
  "fileId": "uuid",
  "success": true,
  "location": "s3://bucket/path",
  "isDuplicate": false,
  "message": "File uploaded successfully"
}
```

**What happens behind the scenes:**
1. ✅ SHA-256 hash calculated
2. ✅ Check for duplicates (if duplicate, returns existing fileId)
3. ✅ Original file → Glacier Deep Archive (12-48h retrieval)
4. ✅ Thumbnail generated (300×300 WebP) → S3 Standard (instant)
5. ✅ Metadata saved to database

---

### 2. Upload Large File (>20MB) - Chunked

**Step 1: Initialize chunked upload**

```http
POST /api/v1/Files/chunked/init
X-API-Key: your-api-key
Content-Type: application/json

{
  "fileName": "large-video.mp4",
  "fileSize": 104857600,
  "mimeType": "video/mp4",
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "chunkSize": 5242880
}

Response:
{
  "sessionId": "uuid",
  "expiresAt": "2025-01-16T10:30:00Z"
}
```

**Step 2: Upload chunks (repeat for each chunk)**

```http
POST /api/v1/Files/chunked/{sessionId}/chunk
X-API-Key: your-api-key
Content-Type: multipart/form-data

chunk: [binary data]
chunkIndex: 0

Response:
{
  "success": true,
  "chunkIndex": 0,
  "chunksReceived": 1,
  "totalChunks": 20
}
```

**Step 3: Complete upload**

```http
POST /api/v1/Files/chunked/{sessionId}/complete
X-API-Key: your-api-key

Response:
{
  "fileId": "uuid",
  "success": true,
  "location": "s3://bucket/path"
}
```

---

### 3. List Files with Thumbnails

```http
GET /api/v1/Files?userId={userId}&page=1&pageSize=50
X-API-Key: your-api-key

Response:
{
  "files": [
    {
      "id": "uuid",
      "fileName": "beach-photo.jpg",
      "size": 2048576,
      "sizeFormatted": "2.0 MB",
      "contentType": "image/jpeg",
      "capturedAt": "2025-01-15T10:30:00Z",
      "uploadedAt": "2025-01-15T10:35:22Z",
      "status": "Completed",

      // Storage info
      "storageProvider": "s3-glacier-deep",
      "storagePath": "s3://flexstorage-deep-archive/2025/01/15/...",

      // Retrieval info
      "needsRetrieval": true,           // ⚠️ Show "cloud download" icon
      "retrievalTimeHours": 12,         // 12-48 hours for deep archive

      // Thumbnail (always instant!)
      "thumbnailUrl": "s3://flexstorage-thumbnails/...",

      "userId": "uuid"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalFiles": 127,
  "queriedUserId": "uuid"
}
```

**UI Recommendations:**
- ✅ Always load thumbnails immediately (instant access)
- ⚠️ Show "cloud" icon for files where `needsRetrieval: true`
- ℹ️ Display `retrievalTimeHours` to set user expectations
- 🚫 Don't allow download if `needsRetrieval: true` (initiate retrieval first)

---

### 4. Download/Retrieve Archived File

**⚠️ IMPORTANT: Files in Glacier require restoration before download!**

#### Scenario A: File Already Available

```http
GET /api/v1/Files/{fileId}/download
X-API-Key: your-api-key

Response: 200 OK
Content-Type: image/jpeg
Content-Disposition: attachment; filename="beach-photo.jpg"

[Binary file data]
```

#### Scenario B: File Needs Restoration (Glacier)

```http
GET /api/v1/Files/{fileId}/download
X-API-Key: your-api-key

Response: 202 Accepted
{
  "message": "File is archived and restoration has been initiated",
  "retrievalId": "uuid",
  "estimatedCompletionTime": "2025-01-16T10:30:00Z",  // +12-48 hours
  "status": "restoration_in_progress"
}
```

**Step 2: Poll retrieval status**

```http
GET /api/v1/Files/retrieval/{retrievalId}/status
X-API-Key: your-api-key

Response:
{
  "retrievalId": "uuid",
  "status": "InProgress",  // Pending | InProgress | Completed | Failed
  "fileId": "uuid",
  "initiatedAt": "2025-01-15T10:35:00Z",
  "completedAt": null,
  "estimatedCompletionTime": "2025-01-16T10:30:00Z"
}
```

**Polling Strategy:**
- First hour: Poll every 30 minutes
- After 1 hour: Poll every hour
- After 6 hours: Poll every 2 hours
- Max wait: 48 hours for Deep Archive

**Step 3: Download when completed**

```http
GET /api/v1/Files/{fileId}/download
X-API-Key: your-api-key

Response: 200 OK
[Binary file data]
```

---

## Storage Classes

| Class | Cost/GB/Month | Retrieval Time | Use Case | Provider Name |
|-------|---------------|----------------|----------|---------------|
| **S3 Glacier Deep Archive** | $0.00099 | 12-48 hours | Original files (photos/videos) | `s3-glacier-deep` |
| **S3 Glacier Flexible** | $0.0036 | 3-5 hours | Frequently accessed archives | `s3-glacier-flexible` |
| **S3 Standard** | $0.023 | Instant | Thumbnails | `s3-standard` |

**Cost Example:**
- 1000 photos @ 2MB each = 2GB
- Deep Archive: $0.00198/month ($0.02/year)
- Thumbnails @ 15KB = 15MB
- S3 Standard: $0.000345/month

**Recommendation:** Store originals in Deep Archive, thumbnails in Standard.

---

## Error Handling

### HTTP Status Codes

| Code | Meaning | Action |
|------|---------|--------|
| **200** | Success | Process response |
| **202** | Accepted (async operation) | Poll retrieval status |
| **400** | Bad request | Check request format |
| **401** | Unauthorized | Check API key |
| **404** | Not found | File doesn't exist |
| **409** | Conflict (duplicate) | Use existing fileId |
| **413** | Payload too large | Use chunked upload |
| **429** | Rate limit exceeded | Retry after X seconds |
| **500** | Server error | Retry with exponential backoff |

### Error Response Format

```json
{
  "error": "Detailed error message",
  "code": "ERROR_CODE",
  "details": {
    "field": "Additional context"
  }
}
```

### Common Error Scenarios

**1. Duplicate File (409 Conflict)**

```json
{
  "error": "File with this hash already exists",
  "existingFileId": "uuid",
  "isDuplicate": true
}
```

**Action:** Use the existing fileId, no need to re-upload.

**2. File Archived (202 Accepted)**

```json
{
  "message": "File is archived and restoration has been initiated",
  "retrievalId": "uuid",
  "estimatedCompletionTime": "2025-01-16T10:30:00Z"
}
```

**Action:** Poll `/api/v1/Files/retrieval/{retrievalId}/status` every 30-60 minutes.

**3. Rate Limited (429 Too Many Requests)**

```json
{
  "error": "Rate limit exceeded",
  "retryAfter": 60  // seconds
}
```

**Action:** Wait `retryAfter` seconds, then retry.

---

## Rate Limiting

**Current limits (subject to change):**

| Endpoint | Limit | Window |
|----------|-------|--------|
| Upload | 100 requests | Per hour |
| Download | 500 requests | Per hour |
| List Files | 1000 requests | Per hour |

**Response headers:**

```http
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 87
X-RateLimit-Reset: 1640000000
```

---

## Best Practices

### 1. **Always Check for Duplicates**

Before uploading, you can check if a file already exists:

```http
POST /api/v1/Files/hash/check
{
  "hashes": ["sha256:abc123...", "sha256:def456..."]
}

Response:
{
  "results": [
    { "hash": "sha256:abc123...", "exists": true, "fileId": "uuid" },
    { "hash": "sha256:def456...", "exists": false }
  ]
}
```

### 2. **Use Thumbnails for Gallery Views**

- ✅ Load thumbnails immediately (instant, small)
- ❌ Don't load full-size images for galleries
- 💡 Thumbnails are 300×300 WebP (~10-15 KB vs 2+ MB originals)

### 3. **Handle Glacier Restoration UX**

**Good UX:**
```
Photo Gallery
├── [Thumbnail] beach.jpg ✓ (instant)
│   └── Tap to view full size
│       ├── Status: Preparing download (12-48h)
│       └── [Progress bar] Estimated: Jan 16, 10:30 AM
│       └── [Notify me when ready]
```

**Bad UX:**
```
Photo Gallery
├── [Spinner] Loading... (user waits 48 hours!)
```

### 4. **Chunked Upload for Large Files**

Use chunked upload for:
- ✅ Files > 20MB
- ✅ Large videos
- ✅ Poor network conditions (resumable!)

### 5. **Retry Strategy**

```javascript
async function uploadWithRetry(file, maxRetries = 3) {
  for (let i = 0; i < maxRetries; i++) {
    try {
      return await uploadFile(file);
    } catch (error) {
      if (error.status === 429) {
        // Rate limited
        await sleep(error.retryAfter * 1000);
      } else if (error.status >= 500) {
        // Server error - exponential backoff
        await sleep(Math.pow(2, i) * 1000);
      } else {
        // Client error - don't retry
        throw error;
      }
    }
  }
  throw new Error('Max retries exceeded');
}
```

---

## Endpoints Reference

**For detailed request/response schemas, see:**
- 📄 OpenAPI Spec: [`docs/openapi.json`](docs/openapi.json)
- 🌐 Interactive Docs: `http://localhost:5000/swagger` (development only)

### Quick Reference

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/Auth/apikey` | POST | Generate API key |
| `/api/v1/Files/upload` | POST | Upload small file |
| `/api/v1/Files/chunked/init` | POST | Start chunked upload |
| `/api/v1/Files/chunked/{sessionId}/chunk` | POST | Upload chunk |
| `/api/v1/Files/chunked/{sessionId}/complete` | POST | Complete chunked upload |
| `/api/v1/Files` | GET | List files |
| `/api/v1/Files/{id}/download` | GET | Download file (or initiate retrieval) |
| `/api/v1/Files/retrieval/{retrievalId}/status` | GET | Check retrieval status |
| `/api/v1/Files/hash/check` | POST | Check for duplicates (P1 feature) |

---

## Examples

### Example: React Native Integration

```typescript
// 1. Provide this API_SPEC.md + docs/openapi.json to Claude/ChatGPT
// 2. Ask: "Generate a React Native hook for FlexStorage upload with thumbnail preview"

// Generated code:
import { useState } from 'react';
import * as FileSystem from 'expo-file-system';

export function useFlexStorage() {
  const API_KEY = 'your-api-key';
  const BASE_URL = 'https://api.flexstorage.com';

  async function uploadPhoto(uri: string, userId: string) {
    // LLM will generate complete implementation based on openapi.json
    // including error handling, retry logic, etc.
  }

  return { uploadPhoto };
}
```

---

## Changelog

### v1.0.0 (2025-01-15)
- ✅ Initial release
- ✅ File upload (small + chunked)
- ✅ Glacier storage integration
- ✅ Thumbnail generation (WebP 300×300)
- ✅ API Key authentication
- ✅ File retrieval workflow

### Upcoming (P1)
- 🔜 OAuth2 authentication
- 🔜 Batch hash comparison API
- 🔜 Backblaze B2 provider

---

## Support

- **Documentation:** This file + `docs/openapi.json`
- **Issues:** GitHub Issues
- **Email:** support@flexstorage.com (placeholder)

---

**Last Updated:** 2025-11-04
**OpenAPI Version:** 3.0.1
