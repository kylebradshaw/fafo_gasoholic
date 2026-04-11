# PRD: Require Magic Link for Every Session

## Problem Statement

The current authentication flow has a security gap: once a user's `EmailVerified` flag is set to `true` (after clicking their first magic link), any subsequent login only requires submitting the user's email address — no magic link, no proof of identity. Anyone who knows or guesses a verified user's email can establish a full session. The magic link verification was intended to prove email ownership, but it only does so once and then becomes a permanent bypass.

## Solution

Require a magic link for every new session establishment, not just first-time verification. The `EmailVerified` flag remains as a "has this email ever been confirmed" marker (essentially a sign-up flag), but it no longer grants instant session access. The flow becomes:

1. User submits their email to `/auth/login`
2. If they already have a valid session cookie → return OK immediately (no email sent)
3. If no valid session and `EmailVerified == true` → send a magic link to re-authenticate, return `pending_reauth` status so the frontend can show "Check your email to sign back in"
4. If no valid session and `EmailVerified == false` → send a magic link for first-time verification, return `pending_verification` status so the frontend can show "Check your email to verify your account"
5. User clicks the magic link → session established for 30 days
6. After 30 days (or cookie cleared), the user must go through the magic link flow again

## User Stories

1. As a verified user with an active session, I want to access the app without re-authenticating, so that I don't have to check my email every time I open the app
2. As a verified user whose session has expired, I want to receive a magic link when I submit my email, so that only someone with access to my email can log in
3. As a verified user re-authenticating, I want to see a message like "Check your email to sign back in" (distinct from first-time verification wording), so that I understand why I'm being asked to verify again
4. As a new user signing up, I want to receive a magic link to verify my email, so that my account is tied to a real email address I own
5. As a new user, I want to see a message like "Check your email to verify your account," so that I understand what's happening
6. As a user who clicked a valid magic link, I want to be redirected to `/app.html` with a 30-day session, regardless of whether this was first-time verification or re-authentication
7. As a user, I want my session to last 30 days before I'm required to re-authenticate, so that I'm not constantly interrupted by email verification
8. As an attacker who knows a user's email, I should NOT be able to establish a session without access to that user's email inbox
9. As a verified user re-authenticating, I want to be able to use `/auth/resend` to get a new magic link if I didn't receive the first one
10. As a user, I want rate limiting to prevent abuse of the magic link sending mechanism

## Implementation Decisions

- **`EmailVerified` flag**: Retained as a sign-up marker. It indicates "this email has been verified at least once" but no longer bypasses authentication.
- **Login endpoint behavior change**: When `EmailVerified == true` and no active session exists, the endpoint sends a magic link and returns `202 Accepted` with `status: "pending_reauth"` instead of immediately setting a session and returning `200 OK`.
- **New status values**: The `/auth/login` response gains a new `status` value:
  - `"ok"` — session already active or just established (no change)
  - `"pending_verification"` — first-time user, magic link sent (replaces current `"pending"`)
  - `"pending_reauth"` — verified user, session expired, magic link sent (new)
- **Resend endpoint**: Should work for both unverified users and verified users who are re-authenticating (have an active unused token but no session). The existing rate limiting (4 tokens/hour) applies to both cases.
- **Session duration**: Already configured at 30 days via cookie settings in `Program.cs` — no change needed there.
- **Active session check**: The login endpoint should check for an existing valid session (via `Session.GetInt32("userId")`) before sending a magic link. If a session exists and the user is verified, return `"ok"` immediately.
- **Cleanup service**: Stays at 7-day cutoff for unverified users and expired tokens. No change needed.
- **Dev-login endpoint**: `/auth/dev-login` (smoke test endpoint) continues to bypass magic link — it's gated by `SMOKE_TEST_SECRET` header and only used in tests and smoke tests.
- **Verify endpoint**: No changes to `/auth/verify` — it already sets `EmailVerified = true`, establishes a session, and redirects to `/app.html`.
- **Frontend**: Must handle the new `pending_reauth` status and display appropriate messaging. The `pending_verification` status replaces the current `pending` status.

## Testing Decisions

Tests should verify external behavior (HTTP status codes, response bodies, session state) — not internal implementation details. The existing `AuthEndpointTests` pattern of using `CreateClient()` with cookie persistence and `MockEmailSender` for inspecting sent magic links is the right approach.

### Modules to test (via `AuthEndpointTests`):

- **Login with verified user, no session**: Should return `202` with `status: "pending_reauth"` and send a magic link (this is the key new behavior — the existing test `Login_AlreadyVerifiedUser_SetsSessionAndReturnsOk` must be updated)
- **Login with verified user, active session**: Should return `200` with `status: "ok"` and NOT send a magic link
- **Login with new user**: Should return `202` with `status: "pending_verification"` and send a magic link
- **Login with unverified user with active token**: Should return `202` with `status: "pending_verification"` and NOT send another magic link (existing behavior)
- **Full re-auth flow**: Login → get magic link → verify → session works → logout → login again → must get new magic link
- **Resend for verified user re-authenticating**: Should work and be subject to rate limiting
- **Existing verify, logout, and /auth/me tests**: Should continue to pass

### Prior art:

- `Tests/AuthEndpointTests.cs` — existing auth integration tests using `GasoholicWebAppFactory` and `MockEmailSender`
- `Tests/IntegrationTestBase.cs` — base class with `CreateClient()` and `CreateAuthenticatedClientAsync()` helpers

## Out of Scope

- Changing session duration (already 30 days)
- Changing magic link expiration (stays at 24 hours)
- Changing the email template wording (works for both cases)
- Changing the cleanup service schedule
- Adding password-based authentication
- Two-factor authentication
- OAuth/social login

## Further Notes

- This is a breaking change for existing verified users: after deployment, any verified user without an active session will need to click a magic link on their next visit. This is expected and acceptable.
- The `pending` status value used today should be replaced with `pending_verification` for clarity. If the frontend currently checks for `"pending"`, it will need to be updated to handle both new values.
