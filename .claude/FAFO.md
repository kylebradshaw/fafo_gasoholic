# FAFO.md — Session Interaction Log

This file is appended with every user interaction in each Claude Code session.

---

## Session: 2026-03-11

- Ran `/init` — Claude analyzed the (empty) repo and created `.claude/CLAUDE.md` as a placeholder
- User described the project: .NET ASP.NET Core API + Vanilla HTML/JS frontend for tracking gas fillups; requested an interview to build a spec
- Interview Q1: Multi-auto → **Multiple autos with a selector dropdown**
- Interview Q1: Fuel types → **Predefined list** (Regular, Mid-grade, Premium, Diesel, E85)
- Interview Q1: Computed stats → **MPG per fillup row**; user noted trend analysis is desired in the future
- Interview Q1: .NET version → **.NET 10, local only**
- Interview Q2: Fillup CRUD → **Create, Edit, Delete, Mark partial fill** (all four)
- Interview Q2: Location field → **GPS auto-fill** (browser geolocation → Nominatim reverse geocode)
- Interview Q2: Log UX → **No sorting/filtering — newest first**
- Interview Q2: UI style → **Clean / utilitarian**
- User noted (via `/btw`): plan should be structured as todo lists executable via `/loop`; each task must have testable acceptance criteria before it can be closed
- Interview Q3: Ralph loop → **/loop skill invocation**
- Interview Q3: Auth/session → **Cookie-based session**
- Plan written to `/Users/kb/.claude/plans/wise-doodling-pinwheel.md` — 10 tasks covering scaffold → data model → auth → autos API → fillups API → login page → app shell → fillup log → add/edit modal → polish
- User asked how to kick off plan execution — explained `/loop` invocation pattern
- User requested git commit after each task's acceptance criteria pass — plan updated; each task now has a commit checkpoint as its final criterion with format `feat: task N — <description>`
- User requested creation of `FAFO.md` to log all session interactions as a running bulleted list
- User moved the plan file from the global plans directory to `.claude/SPEC.md` within the project; `CLAUDE.md` updated to reference it
- User renamed `.claude/SPEC.md` → `.claude/PLAN.md` as a more accurate name; `CLAUDE.md` reference updated
- User moved `FAFO.md` from repo root into `.claude/FAFO.md`; `CLAUDE.md` reference added
- User asked what command to type to execute `PLAN.md` — provided `/loop` invocation
- User asked what the `10m` interval means — clarified it's the delay between iterations; recommended `1m` for this sequential task workflow
- User ran `/loop 1m` to execute `PLAN.md` tasks sequentially — cron job `f34aa792` scheduled at `*/1 * * * *`, auto-expires after 3 days

---

## Session: 2026-03-23

- User reported dissatisfaction with first render speed and having to log in every time on the deployed app at `gas.sdir.cc`
- Interview: user uses the app at the gas pump on iOS Safari — needs it fast; also sees login loss on Edge desktop
- Interview: cold start is the main render issue; user approved `minReplicas: 1` (~$15/mo) to keep container warm
- Task 17 drafted in `PLAN.md`: **Speed Up User Experience** — three fixes: warm replica, session persistence, faster frontend render
- **Task 17 completed** (`feat: task 17`):
  - `infra/main.bicep`: `minReplicas: 0` → `1` — eliminates 5-10s cold start
  - `Program.cs`: added `DefaultSlidingExpiration = 30 days` on SQL Server distributed session cache (was defaulting to 20 min, causing premature session eviction despite 30-day cookie); added explicit `SameSite = Lax`
  - `wwwroot/index.html`: login form renders immediately; `/auth/me` check runs non-blocking in background
  - `wwwroot/app.html`: app shell + shimmer skeleton loading states render instantly; auth check and `/api/autos` fire in parallel via `Promise.all` (was sequential waterfall)
- User requested FAFO.md logging after every plan task completion; added instruction to CLAUDE.md

## Session: 2026-03-23

- User reported magic link emails landing in spam. Root cause: ACS Azure-managed domain (`DoNotReply@<generated>.azurecomm.net`) has low trust with email providers.
- Interview: user owns `sdir.cc`, has DNS access, 1-10 emails/day, wants to stay on ACS free tier, mixed provider targets (Gmail/Outlook), wants sender on host domain.
- Decision: ACS custom domain (`gas.sdir.cc`) with SPF/DKIM/DMARC — minimal code change, stays in Azure. Resend is the fallback if deliverability remains poor.
- **Task 18 drafted in `PLAN.md`:** ACS Custom Domain for Email Deliverability — Bicep changes (CustomerManaged domain), sender address update (`verify@gas.sdir.cc`), `/health` email config reporting, smoke test email verification steps, test-email endpoint.
- **DEPLOYMENT.md updated:** Added full "Email Domain Setup (Task 18)" section with 6 manual steps (deploy infra → get DNS records from portal → add DNS records + DMARC → verify in portal → deploy app → verify delivery), plus troubleshooting guide.
- **Task 18 implemented** (`feat: task 18`):
  - `infra/main.bicep`: changed `acsDomain` from `AzureManaged`/`AzureManagedDomain` to `CustomerManaged`/`gas.sdir.cc`
  - `Services/VerificationEmailSender.cs`: sender prefix changed from `DoNotReply@` to `verify@`; added `IsConfigured`, `SenderDomain`, `SenderAddress` properties; added `SendTestEmailAsync()` method
  - `Services/IVerificationEmailSender.cs`: interface extended with email config properties and test-email method
  - `Program.cs`: `/health` endpoint now returns `email.configured`, `email.senderDomain`, `email.senderAddress`
  - `Endpoints/SmokeTestEndpoints.cs`: added `POST /auth/test-email` endpoint (gated by `SMOKE_TEST_SECRET`) that sends a real test email via ACS
  - `smoke-test.sh`: added step 1b (email config check via /health) and step 3b (test email send via /auth/test-email)
  - Build passes, Playwright e2e tests pass (18/21 — same 3 pre-existing failures as before)
  - Remaining manual checks: ACS domain verification in Azure Portal, inbox delivery test on Gmail/Outlook
- **Task 18 continued:** Two-phase deploy fix for ACS custom domain (can't link unverified domain). Added `useCustomEmailDomain` Bicep param, `--custom-email-domain` deploy.sh flag, kept Azure-managed domain as fallback during verification. Fixed email URL to use `https://gas.sdir.cc` via `UseForwardedHeaders` middleware. Added `verify` sender username via `az communication email domain sender-username create`.
- **Task 19 implemented** (`feat: task 19`):
  - `Models/User.cs`: added `DateTime? LastSignIn` and `DateTime? LastInteraction` properties
  - `Endpoints/AuthEndpoints.cs`: `/auth/verify` sets `LastSignIn`; `/auth/me` sets `LastInteraction` once per session via `interactionLogged` session flag
  - `Endpoints/SmokeTestEndpoints.cs`: `/auth/dev-login` sets `LastSignIn` on both new and existing user paths
  - Migration `AddSignInTracking` adds two nullable columns to `Users` table

## Session: 2026-03-23

- **Bug fix: login immediately logs out on production (gas.sdir.cc)**
  - Root cause: `ForwardedHeaders` middleware (added in task 19) only trusts proxies on localhost by default. Azure Container Apps reverse proxy IP isn't localhost, so `X-Forwarded-Proto: https` was silently ignored. The app thought all requests were HTTP, and since `Cookie.SecurePolicy = Always` in production, session cookies were never sent back by the browser.
  - Fix: `Program.cs` — clear `KnownIPNetworks` and `KnownProxies` on `ForwardedHeadersOptions` so Azure's proxy is trusted.
  - Saved feedback memory: smoke tests must always be run against the live deployment during the verification phase of plan tasks.
- **Bug fix: `datetime2 is incompatible with text` — migration generated for wrong provider**
  - Root cause: `dotnet ef migrations add` was run without `DATABASE_PROVIDER=sqlserver`, so EF Core used SQLite provider and hardcoded `type: "TEXT"` for DateTime columns. On SQL Server, `text` is a deprecated LOB type incompatible with `datetime2` parameters.
  - Fix 1: Created migration `FixSignInColumnTypes` that detects TEXT columns on SQL Server and drops/recreates them as `datetime2(7)`.
  - Fix 2: Added try-catch safety net in `/auth/me` and `/auth/verify` so activity tracking writes can't crash login.
  - Fix 3: `ForwardedHeaders` now clears `KnownIPNetworks`/`KnownProxies` so Azure's reverse proxy is trusted.
  - Lesson: Always generate migrations with `DATABASE_PROVIDER=sqlserver` when targeting SQL Server.

### Task 20 — Smoke Test User Cleanup

- **`Endpoints/SmokeTestEndpoints.cs`** — Added `DELETE /auth/dev-cleanup` endpoint: gated by `X-Smoke-Test-Secret`, validates email matches `@example.com` or `@test.com`, deletes user (cascade handles autos/fillups/tokens), returns 204/404/400/403 as appropriate.
- **`e2e/helpers/auth.ts`** — Added `cleanupUser(api, email)` helper; silently succeeds on 404 (idempotent).
- **`e2e/smoke/happy-path.spec.ts`** — Added `afterAll` that calls `cleanupUser` for the test email; fixed `selector-smoke` test to clean up its own unique email inline; added 3 regression tests: reject non-test domain (400), reject wrong secret (403), verify delete+second-delete returns 404.

## Session: 2026-03-26

- User requested comprehensive TECH STACK section in README.md for GitHub visibility
- Analysis of project components completed:
  - Backend: .NET 10.0.102, ASP.NET Core Minimal API, EF Core 10.0.4/10.0.5, SQLite/SQL Server dual support, Azure Communication Email 1.0.1
  - Frontend: Vanilla HTML/CSS/JS, PWA support, responsive mobile-first design
  - Testing: Playwright 1.52.0 E2E tests, 67 C# integration test cases
  - Infrastructure: Docker, Azure Key Vault, session-based auth, CORS, Forwarded Headers
- **README.md updated** with new "## Tech Stack" section containing 5 organized tables:
  1. Backend technologies with versions & purposes
  2. Frontend approach & capabilities
  3. Testing & QA tools
  4. Infrastructure & DevOps components
  5. Architecture patterns highlighting Minimal APIs, DI, migrations, background services
