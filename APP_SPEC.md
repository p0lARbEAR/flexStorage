# FlexStorage Mobile App Specification

**Version:** 1.0.0 (Future Implementation)
**Platform:** iOS, Android (React Native or Native)
**Purpose:** iPhone Photos-like experience for automatic cloud archival
**Backend API:** FlexStorage REST API

---

## Overview

FlexStorage Mobile App provides a seamless, iPhone Photos-like experience for automatically backing up photos, videos, and files to cloud storage with easy retrieval. The app works in the background, handles interruptions gracefully, and makes cloud storage feel invisible to users.

---

## Core Principles

1. **Invisible & Automatic**: Upload happens in background without user intervention
2. **Smart**: Only upload on WiFi by default, pause on low battery
3. **Safe**: Never delete local files until confirmed in cloud
4. **Fast**: Show thumbnails instantly, even for archived files
5. **Reliable**: Resume interrupted uploads, handle network issues gracefully
6. **Private**: All data encrypted, biometric lock option

---

## Feature Priority

| Priority | Feature | Complexity | Status |
|----------|---------|------------|--------|
| P0 | Auto-upload photos/videos in background | High | ⬜ Not Started |
| P0 | Biometric authentication (Face ID/Touch ID) | Low | ⬜ Not Started |
| P0 | Photo gallery with thumbnails | Medium | ⬜ Not Started |
| P0 | Safe local deletion (hash verification) | Medium | ⬜ Not Started |
| P0 | Upload queue management | Medium | ⬜ Not Started |
| P1 | WiFi-only mode | Low | ⬜ Not Started |
| P1 | Low battery detection | Low | ⬜ Not Started |
| P1 | Upload progress tracking | Medium | ⬜ Not Started |
| P1 | File retrieval (from Glacier) | Medium | ⬜ Not Started |
| P1 | Search & filter | Medium | ⬜ Not Started |
| P2 | Manual tag addition | Low | ⬜ Not Started |
| P2 | Storage usage analytics | Low | ⬜ Not Started |
| P2 | Share files via public link | Medium | ⬜ Not Started |
| P3 | Offline mode (cached thumbnails) | Medium | ⬜ Not Started |
| P3 | Photo editing before upload | High | ⬜ Not Started |

---

## User Experience Flow

### First-Time Setup

```
1. Download app from App Store / Play Store
2. Tap "Get Started"
3. Sign in with Google / Apple / Email (OAuth2)
4. Grant photo library access permission
5. Choose upload preference:
   - WiFi only (recommended)
   - WiFi + Cellular
6. Enable biometric lock (optional but recommended)
7. App starts scanning photo library
8. Background upload begins
```

### Daily Usage (Automatic)

```
User takes photo with camera
    ↓
Photo saved to device gallery
    ↓
FlexStorage app detects new photo (background)
    ↓
Calculate SHA256 hash locally
    ↓
Check if already uploaded (via hash comparison API)
    ↓
    ├─→ [Already uploaded] Skip, mark as backed up
    └─→ [New file] Add to upload queue
            ↓
        Wait for WiFi (if WiFi-only mode)
            ↓
        Upload in background (chunked, resumable)
            ↓
        Show "Backed up" badge on photo
            ↓
        User can now "Free up space" (safe delete)
```

### Viewing Photos

```
Open app
    ↓
Gallery view with thumbnails (always instant)
    ↓
Tap photo to view
    ↓
    ├─→ [Cached locally] Show immediately
    └─→ [Deleted locally, in cloud]
            ↓
        Show thumbnail immediately
            ↓
        Tap "Download" or auto-download
            ↓
            ├─→ [In Backblaze/S3 Standard] Download in 2-5 seconds
            └─→ [In Glacier] Show "Retrieving... (3-5 hours)"
                    ↓
                Wait for retrieval
                    ↓
                Push notification when ready
                    ↓
                Download full resolution
```

---

## Key Features (Detailed)

### 1. Auto-Upload Engine

**Objective:** Upload all photos/videos automatically in background

**Implementation:**
- iOS: Background Fetch, Background Upload Session
- Android: WorkManager with constraints

**Logic:**
```
Every 15 minutes (or when photo taken):
1. Scan photo library for new items
2. For each new item:
   - Calculate SHA256 hash
   - Check upload status via API
   - If not uploaded:
     - Add to queue
3. Process queue:
   - Check WiFi (if required)
   - Check battery level (> 20%)
   - Check storage quota
   - Upload file (chunked for large files)
   - Verify upload success
   - Mark as backed up
```

**Features:**
- Pause/resume on network change
- Retry failed uploads (exponential backoff)
- Skip already uploaded files (deduplication)
- Show upload progress in notification (Android) or app badge (iOS)

---

### 2. Upload Queue Management

**Queue States:**
- **Pending**: Waiting for conditions (WiFi, battery)
- **Uploading**: Currently uploading
- **Paused**: User paused or waiting
- **Completed**: Successfully uploaded
- **Failed**: Error occurred, will retry

**UI:**
```
Settings → Upload Queue
┌────────────────────────────┐
│ Uploading (2 of 145)       │
│ ━━━━━━━━━━░░░░░░ 60%       │
├────────────────────────────┤
│ IMG_1234.jpg               │
│ 12.5 MB - 45% uploaded     │
│                            │
│ VID_5678.mp4              │
│ 120 MB - 15% uploaded      │
├────────────────────────────┤
│ Pending (143)              │
│ Waiting for WiFi...        │
├────────────────────────────┤
│ [Pause All] [Settings]     │
└────────────────────────────┘
```

---

### 3. Photo Gallery

**Layout:** Grid view (3 columns on phone, 5 on tablet)

**Each Thumbnail Shows:**
- Thumbnail image (WebP, cached locally)
- Cloud badge (✓) if uploaded
- Video duration badge (for videos)
- Selection checkbox (multi-select mode)

**Features:**
- Infinite scroll with pagination
- Fast scrolling (thumbnail caching)
- Filter by type (Photos, Videos, All)
- Filter by upload status (Backed up, Local only, All)
- Sort by date (newest first)
- Search by tags/filename

**Example:**
```
┌────────────────────────────────────┐
│  Photos (1,234)         [Search] ⚙️ │
├────────────────────────────────────┤
│  October 2025  (45 photos)         │
├──────────┬──────────┬──────────────┤
│ [📷 ✓]   │ [📷 ✓]   │ [📷]         │
│          │          │              │
├──────────┼──────────┼──────────────┤
│ [🎥 ✓]   │ [📷 ✓]   │ [📷 ✓]       │
│ 0:45     │          │              │
└──────────┴──────────┴──────────────┘

✓ = Backed up to cloud
📷 = Photo
🎥 = Video
```

---

### 4. Safe Local Deletion

**Objective:** Free up phone storage without losing files

**Flow:**
```
1. User taps "Free up space" in settings
2. App queries backend: POST /files/compare-hashes
   - Sends hashes of all local files
   - Backend returns which are safely uploaded
3. Show confirmation:
   "Delete 1,234 photos (5.2 GB) from this device?
    All files are safely stored in the cloud."
4. User confirms
5. Delete local files (only those marked safe)
6. Keep thumbnails cached locally
7. Show success: "5.2 GB freed"
```

**Safety Checks:**
- Only delete if status = "Archived" in backend
- Never delete if upload failed or in progress
- Keep local copy if user marked as "Keep on device"
- Option to keep recent photos (last 30 days)

---

### 5. File Retrieval & Download

**Scenarios:**

**A) File in Backblaze (Instant Access):**
```
Tap photo → Download starts → Shows in 2-5 seconds
```

**B) File in Glacier (Delayed Access):**
```
Tap photo
    ↓
Show message: "This photo is archived.
               Retrieval takes 3-5 hours."
    ↓
Options:
  - [Request Standard Retrieval] (3-5 hours, free)
  - [Request Expedited Retrieval] (5 minutes, $0.03)
    ↓
User taps "Request Standard Retrieval"
    ↓
API call: POST /files/{fileId}/retrieve
    ↓
Show in app: "Retrieving... Est. 3-5 hours"
    ↓
App polls every 30 minutes: GET /files/{fileId}/retrieve/{retId}
    ↓
When ready, push notification: "Your photo is ready!"
    ↓
User opens app, download starts automatically
```

**UI During Retrieval:**
```
┌────────────────────────────┐
│        [Thumbnail]         │
│                            │
│  🔄 Retrieving from cloud  │
│                            │
│  Estimated: 3 hours        │
│  Started: 2:30 PM          │
│                            │
│  We'll notify you when     │
│  it's ready to download.   │
│                            │
│  [Cancel Request]          │
└────────────────────────────┘
```

---

### 6. Authentication & Security

**OAuth2 Flow:**
```
1. App opens authorization URL in browser
2. User logs in with Google/Apple/Email
3. Browser redirects to app: flexstorage://oauth/callback?code=...
4. App exchanges code for tokens:
   - access_token (1 hour)
   - refresh_token (90 days)
5. Store refresh_token in secure keychain
6. Store access_token in memory only
7. Automatically refresh before expiry
```

**Biometric Lock:**
- Required on app launch
- Uses Face ID (iOS) or Fingerprint (Android)
- Fallback to PIN if biometric fails
- Optional setting (enabled by default)

**Token Management:**
- Refresh token silently in background
- Handle token expiration gracefully
- Re-authenticate if refresh token expired
- Logout: revoke tokens via API

---

### 7. Settings

```
┌────────────────────────────────┐
│ Settings                       │
├────────────────────────────────┤
│ 📱 Account                     │
│    user@example.com            │
│    [Manage Account] [Logout]   │
├────────────────────────────────┤
│ ☁️ Upload Preferences          │
│    ✓ WiFi only                 │
│    ⚪ WiFi + Cellular           │
│    ✓ Pause on low battery      │
│    ✓ Upload videos             │
├────────────────────────────────┤
│ 💾 Storage                     │
│    Used: 15.2 GB / 50 GB       │
│    [View Details]              │
│    [Free up space]             │
├────────────────────────────────┤
│ 🔒 Security                    │
│    ✓ Biometric lock            │
│    [Change PIN]                │
├────────────────────────────────┤
│ 🔔 Notifications               │
│    ✓ Upload completed          │
│    ✓ Retrieval ready           │
│    ✓ Storage full warning      │
├────────────────────────────────┤
│ ⚙️ Advanced                    │
│    [Upload Queue]              │
│    [Cache Management]          │
│    [Developer Options]         │
└────────────────────────────────┘
```

---

### 8. Storage Usage Analytics

**Dashboard:**
```
┌────────────────────────────────┐
│ Storage Usage                  │
├────────────────────────────────┤
│  ━━━━━━━━━━━━░░░░ 30.4 / 50 GB │
├────────────────────────────────┤
│ Photos:  1,234  (12.5 GB)      │
│ Videos:    145  (17.8 GB)      │
│ Other:      12  (0.1 GB)       │
├────────────────────────────────┤
│ By Provider:                   │
│ S3 Glacier Deep:  20.0 GB      │
│ Backblaze:         8.5 GB      │
│ S3 Glacier Flex:   1.9 GB      │
├────────────────────────────────┤
│ This Month:                    │
│ Uploaded: 2.3 GB (45 files)    │
│ Downloaded: 0.5 GB (12 files)  │
└────────────────────────────────┘
```

---

### 9. Search & Filters

**Search Bar:**
- Search by filename
- Search by tags (auto-generated + user tags)
- Search by date ("October 2025", "Last week")
- Search by location (if GPS data)

**Filters:**
- Type: Photos, Videos, Screenshots, All
- Status: Backed up, Local only, All
- Date range picker
- Size: Small, Medium, Large

**Example Search:**
```
Search: "beach vacation 2025"

Results (23):
┌────────────────────────────────┐
│ [📷] beach_sunset_1.jpg        │
│ Tags: beach, sunset, vacation  │
│ Oct 15, 2025 • 3.2 MB • ✓     │
├────────────────────────────────┤
│ [🎥] beach_video.mp4           │
│ Tags: beach, ocean, vacation   │
│ Oct 15, 2025 • 45 MB • ✓      │
└────────────────────────────────┘
```

---

### 10. Offline Mode

**Features:**
- View thumbnails (always cached)
- Browse gallery
- Search cached metadata
- Queue files for upload (when online)
- Mark files for download (when online)

**Cached Data:**
- Thumbnails (WebP, max 5000 most recent)
- File metadata (filename, tags, date, size)
- Upload queue state

**Not Available Offline:**
- Full resolution download
- Upload (queued for later)
- Retrieval requests
- Sharing

---

## Technical Implementation Notes

### Background Upload (iOS)

```swift
// Use URLSession with background configuration
let config = URLSessionConfiguration.background(
    withIdentifier: "com.flexstorage.upload"
)
let session = URLSession(configuration: config, delegate: self)

// Upload task continues even when app terminated
let task = session.uploadTask(with: request, fromFile: fileURL)
task.resume()

// Handle completion in AppDelegate
func application(_ application: UIApplication,
                 handleEventsForBackgroundURLSession identifier: String,
                 completionHandler: @escaping () -> Void)
```

### Background Upload (Android)

```kotlin
// Use WorkManager with constraints
val uploadWork = OneTimeWorkRequestBuilder<UploadWorker>()
    .setConstraints(
        Constraints.Builder()
            .setRequiredNetworkType(NetworkType.WIFI)
            .setRequiresBatteryNotLow(true)
            .build()
    )
    .setBackoffCriteria(
        BackoffPolicy.EXPONENTIAL,
        WorkRequest.MIN_BACKOFF_MILLIS,
        TimeUnit.MILLISECONDS
    )
    .build()

WorkManager.getInstance(context).enqueue(uploadWork)
```

### Token Refresh

```typescript
// Automatic token refresh before expiry
class TokenManager {
  private accessToken: string;
  private refreshToken: string;
  private expiresAt: Date;

  async getAccessToken(): Promise<string> {
    // Refresh if expires in < 5 minutes
    if (this.expiresAt.getTime() - Date.now() < 5 * 60 * 1000) {
      await this.refreshAccessToken();
    }
    return this.accessToken;
  }

  async refreshAccessToken() {
    const response = await fetch('/auth/refresh', {
      method: 'POST',
      body: JSON.stringify({
        grant_type: 'refresh_token',
        refresh_token: this.refreshToken
      })
    });
    const data = await response.json();
    this.accessToken = data.access_token;
    this.refreshToken = data.refresh_token; // token rotation
    this.expiresAt = new Date(Date.now() + data.expires_in * 1000);
    await this.saveToKeychain();
  }
}
```

### Hash Calculation

```typescript
// Calculate SHA256 before upload
import crypto from 'crypto';

async function calculateFileHash(fileUri: string): Promise<string> {
  const fileData = await RNFS.readFile(fileUri, 'base64');
  const hash = crypto
    .createHash('sha256')
    .update(fileData, 'base64')
    .digest('hex');
  return `sha256:${hash}`;
}
```

### Chunked Upload

```typescript
async function uploadFileChunked(
  file: File,
  uploadId: string,
  chunkSize = 5 * 1024 * 1024 // 5MB
) {
  const totalChunks = Math.ceil(file.size / chunkSize);

  for (let i = 0; i < totalChunks; i++) {
    const start = i * chunkSize;
    const end = Math.min(start + chunkSize, file.size);
    const chunk = file.slice(start, end);

    // Calculate MD5 for chunk
    const md5 = await calculateMD5(chunk);

    // Upload chunk
    await fetch(`/files/upload/${uploadId}/parts/${i + 1}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/octet-stream',
        'Content-MD5': md5,
      },
      body: chunk
    });

    // Update progress
    const progress = ((i + 1) / totalChunks) * 100;
    updateProgress(uploadId, progress);
  }

  // Complete upload
  await fetch(`/files/upload/${uploadId}/complete`, {
    method: 'POST'
  });
}
```

---

## API Integration

### Required Backend Endpoints

**Authentication:**
- `POST /auth/token` - Initial login
- `POST /auth/refresh` - Refresh token
- `POST /auth/revoke` - Logout

**Upload:**
- `POST /files/upload/initiate` - Start chunked upload
- `PUT /files/upload/{uploadId}/parts/{partNumber}` - Upload chunk
- `POST /files/upload/{uploadId}/complete` - Complete upload
- `GET /files/upload/{uploadId}` - Resume upload
- `DELETE /files/upload/{uploadId}` - Cancel upload

**Retrieval:**
- `GET /files/{fileId}` - Get metadata
- `GET /files/{fileId}/thumbnail?size=medium` - Get thumbnail
- `POST /files/{fileId}/retrieve` - Initiate Glacier retrieval
- `GET /files/{fileId}/retrieve/{retId}` - Check retrieval status
- `GET /files/{fileId}/download` - Download file

**Management:**
- `GET /files?page=1&pageSize=50` - List files
- `GET /files/search?q=beach` - Search files
- `POST /files/compare-hashes` - Batch hash comparison
- `GET /files/sync-status?since=...` - Sync status
- `DELETE /files/{fileId}` - Delete file

**Quota:**
- `GET /users/me/quota` - Get quota info
- `GET /users/me/stats` - Get usage stats

---

## Push Notifications

**Events to Notify:**
1. "Upload completed" - All pending files uploaded
2. "Retrieval ready" - Glacier file ready to download
3. "Storage 80% full" - Approaching quota limit
4. "Upload failed" - Repeated upload failures

**Payload Example:**
```json
{
  "type": "retrieval.ready",
  "title": "Your photo is ready!",
  "body": "IMG_1234.jpg is ready to download",
  "data": {
    "fileId": "file_3nB8vC1kL",
    "retrievalId": "ret_9kL3mN7pQ"
  }
}
```

---

## Future Enhancements (Phase 2+)

1. **Collaborative Albums** - Share albums with family
2. **AI Search** - "Show me photos with dogs"
3. **Photo Editing** - Basic crop, filters before upload
4. **Video Transcoding** - Stream videos without full download
5. **Timeline View** - Auto-group by events
6. **Desktop App** - macOS/Windows client
7. **Web App** - Browser-based access
8. **Apple Photos Extension** - Integrate with native Photos app
9. **Smart Suggestions** - "Delete blurry photos to save space"
10. **Family Sharing** - Shared storage quota

---

## Success Metrics

**User Engagement:**
- Daily Active Users (DAU)
- Photos uploaded per user per day
- Storage usage growth

**Performance:**
- Upload success rate > 95%
- Average upload time < 30 seconds per photo
- Thumbnail load time < 200ms

**Reliability:**
- App crash rate < 0.1%
- API error rate < 1%
- Background upload success rate > 90%

**User Satisfaction:**
- App Store rating > 4.5 stars
- Net Promoter Score (NPS) > 40
- User retention (30-day) > 60%

---

**Last Updated:** 2025-10-25
**Document Owner:** FlexStorage Team
**Status:** Specification Only - Implementation TBD
