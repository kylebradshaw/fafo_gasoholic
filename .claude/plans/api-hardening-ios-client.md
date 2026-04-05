# Plan: API Hardening + iOS Native Client

> Source PRD: `.claude/prd/api-hardening-ios-client.md`
> GitHub Issue: kylebradshaw/fafo_gasoholic#6

## Architectural Decisions

Durable decisions that apply across all phases:

- **Routes**: Auth endpoints remain at `/auth/` (unversioned). All data endpoints move from `/api/` to `/api/v1/`. No redirects from old paths — clients migrate simultaneously.
- **Auth model**: Session middleware removed entirely. All protected endpoints accept `Authorization: Bearer <access_token>`. Web stores access token in memory + refresh token in `httpOnly SameSite=Strict` cookie. iOS stores both tokens in Keychain.
- **JWT lifetimes**: Access token = 15 min TTL. Refresh token = 30 day TTL. Refresh tokens rotate on every use.
- **Schema — RefreshTokens table**: `Id`, `UserId` (FK, cascade delete), `TokenHash` (SHA-256, indexed), `CreatedAt`, `ExpiresAt`, `UsedAt` (nullable), `RevokedAt` (nullable), `DeviceHint` (nullable string).
- **Error shape**: All API errors use RFC 7807 Problem Details (`application/problem+json`) with `type`, `title`, `status`, `detail`. Validation errors include an `errors` extension property.
- **Rate limiting**: ASP.NET Core built-in `RateLimiter` middleware. Two tiers — auth tier (stricter, `/auth/`), API tier (standard, `/api/v1/`). Fixed window per IP. Config driven by environment variables.
- **OpenAPI**: .NET 9 built-in `Microsoft.AspNetCore.OpenApi`. Spec at `/openapi/v1.json`. Scalar UI at `/docs` (disabled or auth-gated in production).
- **Universal Links**: AASA file served at `/.well-known/apple-app-site-association`. Requires iOS bundle ID and Apple Developer Team ID before authoring.
- **iOS architecture**: `NetworkService` (URLSession wrapper), `KeychainService` (Security framework), `UniversalLinkHandler` (scene delegate). ViewModels use `@Observable`. Navigation uses `NavigationStack` with typed path.

---

## Phase 1: API Versioning + Problem Details

**User stories**: 11, 12, 13, 18, 19, 20

### What to build

Migrate all data endpoints from `/api/` to `/api/v1/` and standardize all error responses to RFC 7807 Problem Details. This is a non-auth-breaking change that establishes the stable API surface all future phases build on. The Angular service layer is updated simultaneously so both changes ship together.

Concretely: every `{ "error": "string" }` response in the backend becomes a Problem Details object. Every URL in the Angular services changes from `/api/` to `/api/v1/`. Old `/api/` paths return 404 — no redirects.

### Acceptance criteria

- [ ] All data endpoints respond at `/api/v1/autos`, `/api/v1/autos/{id}`, `/api/v1/autos/{autoId}/fillups`, `/api/v1/autos/{autoId}/fillups/{id}`
- [ ] Old `/api/` paths return 404
- [ ] All error responses return `Content-Type: application/problem+json` with `type`, `title`, `status`, `detail` fields
- [ ] Validation errors include an `errors` extension property with field-level detail
- [ ] Angular services target `/api/v1/` paths; app functionality is unchanged
- [ ] Existing integration tests updated to use `/api/v1/` paths and assert Problem Details shape
- [ ] New tests verify old `/api/` paths return 404
- [ ] New tests verify all error responses conform to Problem Details shape

---

## Phase 2: Rate Limiting + OpenAPI

**User stories**: 14, 15, 16, 17, 21, 22, 23

### What to build

Add two-tier rate limiting using ASP.NET Core's built-in `RateLimiter` middleware. The auth tier (applied to all `/auth/` endpoints) has a stricter limit than the API tier (applied to all `/api/v1/` endpoints). Rate-limited responses return HTTP 429 with a `Retry-After` header and a Problem Details body. Limits are configured via environment variables.

Separately, add OpenAPI documentation using .NET 9's built-in `Microsoft.AspNetCore.OpenApi`. All endpoints are annotated with request/response schemas and status codes. The Scalar UI is served at `/docs` and disabled in production.

### Acceptance criteria

- [ ] All `/auth/` endpoints enforce the auth-tier rate limit (stricter window/count)
- [ ] All `/api/v1/` endpoints enforce the API-tier rate limit
- [ ] Exceeding either limit returns HTTP 429 with a `Retry-After` header and Problem Details body
- [ ] Auth and API tier limits are independently configurable via environment variables
- [ ] Auth and API tier limits are enforced independently (hitting auth limit does not affect API limit)
- [ ] OpenAPI spec is served at `/openapi/v1.json` with all endpoints, request/response schemas, and status codes documented
- [ ] Scalar UI is available at `/docs` in development
- [ ] Scalar UI is disabled (or returns 404) when `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Integration tests verify 429 + `Retry-After` after limit is exceeded per IP
- [ ] Integration tests verify auth and API tiers have independent limits

---

## Phase 3: JWT Auth Backend

**User stories**: 1, 4, 5, 8, 9, 10

> **Deployment note**: Phases 3 and 4 must be deployed together. Session auth is removed entirely in this phase — deploying Phase 3 without Phase 4 breaks the Angular client.

### What to build

Replace session-based authentication with a JWT access + refresh token flow. Add the `RefreshTokens` table. Implement three new endpoints: `POST /auth/exchange` (converts a verified magic link token into a JWT pair), `POST /auth/refresh` (rotates a refresh token), and `POST /auth/revoke` (invalidates a refresh token). Update `GET /auth/verify` to issue a JWT pair in the response body instead of setting a session cookie. Remove session middleware from the app entirely. All protected endpoints accept `Authorization: Bearer <access_token>` instead of a session cookie.

Refresh tokens are stored as SHA-256 hashes (not plaintext). Rotation invalidates the old token on use. `VerificationCleanupService` is extended to also purge expired and used refresh tokens.

### Acceptance criteria

- [ ] `RefreshTokens` table exists with the specified schema; cascade deletes when User is deleted
- [ ] `POST /auth/exchange` with a valid magic link token returns `{ accessToken, refreshToken, expiresIn }` (200)
- [ ] `POST /auth/exchange` with an invalid, used, or expired token returns Problem Details 400
- [ ] `POST /auth/refresh` with a valid refresh token returns a new token pair and invalidates the old token (rotation)
- [ ] `POST /auth/refresh` reuse of an invalidated token returns Problem Details 400
- [ ] `POST /auth/refresh` with an expired or revoked token returns Problem Details 400
- [ ] `POST /auth/revoke` invalidates the given refresh token and returns 204
- [ ] All previously session-protected endpoints accept `Authorization: Bearer <access_token>`
- [ ] Expired access tokens (>15 min) are rejected with 401
- [ ] Session middleware is removed from the app
- [ ] Refresh token values stored as SHA-256 hashes, never plaintext
- [ ] `VerificationCleanupService` purges expired and used refresh tokens
- [ ] Integration tests cover all exchange/refresh/revoke scenarios and token expiry enforcement

---

## Phase 4: Angular JWT Client

**User stories**: 6, 7

> **Deployment note**: Must be deployed with Phase 3. Session auth is gone after Phase 3 ships.

### What to build

Update the Angular client to use token-based auth. `AuthService` is rewritten: it no longer calls `GET /auth/me` on load — instead it validates access token expiry locally from JWT claims and silently refreshes via the `httpOnly` refresh token cookie if needed. A new `JwtInterceptor` attaches `Authorization: Bearer <access_token>` to all API requests, intercepts 401 responses, attempts a silent token refresh, and retries the original request. On refresh failure, the user is redirected to `/login` and tokens are cleared. The refresh token is stored in an `httpOnly` cookie set by the server (inaccessible to JS). The access token is stored in a service-level memory variable (lost on page reload, triggers silent refresh via cookie).

### Acceptance criteria

- [ ] `JwtInterceptor` attaches `Authorization: Bearer` header to all outbound API requests
- [ ] A 401 response triggers a silent token refresh attempt before retrying the original request
- [ ] Successful silent refresh retries the original request transparently
- [ ] Failed silent refresh clears tokens and redirects to `/login`
- [ ] `AuthService` validates access token expiry from JWT claims without a network call
- [ ] Page reload triggers silent refresh via `httpOnly` cookie; user remains logged in if refresh token is valid
- [ ] Logging out calls `POST /auth/revoke` to invalidate the server-side refresh token
- [ ] App functionality (autos, fillups, auth) works end-to-end with JWT auth
- [ ] Angular unit tests verify Bearer header attachment, 401 retry, and redirect-on-failure behavior

---

## Phase 5: Universal Links Backend

**User stories**: 2, 3

> **Prerequisite**: iOS app bundle ID and Apple Developer Team ID must be known before this phase can be completed.

### What to build

Serve the Apple App Site Association (AASA) file at `/.well-known/apple-app-site-association` with the correct content type (`application/json`) and a structure that maps the magic link path to the iOS app bundle ID. This enables iOS to intercept magic link URLs and open the native app instead of Safari. Magic link emails are unchanged — the same URL works for both web (Safari redirect flow) and iOS (Universal Link interception → `/auth/exchange`).

### Acceptance criteria

- [ ] `GET /.well-known/apple-app-site-association` returns HTTP 200 with `Content-Type: application/json`
- [ ] AASA body contains the correct `applinks` configuration mapping the magic link path to the iOS bundle ID and Team ID
- [ ] File is served without authentication or rate limiting
- [ ] Integration test verifies the AASA endpoint returns 200, correct content type, and valid JSON structure

---

## Phase 6: iOS App — Authentication

**User stories**: 1, 2, 3, 4, 5, 24, 25, 26, 27

### What to build

Build the iOS app from scratch: Xcode project, Swift package dependencies, and the core infrastructure layer. Implement `KeychainService` (read/write/delete for access and refresh tokens), `NetworkService` (URLSession wrapper that attaches Bearer headers, detects 401s, triggers token refresh, retries, and returns typed `Result<T, APIError>` values), and `UniversalLinkHandler` (called from the scene delegate; parses the incoming magic link URL, extracts the token, calls `POST /auth/exchange`, stores the resulting tokens via `KeychainService`).

Build the authentication UI: a login screen with email input, a pending verification screen with a resend button, and the automatic post-link-tap redirect into the main app. Sessions persist for 30 days via the stored refresh token. Logout calls `POST /auth/revoke` and clears Keychain.

### Acceptance criteria

- [ ] `KeychainService` correctly reads, writes, and deletes access and refresh tokens from the iOS Keychain
- [ ] `NetworkService` attaches `Authorization: Bearer` to all requests; retries after silent refresh on 401; surfaces typed `APIError` values
- [ ] `UniversalLinkHandler` extracts the magic link token from the incoming URL, calls `/auth/exchange`, and stores tokens in Keychain
- [ ] Tapping a magic link on an iPhone opens the iOS app (not Safari) and completes login automatically
- [ ] Login screen accepts an email address and initiates the magic link flow
- [ ] Pending verification screen appears after email submission and offers a resend option
- [ ] Session persists across app restarts for up to 30 days without re-authentication
- [ ] Logout invalidates the refresh token server-side and clears all Keychain tokens
- [ ] Light and dark mode are both supported

---

## Phase 7: iOS App — Vehicles + Fillups

**User stories**: 28–42

### What to build

Build the full data screens of the iOS app. The autos list is the home screen, showing each vehicle's brand, model, plate, and latest fillup odometer. Autos can be added, edited, and deleted (with confirmation). Selecting an auto navigates to its fillups list in reverse chronological order. Each fillup row shows date, fuel type, price per gallon, gallons, odometer, and server-calculated MPG. Fillups can be logged (with optional location), edited, and deleted. All views use native SwiftUI navigation (`NavigationStack` with typed path), `@Observable` view models, and surface network errors as clear, actionable messages. Light and dark mode are supported throughout.

### Acceptance criteria

- [ ] Autos list displays brand, model, plate, and latest fillup odometer for each vehicle
- [ ] Add auto form accepts brand, model, plate, and current odometer; creates vehicle on submit
- [ ] Edit auto updates the vehicle details; delete auto removes it and all associated fillups
- [ ] Fillups list for a selected auto is ordered newest-first
- [ ] Each fillup row shows date, fuel type, price per gallon, gallons, odometer, and MPG
- [ ] Log fillup form accepts date/time, fuel type, price per gallon, gallons, odometer, partial fill flag, and optional location
- [ ] Edit fillup updates an existing entry; delete fillup removes it
- [ ] MPG is calculated server-side and displayed automatically (no client-side math)
- [ ] Network errors surface as clear, actionable messages (not raw status codes)
- [ ] Navigation uses `NavigationStack` with a typed path throughout
- [ ] All views support light and dark mode
- [ ] `AutosViewModel` and `FillupsViewModel` use `@Observable`
