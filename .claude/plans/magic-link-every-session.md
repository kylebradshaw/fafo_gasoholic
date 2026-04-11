# Plan: Require Magic Link for Every Session

> Source PRD: `.claude/prd/magic-link-every-session.md`

## Architectural decisions

Durable decisions that apply across all phases:

- **`EmailVerified` flag**: Retained as a sign-up marker ("has this email ever been verified"). No longer grants instant session access.
- **Session duration**: 30 days, already configured in `Program.cs` — no change needed
- **Magic link expiration**: 24 hours — no change needed
- **Status values from `/auth/login`**:
  - `"ok"` — active session exists, user is authenticated
  - `"pending_verification"` — new user, magic link sent (replaces current `"pending"`)
  - `"pending_reauth"` — verified user, session expired, magic link sent (new)
- **`/auth/verify` endpoint**: No changes — already sets `EmailVerified = true`, establishes session, redirects to `/app`
- **`/auth/dev-login` endpoint**: No changes — continues to bypass magic link, gated by `SMOKE_TEST_SECRET`
- **Cleanup service**: Stays at 7-day cutoff — no change needed
- **Rate limiting**: Existing 4 tokens/hour limit applies to both verification and re-auth flows

---

## Phase 1: Backend — require magic link for verified users without a session

**User stories**: 2, 4, 7, 8

### What to build

Change `/auth/login` so that verified users without an active session receive a magic link instead of getting an instant session. The endpoint should check for an existing valid session first — if present, return `200 OK` with `status: "ok"`. If no session and `EmailVerified == true`, generate a token, send a magic link, and return `202 Accepted` with `status: "pending_reauth"`. If no session and `EmailVerified == false`, behave as today but return `status: "pending_verification"` instead of `"pending"`.

### Acceptance criteria

- [x] Verified user without a session: `/auth/login` returns `202` with `status: "pending_reauth"` and sends a magic link
- [x] Verified user with an active session: `/auth/login` returns `200` with `status: "ok"` and does NOT send a magic link
- [x] New user: `/auth/login` returns `202` with `status: "pending_verification"` and sends a magic link
- [x] Unverified user with an active token: `/auth/login` returns `202` with `status: "pending_verification"` and does NOT send another magic link
- [x] Clicking the magic link from a re-auth flow establishes a session and redirects to `/app`

---

## Phase 2: Backend — resend support for re-authenticating users + tests

**User stories**: 9, 10

### What to build

Update `/auth/resend` to work for verified users who are re-authenticating (have a pending unused token but no session). Currently it returns `200 OK` for verified users to avoid user enumeration — instead, it should check for an active unused token and resend if one exists (or generate a new one). The existing rate limit (4 tokens/hour) applies. Update all `AuthEndpointTests` to cover the new status values and the full re-auth lifecycle (login → magic link → verify → session works → logout → login again → must get new magic link).

### Acceptance criteria

- [x] `/auth/resend` for a verified user with a pending token: sends a new magic link and returns `202`
- [x] `/auth/resend` rate limiting applies to verified users re-authenticating
- [x] Existing test `Login_AlreadyVerifiedUser_SetsSessionAndReturnsOk` updated to expect `202` with `pending_reauth`
- [x] New test: verified user with active session returns `200 OK` without sending a magic link
- [x] New test: full re-auth lifecycle (login → verify → logout → login again → requires magic link)
- [x] New test: resend works for verified user re-authenticating
- [x] All existing verify, logout, and `/auth/me` tests continue to pass

---

## Phase 3: Frontend — handle new auth statuses and messaging

**User stories**: 1, 3, 5, 6

### What to build

Update `AuthService.login()` to handle the `202` response (currently throws because it only expects `200`). Return the status value so the login component can differentiate. Update `LoginComponent` to show different pending messages: "Check your email to sign back in" for `pending_reauth` vs "Check your email to verify your account" for `pending_verification`.

### Acceptance criteria

- [x] `AuthService.login()` handles `202` responses and exposes the status value (`pending_verification` or `pending_reauth`)
- [x] `LoginComponent` shows "Check your email to verify your account" for `pending_verification`
- [x] `LoginComponent` shows "Check your email to sign back in" for `pending_reauth`
- [x] Verified user with an active session: login redirects to app immediately (no pending screen)
- [x] Resend button works from both pending states
- [x] "Use a different email" link works from both pending states
