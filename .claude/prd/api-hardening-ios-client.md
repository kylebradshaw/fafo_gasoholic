# PRD: API Hardening + iOS Native Client (Swift/SwiftUI)

> GitHub Issue: kylebradshaw/fafo_gasoholic#6

## Problem Statement

Gasoholic currently serves a single Angular PWA client through a session-cookie-based API. As the application grows to support a native iOS client alongside the existing web app, the current API has meaningful gaps: authentication is incompatible with native mobile apps, there is no versioning contract, no self-documenting interface, inconsistent error shapes, and limited rate limiting. Without addressing these, maintaining two clients against the same backend becomes brittle and error-prone.

## Solution

Harden the existing .NET Minimal API to serve as a durable, multi-client platform by:

1. Replacing session-based authentication with a JWT (access + refresh token) flow that works identically for both web and native iOS clients, using Universal Links to enable seamless magic-link deep linking into the iOS app
2. Migrating all API routes to `/api/v1/...` to establish a stable versioning contract
3. Adding global per-IP rate limiting across all endpoints
4. Standardizing all error responses to RFC 7807 Problem Details
5. Publishing OpenAPI documentation with a Scalar UI

Alongside the API hardening, build a native Swift/SwiftUI iOS application with full feature parity to the Angular web app.

## User Stories

### Authentication

1. As a user, I want to enter my email on the iOS app and receive a magic link, so that I can log in without a password
2. As a user, I want tapping the magic link on my iPhone to open the iOS app directly (not Safari), so that I don't have to leave the app to complete login
3. As a user, I want the iOS app to automatically complete login after I tap the magic link, so that the experience is seamless
4. As a user, I want my login session to persist on iOS for 30 days without re-authentication, so that I don't have to log in repeatedly
5. As a user, I want to log out of the iOS app and have my session fully invalidated server-side, so that my account is secure if I lose my phone
6. As a user, I want the same magic link flow on the web app as before, so that the web experience is unchanged
7. As a user, I want the web app to also use token-based auth, so that security is consistent across clients
8. As a developer, I want refresh tokens to rotate on every use, so that stolen refresh tokens are detectable and have limited blast radius
9. As a developer, I want access tokens to expire after 15 minutes, so that compromised tokens have a short window of validity
10. As a developer, I want all active refresh tokens to be revocable per user, so that I can respond to a security incident

### API Versioning

11. As a developer, I want all data endpoints to be prefixed with `/api/v1/`, so that future breaking changes can be introduced under `/api/v2/` without affecting existing clients
12. As a developer, I want the Angular web app to communicate with `/api/v1/` endpoints, so that both clients target the same versioned surface
13. As a developer, I want auth endpoints to remain at `/auth/` (unversioned), so that the authentication contract is stable and shared

### Rate Limiting

14. As a developer, I want all endpoints to enforce a global per-IP rate limit, so that the API is protected against abuse and brute-force attacks
15. As a developer, I want auth endpoints to have a stricter rate limit than data endpoints, so that authentication abuse is more constrained
16. As a developer, I want rate-limited responses to return HTTP 429 with a `Retry-After` header, so that clients can back off gracefully
17. As a user, I want to see a clear message when I've been rate limited, so that I know to wait before retrying

### Error Handling

18. As a developer, I want all API error responses to follow RFC 7807 Problem Details (`application/problem+json`), so that both iOS and web clients can handle errors with a single, predictable parsing strategy
19. As a developer, I want every error to include a `type`, `title`, `status`, and `detail` field, so that errors are machine-readable and human-readable simultaneously
20. As a developer, I want validation errors to include a structured `errors` extension in the Problem Details body, so that field-level errors are surfaced consistently

### OpenAPI Documentation

21. As a developer, I want a live OpenAPI spec served at `/openapi/v1.json`, so that I can generate client SDKs and validate request/response contracts
22. As a developer, I want a Scalar UI available at `/docs`, so that I can explore and test the API interactively without a separate tool
23. As a developer, I want all endpoints to be documented with request/response schemas and status codes, so that the API is self-describing

### iOS App — Authentication Views

24. As an iOS user, I want a login screen with an email input field, so that I can initiate the magic link flow
25. As an iOS user, I want a "pending verification" screen after submitting my email, so that I know to check my inbox
26. As an iOS user, I want a resend button on the pending screen, so that I can request a new magic link if the original expired
27. As an iOS user, I want to be automatically redirected to the main app after tapping the magic link, so that login completes without any manual steps

### iOS App — Vehicles (Autos)

28. As an iOS user, I want to see a list of my vehicles on the home screen, so that I can quickly navigate to the one I want
29. As an iOS user, I want each vehicle card to show the brand, model, plate, and latest fillup odometer, so that I have context at a glance
30. As an iOS user, I want to add a new vehicle by entering brand, model, plate, and current odometer, so that I can start tracking fuel for it
31. As an iOS user, I want to edit a vehicle's details, so that I can correct mistakes or update the plate
32. As an iOS user, I want to delete a vehicle and have all its fillups removed automatically, so that I can clean up vehicles I no longer own

### iOS App — Fillups

33. As an iOS user, I want to view all fillups for a selected vehicle in reverse chronological order, so that the most recent entry is always at the top
34. As an iOS user, I want each fillup row to show the date, fuel type, price per gallon, gallons, odometer, and calculated MPG, so that I have full context
35. As an iOS user, I want to log a new fillup by entering date/time, fuel type, price per gallon, gallons, odometer, and whether it was a partial fill, so that I can record every fueling event
36. As an iOS user, I want to optionally attach a location to a fillup, so that I can remember where I fueled up
37. As an iOS user, I want to edit an existing fillup, so that I can correct data entry errors
38. As an iOS user, I want to delete a fillup, so that I can remove incorrect entries
39. As an iOS user, I want the MPG to be calculated server-side and displayed automatically, so that I don't have to do math

### iOS App — General UX

40. As an iOS user, I want the app to use native SwiftUI navigation and components, so that it feels like a first-class iOS app
41. As an iOS user, I want to support both light and dark mode, so that the app respects my system preference
42. As an iOS user, I want network errors to surface as clear, actionable messages, so that I know what went wrong and what to do

## Implementation Decisions

### JWT Authentication

- Magic link verification (`GET /auth/verify?token=XXX`) issues a JWT access token (15 min TTL) and a refresh token (30 day TTL) in the response body for native clients
- New `POST /auth/exchange` endpoint: iOS app calls this with a verified magic link token to receive JWT pair — this is the Universal Link target for iOS
- New `POST /auth/refresh` endpoint: accepts a refresh token, returns a new access/refresh token pair (rotation — old token is invalidated on use)
- New `POST /auth/revoke` endpoint: invalidates a refresh token (logout for JWT clients)
- A new `RefreshToken` database table stores: userId, token hash (SHA-256, not plaintext), expiresAt, usedAt, revokedAt, deviceHint (optional)
- Web Angular app stores the access token in memory (JS variable) and the refresh token in an `httpOnly` `SameSite=Strict` cookie — never in localStorage
- iOS app stores both tokens in the iOS Keychain
- All protected endpoints accept `Authorization: Bearer <access_token>` instead of session cookie
- Session middleware is removed entirely

### Universal Links

- The backend serves `/.well-known/apple-app-site-association` (AASA) as a static JSON file mapping the magic link path to the iOS app bundle ID
- Magic link emails remain unchanged — the same URL works for both web (Safari opens, redirects to web app) and iOS (Universal Link intercepts, opens iOS app)
- iOS app registers the domain in its entitlements and handles the incoming URL in the app delegate to extract the token and call `/auth/exchange`
- **Prerequisite**: iOS app bundle ID and Apple Developer Team ID must be known before the AASA file can be authored

### API Versioning

- All data endpoints move from `/api/` to `/api/v1/`
- Auth endpoints remain at `/auth/` (no version prefix)
- The Angular app's service layer is updated to target `/api/v1/` paths
- No redirect from old paths — both clients migrate simultaneously

### Rate Limiting

- ASP.NET Core's built-in `RateLimiter` middleware (introduced in .NET 7) is used
- Two tiers:
  - **Auth tier** (stricter): applied to all `/auth/` endpoints
  - **API tier** (standard): applied to all `/api/v1/` endpoints
- Policy: fixed window per IP address
- 429 responses include a `Retry-After` header
- Rate limit configuration is environment-variable driven for tuning without redeployment

### RFC 7807 Problem Details

- `.AddProblemDetails()` registered in the DI container
- All existing `{ "error": "string" }` responses replaced with `TypedResults.Problem(...)`
- The `type` field uses application-defined URI identifiers (e.g., `https://gasoholic.app/errors/token-expired`)
- Validation errors include an `errors` extension property with field-level detail
- Both `application/json` and `application/problem+json` content types are handled

### OpenAPI Documentation

- Uses .NET 9's built-in `Microsoft.AspNetCore.OpenApi` package
- Scalar UI served at `/docs`
- OpenAPI spec available at `/openapi/v1.json`
- All endpoints annotated with `.WithOpenApi()`, `.Produces<T>()`, and `.ProducesProblem()` calls
- Auth endpoints documented but marked as not requiring JWT (for clarity)
- Scalar UI disabled or auth-gated in production

### Angular Client Changes

- New `JwtInterceptor` (HTTP interceptor) attaches `Authorization: Bearer` header to all API requests
- Interceptor handles 401 responses by attempting a silent token refresh before retrying the original request
- On refresh failure, user is redirected to `/login` and tokens are cleared
- `AuthService` rewritten: no longer calls `/auth/me` on load — instead validates access token expiry locally (JWT claims), refreshes if needed via the `httpOnly` cookie
- Refresh token is stored in an `httpOnly` cookie set by the server (not accessible to JS), eliminating XSS risk
- Access token stored in a service-level memory variable (lost on page reload, triggers silent refresh via cookie)

### iOS App Architecture

- **NetworkService**: thin URLSession wrapper responsible for attaching `Authorization` headers, detecting 401s, triggering token refresh, and retrying. Returns typed `Result<T, APIError>` values.
- **KeychainService**: abstraction over Security framework for reading/writing/deleting access and refresh tokens
- **UniversalLinkHandler**: called from the app's scene delegate; parses the incoming URL, extracts the magic link token, calls `/auth/exchange`, stores tokens via KeychainService
- **AuthViewModel**: drives login and pending screens; calls NetworkService
- **AutosViewModel**: loads, creates, updates, deletes autos; drives the autos list view
- **FillupsViewModel**: loads, creates, updates, deletes fillups for the selected auto; drives the fillups list and form views
- All ViewModels use `@Observable` (iOS 17 Observation framework)
- Navigation uses `NavigationStack` with a typed path

### Schema Changes

- New `RefreshTokens` table: `Id`, `UserId` (FK), `TokenHash` (string, indexed), `CreatedAt`, `ExpiresAt`, `UsedAt` (nullable), `RevokedAt` (nullable), `DeviceHint` (nullable string)
- Cascade delete: deleting a User deletes all RefreshTokens
- `VerificationCleanupService` extended to also purge expired and used refresh tokens

### API Contracts (new/changed endpoints)

```
POST /auth/exchange
  Body: { token: string }
  200: { accessToken: string, refreshToken: string, expiresIn: number }
  400: Problem Details (token_not_found | token_used | token_expired)

POST /auth/refresh
  Body: { refreshToken: string }
  200: { accessToken: string, refreshToken: string, expiresIn: number }
  400: Problem Details (token_invalid | token_expired | token_revoked)

POST /auth/revoke
  Body: { refreshToken: string }
  204: No Content

GET    /api/v1/autos                          (was /api/autos)
POST   /api/v1/autos                          (was /api/autos)
PUT    /api/v1/autos/{id}                     (was /api/autos/{id})
DELETE /api/v1/autos/{id}                     (was /api/autos/{id})

GET    /api/v1/autos/{autoId}/fillups         (was /api/autos/...)
POST   /api/v1/autos/{autoId}/fillups
PUT    /api/v1/autos/{autoId}/fillups/{id}
DELETE /api/v1/autos/{autoId}/fillups/{id}
```

## Testing Decisions

**What makes a good test:** Tests should verify observable API behavior (HTTP status codes, response shapes, side effects in the database) rather than implementation details. A test should fail when the behavior changes, not when the implementation is refactored.

**Modules to test:**

- **JWT Auth (backend)**: exchange endpoint (valid token, invalid, used, expired), refresh endpoint (valid rotation, invalid token, expired, revoked), revoke endpoint, token expiry enforcement on protected endpoints, 401 → refresh → retry flow
- **Rate Limiting (backend)**: verify 429 is returned after limit is exceeded per IP, verify `Retry-After` header is present, verify auth and API tiers have independent limits
- **Problem Details (backend)**: verify all error responses are `application/problem+json` with correct `type`, `title`, `status`, `detail` fields; verify validation errors include `errors` extension
- **API Versioning (backend)**: verify old `/api/` paths return 404, verify `/api/v1/` paths return expected responses
- **Universal Links (backend)**: verify AASA file is served at `/.well-known/apple-app-site-association` with correct content type and structure
- **RefreshToken rotation (backend)**: verify old refresh token is invalidated after use, verify reuse of an invalidated token returns an error
- **Angular JWT Interceptor**: verify Bearer header is attached, verify silent refresh on 401, verify redirect to login on refresh failure
- **iOS NetworkService**: verify token attachment, 401 retry logic, error mapping to typed `APIError`
- **iOS KeychainService**: verify read/write/delete for access and refresh tokens
- **iOS UniversalLinkHandler**: verify correct token extraction from URL, verify tokens stored after successful exchange

**Prior art for backend tests:** Existing `IntegrationTestBase` + `GasoholicWebAppFactory` pattern in `/Tests/`. New JWT auth tests follow the same shape as `AuthEndpointTests.cs`. The `MockEmailSender` pattern can be extended to capture issued tokens for test assertions.

## Out of Scope

- Offline sync / background sync queue for iOS (acknowledged as a future feature)
- Push notifications on iOS
- Sign in with Apple or any OAuth provider
- Android client
- Multi-device session management UI (viewing/revoking individual devices)
- API v2 or any breaking changes beyond the v1 migration
- Analytics, crash reporting, or observability instrumentation

## Further Notes

- The Angular web app must be updated alongside the backend changes — the two cannot be deployed independently mid-migration since session auth is being removed entirely
- The iOS app bundle ID and Apple Developer Team ID must be known before the AASA file can be authored; this is a hard prerequisite for Universal Links
- Refresh token hashes (not plaintext) should be stored using SHA-256 before persistence
- The `VerificationCleanupService` background service should be extended to also purge expired and used refresh tokens
- The Scalar UI at `/docs` should be disabled or auth-gated in production
