# OAuth2 Authentication Design (P1 Feature)

**Status:** Foundation Created
**Priority:** P1 (Essential Features)
**Target:** Production-ready authentication

---

## Overview

OAuth2 authentication replaces the simple API Key authentication (P0) with production-grade security. Supports multiple identity providers and mobile app integration.

---

## Architecture

### Grant Types
1. **Authorization Code Grant** - For initial user login
2. **Refresh Token Grant** - For silent token renewal

### Token Lifecycle
- **Access Token**: 1 hour expiry, JWT format
- **Refresh Token**: 90 days expiry, rotates on each refresh
- **Token Storage**: Database (RefreshTokens table)

---

## Supported Providers

### 1. Google OAuth2
- **Client ID/Secret**: From Google Cloud Console
- **Scopes**: `openid`, `profile`, `email`
- **Redirect URI**: `https://api.flexstorage.com/auth/google/callback`
- **Mobile Redirect**: `flexstorage://oauth/callback`

### 2. Apple Sign In
- **Team ID**: From Apple Developer Account
- **Service ID**: Configured in Apple portal
- **Scopes**: `name`, `email`
- **JWT-based authentication**

### 3. Email/Password (Future)
- **Option A**: Use IdentityServer
- **Option B**: Use Auth0
- **Option C**: Custom implementation with ASP.NET Core Identity

---

## Implementation Steps

### Phase 1: Infrastructure (Current)
- [x] Create `IOAuth2Service` interface
- [x] Create `OAuth2TokenResponse` DTO
- [ ] Create `RefreshToken` entity (Domain layer)
- [ ] Create `RefreshTokenRepository` (Infrastructure layer)
- [ ] Add JWT configuration to `appsettings.json`

### Phase 2: Google OAuth2 Integration
- [ ] Install `Microsoft.AspNetCore.Authentication.Google` NuGet package
- [ ] Install `Microsoft.AspNetCore.Authentication.JwtBearer` NuGet package
- [ ] Create `OAuth2Service` implementation
- [ ] Add Google OAuth configuration to Program.cs
- [ ] Create `AuthController` with OAuth endpoints
- [ ] Write tests for OAuth2Service

### Phase 3: Token Management
- [ ] Implement JWT token generation
- [ ] Implement refresh token rotation
- [ ] Add token revocation logic
- [ ] Create middleware for JWT validation
- [ ] Update existing endpoints to use OAuth2

### Phase 4: Mobile App Support
- [ ] Configure custom URI scheme (`flexstorage://`)
- [ ] Add mobile-specific OAuth flow
- [ ] Test with iOS and Android apps

### Phase 5: Apple Sign In
- [ ] Install Apple Sign In libraries
- [ ] Configure Apple Developer account
- [ ] Implement Apple-specific flow
- [ ] Handle JWT from Apple

---

## Database Schema

### RefreshTokens Table
```sql
CREATE TABLE RefreshTokens (
    Id UUID PRIMARY KEY,
    UserId UUID NOT NULL REFERENCES Users(Id),
    Token VARCHAR(500) NOT NULL UNIQUE,
    Provider VARCHAR(50) NOT NULL, -- Google, Apple, Email
    CreatedAt TIMESTAMP NOT NULL,
    ExpiresAt TIMESTAMP NOT NULL,
    IsRevoked BOOLEAN DEFAULT FALSE,
    RevokedAt TIMESTAMP NULL,
    ReplacedByToken VARCHAR(500) NULL -- For token rotation
);

CREATE INDEX IX_RefreshTokens_UserId ON RefreshTokens(UserId);
CREATE INDEX IX_RefreshTokens_Token ON RefreshTokens(Token);
CREATE INDEX IX_RefreshTokens_ExpiresAt ON RefreshTokens(ExpiresAt);
```

### Users Table Enhancement
```sql
ALTER TABLE Users ADD COLUMN Email VARCHAR(255) NULL;
ALTER TABLE Users ADD COLUMN DisplayName VARCHAR(255) NULL;
ALTER TABLE Users ADD COLUMN Provider VARCHAR(50) NULL;
ALTER TABLE Users ADD COLUMN ProviderUserId VARCHAR(255) NULL;

CREATE INDEX IX_Users_Email ON Users(Email);
CREATE INDEX IX_Users_Provider_ProviderUserId ON Users(Provider, ProviderUserId);
```

---

## Configuration (appsettings.json)

```json
{
  "Authentication": {
    "JWT": {
      "SecretKey": "your-256-bit-secret-key-here-min-32-chars",
      "Issuer": "https://api.flexstorage.com",
      "Audience": "https://app.flexstorage.com",
      "AccessTokenExpirationMinutes": 60,
      "RefreshTokenExpirationDays": 90
    },
    "Google": {
      "ClientId": "your-google-client-id",
      "ClientSecret": "your-google-client-secret",
      "RedirectUri": "https://api.flexstorage.com/auth/google/callback",
      "MobileRedirectUri": "flexstorage://oauth/callback"
    },
    "Apple": {
      "ClientId": "com.flexstorage.service",
      "TeamId": "your-apple-team-id",
      "KeyId": "your-apple-key-id",
      "PrivateKey": "path-to-apple-private-key.p8"
    }
  }
}
```

---

## API Endpoints

### 1. Get Authorization URL
```http
GET /api/v1/auth/authorize?provider=google&redirect_uri=flexstorage://oauth/callback&state=random_state
```
**Response:**
```json
{
  "authorizationUrl": "https://accounts.google.com/o/oauth2/v2/auth?client_id=...&redirect_uri=...&response_type=code&scope=openid%20profile%20email&state=random_state"
}
```

### 2. Exchange Code for Tokens
```http
POST /api/v1/auth/token
Content-Type: application/json

{
  "authorizationCode": "4/0AY0e-g7...",
  "provider": "google",
  "redirectUri": "flexstorage://oauth/callback"
}
```
**Response:**
```json
{
  "success": true,
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "8c3a2b1f-4e5d-6a7b-8c9d-0e1f2a3b4c5d",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "provider": "google",
  "email": "user@example.com",
  "displayName": "John Doe"
}
```

### 3. Refresh Access Token
```http
POST /api/v1/auth/refresh
Content-Type: application/json

{
  "refreshToken": "8c3a2b1f-4e5d-6a7b-8c9d-0e1f2a3b4c5d"
}
```
**Response:** Same as Exchange Code response with new tokens

### 4. Revoke Tokens (Logout)
```http
POST /api/v1/auth/revoke
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```
**Response:**
```json
{
  "success": true,
  "message": "Tokens revoked successfully"
}
```

---

## Security Considerations

### Token Security
- [ ] Use HTTPS only
- [ ] Store refresh tokens hashed in database (SHA256)
- [ ] Implement token rotation (invalidate old refresh token on use)
- [ ] Add rate limiting on token endpoints
- [ ] Validate state parameter to prevent CSRF
- [ ] Use short-lived access tokens (1 hour)

### Mobile App Security
- [ ] Use PKCE (Proof Key for Code Exchange) for mobile apps
- [ ] Validate redirect URIs strictly
- [ ] Implement biometric lock on app side
- [ ] Store tokens in secure keychain/keystore

---

## Testing Strategy

### Unit Tests
- [ ] OAuth2Service token generation
- [ ] Token validation logic
- [ ] Refresh token rotation
- [ ] Token revocation

### Integration Tests
- [ ] Complete OAuth flow with mocked providers
- [ ] Token refresh flow
- [ ] Concurrent token refresh handling
- [ ] Expired token handling

### E2E Tests (Manual)
- [ ] Google OAuth flow
- [ ] Apple Sign In flow
- [ ] Mobile app redirect flow
- [ ] Token expiration and refresh

---

## Migration from API Key to OAuth2

### Transition Period
1. Support both API Key and OAuth2 simultaneously
2. Add `[AllowApiKeyOrOAuth2]` attribute to endpoints
3. Gradually migrate users to OAuth2
4. Deprecate API Key authentication after migration

### Middleware Order
```csharp
app.UseHttpsRedirection();
app.UseAuthentication();  // JWT validation
app.UseApiKeyAuthentication();  // Fallback to API Key
app.UseAuthorization();
app.MapControllers();
```

---

## Dependencies

### NuGet Packages
```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.Google" Version="8.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="7.0.0" />
```

### Optional (for Apple Sign In)
```xml
<PackageReference Include="AppleAuth.NET" Version="2.0.0" />
```

---

## Next Steps

1. **Immediate**: Create RefreshToken domain entity
2. **Short-term**: Implement OAuth2Service with Google provider
3. **Medium-term**: Add JWT middleware and AuthController
4. **Long-term**: Add Apple Sign In and migrate from API Keys

---

**Document Version:** 1.0
**Last Updated:** 2025-10-27
**Owner:** FlexStorage Backend Team
