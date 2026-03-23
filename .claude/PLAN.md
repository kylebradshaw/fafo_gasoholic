# Gasoholic — App Spec & Execution Plan

## Context

Building a local-only .NET 10 ASP.NET Core API + Vanilla HTML/JS frontend for tracking vehicle fuel fillups. The app is demo-grade: email-only login (no password), cookie-based sessions, SQLite via EF Core. Users can own multiple autos, each with its own fillup log showing MPG computed per entry.

This plan is structured as sequential todo items with acceptance criteria. Each task is optimized for use with the ralph-loop plugin.

**How to run all remaining tasks sequentially:**
```
/ralph-loop "$(cat .claude/PLAN.md)" --completion-promise "TESTS COMPLETE"
```

**Loop behavior:** Each iteration, find the first task that still has unchecked `[ ]` acceptance criteria. Implement it fully, check off each criterion as it passes, write or update its Playwright e2e test, then create the git commit. Do NOT output the completion promise until every task (1–10) has all criteria checked `[x]` and all commits exist.

---

## Stack

| Layer | Choice |
|---|---|
| Runtime | .NET 10 |
| API style | ASP.NET Core Minimal API |
| ORM | EF Core 10 + SQLite provider |
| Auth | Cookie-based session (`AddSession` + in-memory or DB-backed) |
| Frontend | Vanilla HTML + CSS + JS (no build step, no framework) |
| RWD | CSS Grid / Flexbox, mobile-first |

---

## Data Model

### Users
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| Email | string | unique, login key |

### Autos
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| UserId | int FK → Users | |
| Brand | string | e.g. Toyota |
| Model | string | e.g. Camry |
| Plate | string | license plate |
| Odometer | decimal | current odometer reading |

### Fillups
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| AutoId | int FK → Autos | |
| FilledAt | DateTime | date + time of fillup |
| Location | string? | station name / address from reverse geocode |
| Latitude | double? | from browser GPS |
| Longitude | double? | from browser GPS |
| FuelType | enum | Regular, MidGrade, Premium, Diesel, E85 |
| PricePerGallon | decimal | |
| Gallons | decimal | |
| Odometer | decimal | at time of fillup |
| IsPartialFill | bool | tank not filled to full |

**Computed (not stored):** `MPG = (this.Odometer - prev.Odometer) / this.Gallons` — only meaningful when `IsPartialFill = false` on the current entry.

---

## API Endpoints

```
POST   /auth/login           { email }           → 200 + Set-Cookie
POST   /auth/logout                              → 200 + clears cookie
GET    /auth/me                                  → { email }

GET    /api/autos                                → Auto[]
POST   /api/autos            { brand, model, plate, odometer }
PUT    /api/autos/{id}       { brand, model, plate, odometer }
DELETE /api/autos/{id}

GET    /api/autos/{autoId}/fillups               → FillupRow[] (newest first, includes mpg)
POST   /api/autos/{autoId}/fillups   { ... }
PUT    /api/autos/{autoId}/fillups/{id}  { ... }
DELETE /api/autos/{autoId}/fillups/{id}
```

---

## Frontend Pages

1. **`/` (index.html)** — Login: email input + submit → redirect to `/app.html`
2. **`/app.html`** — Shell: nav with auto selector dropdown + logout link
   - **Log tab** (default): fillup table, newest first, `+ Add Fillup` button
   - **Autos tab**: auto cards with edit/delete, `+ Add Auto` button
3. **Modals / inline forms** for add/edit of both Autos and Fillups

### Fillup Table Columns
`Date/Time | Location | Fuel Type | $/gal | Gal | Odometer | MPG*`

`*` shows `—` for partial fills or when no prior full fill exists.

### GPS flow
Browser `navigator.geolocation.getCurrentPosition()` → coordinates stored → reverse geocode via browser-side call to `https://nominatim.openstreetmap.org/reverse` → populate Location field as display hint (editable).

---

## Execution Todo List

> Each task requires all acceptance criteria to pass before closing.

---

### Task 1 — Project Scaffold

**Work:**
- `dotnet new webapi -n Gasoholic --no-openapi` inside repo root
- Add packages: `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`
- Configure `Program.cs`: add session middleware, CORS for `localhost` dev, static file serving from `wwwroot/`
- Add `appsettings.json` with SQLite connection string pointing to `gasoholic.db`

**Acceptance criteria:**
- [x] `dotnet run` starts without errors
- [x] `curl http://localhost:5000/` returns 200 or 404 (not 500)
- [x] `gasoholic.db` file is created on first run (empty schema OK at this stage)
- [x] git commit created: `feat: task 1 — project scaffold`

**Task done when:** all criteria above are checked `[x]` and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 2 — EF Core Data Model + Migrations

**Work:**
- Create `Models/` folder: `User.cs`, `Auto.cs`, `Fillup.cs` with all columns from spec
- Create `Data/AppDbContext.cs` with all three `DbSet<>` and unique index on `User.Email`
- Add `FuelType` enum to `Models/Enums.cs`
- Run `dotnet ef migrations add InitialCreate` and `dotnet ef database update`

**Acceptance criteria:**
- [x] `dotnet ef migrations list` shows `InitialCreate` as applied
- [x] SQLite DB contains tables `Users`, `Autos`, `Fillups` with correct columns (verify via `sqlite3 gasoholic.db .schema`)
- [x] No EF warnings about unmapped properties at startup
- [x] git commit created: `feat: task 2 — data model and migrations`

**Task done when:** all criteria above are checked `[x]` and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 3 — Auth Endpoints

**Work:**
- `POST /auth/login`: look up or create `User` by email, store `userId` in session, return 200
- `POST /auth/logout`: clear session, return 200
- `GET /auth/me`: return `{ email }` if session valid, else 401
- Add `RequireAuth` helper / filter reused by all `/api/*` routes

**Acceptance criteria:**
- [x] `POST /auth/login` with `{ "email": "test@test.com" }` returns 200 and `Set-Cookie` header
- [x] `GET /auth/me` with that cookie returns `{ "email": "test@test.com" }`
- [x] `GET /auth/me` without cookie returns 401
- [x] `POST /auth/logout` clears session; subsequent `GET /auth/me` returns 401
- [x] git commit created: `feat: task 3 — auth endpoints`

**Task done when:** all criteria above are checked `[x]` and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 4 — Autos API

**Work:**
- `GET /api/autos` — return all autos for session user
- `POST /api/autos` — create auto, link to session user
- `PUT /api/autos/{id}` — update; 403 if not owner
- `DELETE /api/autos/{id}` — delete (cascade fillups); 403 if not owner

**Acceptance criteria:**
- [x] POST creates an auto; GET returns it in the list
- [x] PUT updates brand/model/plate/odometer
- [x] DELETE removes the auto and returns 204
- [x] A second user (different session) cannot PUT/DELETE another user's auto (returns 403)
- [x] git commit created: `feat: task 4 — autos API`

**Task done when:** all criteria above are checked `[x]` and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 5 — Fillups API

**Work:**
- `GET /api/autos/{autoId}/fillups` — return fillups newest-first; compute `mpg` per row
- `POST /api/autos/{autoId}/fillups` — create fillup
- `PUT /api/autos/{autoId}/fillups/{id}` — update
- `DELETE /api/autos/{autoId}/fillups/{id}` — delete

**MPG computation logic (server-side):**
- Sort all fillups for auto by `Odometer` ascending
- For each fillup, find the most recent prior full fill (`IsPartialFill = false`)
- `mpg = (this.Odometer - prior.Odometer) / sum of gallons between them` — or null if no prior full fill

**Acceptance criteria:**
- [x] POST fillup, then GET returns it as first row
- [x] Two consecutive full fills: GET shows correct MPG on second entry
- [x] Partial fill shows `null` mpg in response
- [x] DELETE fillup returns 204; it no longer appears in GET
- [x] Auto must belong to session user; otherwise 403
- [x] git commit created: `feat: task 5 — fillups API with MPG`

**Task done when:** all criteria above are checked `[x]` (verified via Playwright e2e test or curl) and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 6 — Login Page (`wwwroot/index.html`)

**Work:**
- Clean single-column centered form: email input + "Sign In" button
- On submit: `POST /auth/login`, then redirect to `/app.html`
- On load: `GET /auth/me` — if already logged in, redirect immediately
- RWD: looks correct on 375px and 1280px

**Acceptance criteria:**
- [x] Entering email and clicking Sign In redirects to `/app.html`
- [x] Visiting `index.html` when already logged in skips to `/app.html`
- [x] Form is usable on mobile (375px width) without horizontal scroll
- [x] git commit created: `feat: task 6 — login page`

**Task done when:** all criteria above are checked `[x]` (verified via Playwright e2e test) and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 7 — App Shell + Autos Management (`wwwroot/app.html`)

**Work:**
- Nav bar: app name, auto selector `<select>`, logout button
- Two tabs: "Log" and "Autos"
- **Autos tab**: list of user's autos as cards (brand + model + plate + odometer); each card has Edit and Delete buttons; `+ Add Auto` opens modal form
- Modal form: brand, model, plate, odometer fields; Save and Cancel
- On logout: `POST /auth/logout` → redirect to `index.html`
- On load: `GET /auth/me` → if 401 redirect to `index.html`

**Acceptance criteria:**
- [x] Adding an auto via modal appears in the list and in the nav selector
- [x] Editing an auto updates the card
- [x] Deleting an auto removes it from list and selector
- [x] Logout clears session and redirects to login
- [x] Unauthenticated visit to `/app.html` redirects to `index.html`
- [x] git commit created: `feat: task 7 — app shell and autos management`

**Task done when:** all criteria above are checked `[x]` (verified via Playwright e2e test) and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 8 — Fillup Log Tab

**Work:**
- When an auto is selected: `GET /api/autos/{autoId}/fillups` → render table
- Table columns: `Date/Time | Location | Fuel Type | $/gal | Gal | Odometer | MPG`
- MPG cell: display value to 1 decimal, or `—` if null
- `+ Add Fillup` button opens add-fillup modal
- Each row: Edit (pencil icon) and Delete (trash icon)
- Auto selector change reloads the table for the newly selected auto

**Acceptance criteria:**
- [x] Table renders all fillups for selected auto, newest first
- [x] Switching auto selector reloads the table with correct data
- [x] MPG shows computed value or `—` correctly
- [x] Delete row removes it without page reload
- [x] Table is scrollable/readable on 375px mobile
- [x] git commit created: `feat: task 8 — fillup log tab`

**Task done when:** all criteria above are checked `[x]` (verified via Playwright e2e test) and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 9 — Add/Edit Fillup Modal + GPS

**Work:**
- Modal fields: Date (date input, default today), Time (time input, default now), Location (text, pre-populated by GPS), Fuel Type (`<select>`: Regular, Mid-grade, Premium, Diesel, E85), Price/gal (number), Gallons (number), Odometer (number), Partial Fill (checkbox)
- On modal open: call `navigator.geolocation.getCurrentPosition()` → POST coords to reverse-geocode via Nominatim → populate Location field; show spinner while loading; gracefully handle denial
- On save: POST or PUT to API, close modal, refresh table

**Acceptance criteria:**
- [x] Opening the modal triggers GPS prompt; Location populates with a human-readable string if permission granted
- [x] If GPS denied, Location field is empty and editable
- [x] Saving a new fillup appends it to the top of the log
- [x] Editing an existing fillup updates the row in place
- [x] All required fields validated client-side before submit (no blank price/gal/odometer)
- [x] git commit created: `feat: task 9 — add/edit fillup modal with GPS`

**Task done when:** all criteria above are checked `[x]` (verified via Playwright e2e test) and the git commit exists — then move on to the next task with unchecked criteria.

---

### Task 10 — Polish & RWD Pass

**Work:**
- Ensure consistent spacing, typography, and color on all pages
- Verify table horizontal scroll on narrow viewports
- Add basic loading states (skeleton or spinner) during API calls
- Ensure modal is full-screen on mobile, centered card on desktop
- Test happy path end-to-end: login → add auto → add 3 fillups → verify MPG → edit → delete

**Acceptance criteria:**
- [x] No layout breakage at 375px, 768px, 1280px
- [x] End-to-end happy path completes without errors in browser console
- [x] All API errors surface a visible (non-alert) error message to user
- [x] `dotnet run` + open browser → fully functional with zero manual setup steps beyond that
- [x] git commit created: `feat: task 10 — polish and RWD pass`

**Completion signal:** When all acceptance criteria above are satisfied (verified via Playwright e2e test covering the full happy path) and the git commit exists, output exactly: `<promise>TESTS COMPLETE</promise>`

---

### Task 11 — Azure Cloud Deployment (Infrastructure & Code)

**Goal:** Make the app deployable to Azure Container Apps with Azure SQL Database, configurable via environment variables, with GitHub Actions CI/CD and Bicep IaC. *(Architecture pivoted from App Service to Container Apps due to new-account App Service quota restrictions.)*

**Key cloud migration problems solved:**

| Problem | Local state | Cloud fix |
|---|---|---|
| SQLite on ephemeral disk | Works fine | Switch to Azure SQL Database in prod |
| In-memory session cache | Works fine | DB-backed distributed session (`Microsoft.Extensions.Caching.SqlServer`) |
| Hardcoded CORS origins | `localhost` only | `CORS_ORIGINS` env var |
| `EnsureCreated()` for DB init | OK for dev | `Database.Migrate()` on startup |
| No secrets management | `appsettings.json` | Azure Key Vault + managed identity |

**Actual architecture delivered:**

1. **Multi-provider database** — `DATABASE_PROVIDER` env var switches between `sqlite` (dev) and `sqlserver` (prod); connection strings keyed `DefaultConnection` / `SqlServer`
2. **DB-backed session** — `AddDistributedSqlServerCache` in prod, `AddDistributedMemoryCache` in dev
3. **Configurable CORS** — `CORS_ORIGINS` env var, comma-separated
4. **Dockerfile** — multi-stage build with `BUILDPLATFORM`/`TARGETPLATFORM` BuildKit ARGs to support Apple Silicon natively; exposes port 8080
5. **Bicep IaC (`infra/main.bicep`)** — provisions: ACR (Basic), Azure SQL (Basic 5 DTU), Key Vault (RBAC, managed identity secret pull), Container Apps Environment (consumption, scales to zero), Container App (system-assigned identity, ACR pull, KV secret ref). Primary region East US; SQL falls back to East US 2 only when East US is at capacity.
6. **GitHub Actions (`.github/workflows/azure-deploy.yml`)** — push to `main` triggers: build image → push to ACR → `az containerapp update`
7. **`deploy.sh`** — single CLI script covering full provision + image deploy; `--infra-only` / `--app-only` flags; reads/writes `.deploy-secrets` (gitignored)

**Acceptance criteria:**
- [x] `docker buildx build --platform linux/arm64 -t gasoholic --load .` succeeds
- [x] `docker run --platform linux/arm64 -e DATABASE_PROVIDER=sqlite -e "ConnectionStrings__DefaultConnection=Data Source=/tmp/gasoholic.db" -p 8080:8080 gasoholic` starts and `/health` returns `{"status":"ok"}`
- [x] `infra/main.bicep` passes `az bicep build` validation without errors
- [x] GitHub Actions workflow YAML is valid (manual review)
- [x] `CORS_ORIGINS` env var correctly gates allowed origins
- [x] `Database.Migrate()` is called on startup (not `EnsureCreated`)
- [x] `DATABASE_PROVIDER=sqlserver` path compiles and wires SQL Server provider + DB-backed session
- [x] `deploy.sh` is executable and documents all steps via `--help` or inline comments
- [x] `.deploy-secrets` and `infra/main.json` are gitignored
- [x] git commit created: `feat: task 11 — azure cloud deployment`

**Completion signal:** When all acceptance criteria above are checked `[x]` and the git commit exists, output exactly: `<promise>TESTS COMPLETE</promise>`

---

### Task 12 — Container Deployment Verification

**Goal:** Execute `deploy.sh` against a clean Azure resource group, verify the live Container App serves real traffic, and confirm GitHub Actions CI/CD completes a full push-to-deploy cycle.

**Pre-conditions:**
- `gasoholic-rg` resource group exists in East US (user-created)
- Azure CLI authenticated (`az login`)
- `AZURE_CREDENTIALS` secret set in GitHub repo settings
- Any zombie resources from prior failed attempts are cleaned up before starting

**Work:**

1. **Clean state** — delete any partial resources in `gasoholic-rg` that conflict (SQL servers, old ACR, orphaned Container Apps environments)
2. **Full provision** — run `./deploy.sh` end-to-end; capture outputs (app URL, ACR server, SQL FQDN)
3. **DB migration** — confirm `deploy.sh` ran `dotnet ef database update` and created `SessionCache` table against Azure SQL
4. **Smoke test** — verify live app URL serves the login page and `/auth/me` returns 401
5. **CI/CD round-trip** — make a trivial code change, push to `main`, confirm GitHub Actions workflow completes and new image is live
6. **Update `AZURE.md`** — record the confirmed live URL and any operational notes

**Architecture pivot during Task 12:**
Azure Files SMB (port 445) is blocked on Container Apps consumption plan — investigated and ruled out.
Switched to SQLite at `/tmp/gasoholic.db` (ephemeral per revision). Azure SQL also blocked on new account
quota. Bicep simplified to: ACR + Container Apps Environment + Container App only. All resources East US.

**Acceptance criteria:**
- [x] `./deploy.sh` completes without error from a clean state
- [x] `curl https://gasoholic.yellowcliff-a9ca470c.eastus.azurecontainerapps.io/health` returns `{"status":"ok"}`
- [x] `curl https://gasoholic.yellowcliff-a9ca470c.eastus.azurecontainerapps.io/auth/me` returns HTTP 401
- [x] Login page renders at the live URL; login → add auto → add fillup all work
- [x] SQLite `/tmp/gasoholic.db` creates tables `Users`, `Autos`, `Fillups`, `__EFMigrationsHistory` on startup via `Database.Migrate()` (verified via successful login API call)
- [x] GitHub Actions run triggered by push to `main` completed successfully in 1m12s (run 23410895395)
- [x] New image tag `98abe06a...` deployed by CI/CD and reflected in Container App revision
- [x] `.claude/AZURE.md` updated with confirmed live URL and deployment date (2026-03-22)
- [x] git commit created: `feat: task 12 — container deployment verification`

**Completion signal:** When all acceptance criteria above are checked `[x]` and the git commit exists, output exactly: `<promise>TESTS COMPLETE</promise>`

---

### Task 13 — Email Verification via Azure Communication Services

**Goal:** Require users to verify their email address before gaining access. On login, a magic link is emailed via ACS; clicking it creates a 30-day persistent session. Existing users are pre-verified. Hard gate: unverified users see only a holding page.

**Key decisions:**
| Concern | Choice |
|---|---|
| Email provider | Azure Communication Services (ACS) — free tier, 2,000 emails/mo |
| Mechanism | Magic link (one-time URL, 24hr expiry) |
| Session model | Hard gate — no session until verified; 30-day persistent cookie after |
| Existing users | Auto-marked verified on migration (no re-verification required) |
| Rate limiting | Max 3 resend requests per hour per email address |
| Stale cleanup | Unverified users with no activity purged after 7 days |

---

**Work:**

1. **Data model**
   - Add `EmailVerified bool` column to `Users` (default `true` — migration auto-verifies all existing rows)
   - Add `VerificationTokens` table: `Id int PK`, `UserId int FK`, `Token string` (GUID, unique index), `CreatedAt datetime`, `ExpiresAt datetime` (24hr), `UsedAt datetime?`
   - New EF migration: `AddEmailVerification`

2. **Azure Communication Services (ACS) provisioning**
   - Add to `infra/main.bicep`:
     - `Microsoft.Communication/emailServices` (free tier) in `eastus`
     - `Microsoft.Communication/emailServices/domains` with `domainManagement: 'AzureManaged'` — zero DNS config, Azure provides the sending domain
     - ACS connection string stored in Key Vault as secret `AcsConnection`
     - New `secretRef` in Container App pointing to `AcsConnection`
   - Add env var `ConnectionStrings__ACS` to Container App (from KV secret)
   - `deploy.sh`: add ACS connection string extraction step after Bicep outputs

3. **Email sending service**
   - Add package `Azure.Communication.Email`
   - Register `EmailClient` from `ConnectionStrings__ACS` (no-op stub when env var absent — local dev skips email)
   - `IVerificationEmailSender` interface with `SendMagicLinkAsync(email, token)`:
     - In prod: uses ACS `EmailClient.SendAsync` with from address on ACS managed domain
     - In dev (no ACS connection string): writes magic link to console/log instead

4. **Auth flow changes**
   - `POST /auth/login` — modified:
     - Look up or create user
     - If `EmailVerified = false` and user was just created or token is expired: generate new `VerificationToken` (GUID), save, call `IVerificationEmailSender.SendMagicLinkAsync`
     - If `EmailVerified = true`: create session as before
     - If `EmailVerified = false` and valid token exists: return 202 `{"status":"pending"}` (don't resend automatically)
     - Response shape: `{"status": "ok"}` (verified) or `{"status": "pending"}` (check email)
   - `GET /auth/verify?token=<guid>` — new endpoint:
     - Look up token; reject if not found, expired (`ExpiresAt < now`), or already used (`UsedAt != null`)
     - Mark token `UsedAt = now`, set `User.EmailVerified = true`
     - Create session; set cookie `SameSite=Strict, HttpOnly, Secure, MaxAge=30days` (persistent)
     - Redirect to `/app.html`
   - `POST /auth/resend` — new endpoint:
     - Body: `{ email }`
     - Rate limit: count tokens created for this email in last 1hr; if ≥ 3, return 429 `{"error":"too_many_requests"}`
     - Otherwise: generate new token, invalidate previous unused tokens for this user, send email
   - `RequireAuth` middleware: additionally check `User.EmailVerified = true`; return 403 with `{"error":"unverified"}` if not

5. **Session persistence — 30-day cookie**
   - Change session cookie options: `IsPersistent = true`, `MaxAge = TimeSpan.FromDays(30)`
   - SQL Server session cache sliding expiration: 30 days
   - Local dev (memory cache) unchanged

6. **Background cleanup (hosted service)**
   - `VerificationCleanupService : BackgroundService` runs daily:
     - Delete `VerificationTokens` where `ExpiresAt < now - 7 days` (expired and stale)
     - Delete `Users` where `EmailVerified = false` and `CreatedAt < now - 7 days` (never verified, purge orphans)

7. **UI changes**
   - `index.html` — login form:
     - On `{"status":"pending"}` response: hide form, show holding state: *"Check your inbox — we sent a link to `{email}`."* + **Resend** button
     - Resend button calls `POST /auth/resend`, shows cooldown message on 429
   - `app.html` — app shell:
     - Logout button: already exists (Task 7), ensure it's visually clear in the nav; clicking calls `POST /auth/logout` → redirect to `index.html` (clearing 30-day cookie)
   - New: if `GET /auth/me` returns 403 `unverified` (edge case: session exists but flag cleared), redirect to `index.html`

---

**Acceptance criteria:**
- [x] New user login → 202 `{"status":"pending"}`; magic link email arrives via ACS
- [x] Clicking magic link sets 30-day persistent cookie and redirects to `/app.html`
- [x] Clicking same magic link a second time → 400 (token already used)
- [x] Token expired after 24hr → `GET /auth/verify` returns 400 with `{"error":"token_expired"}`
- [x] Existing users in DB are all `EmailVerified = true` after migration — no re-verification required
- [x] Unverified user cannot reach `/app.html` (hard gate redirects to `index.html`)
- [x] 4th resend within 1 hour → 429 `{"error":"too_many_requests"}`; button shows cooldown message
- [x] Unverified user created 8 days ago → deleted by cleanup service
- [x] Logout clears session; subsequent `GET /auth/me` returns 401
- [x] ACS `Microsoft.Communication/emailServices` resource present in `infra/main.bicep`
- [x] ACS connection string stored in Key Vault; Container App pulls it as a secret ref
- [x] Local dev works with no ACS connection string: magic link logged to console, no email sent
- [x] `dotnet build` passes with new packages
- [x] git commit created: `feat: task 13 — email verification via ACS magic link`

**Completion signal:** When all acceptance criteria above are checked `[x]` and the git commit exists, output exactly: `<promise>TESTS COMPLETE</promise>`

---

### Task 14 — Deployment Smoke Test Script

**Goal:** A single executable script (`smoke-test.sh`) that exercises the full happy path against any deployed URL and exits non-zero on failure. Runnable after every deployment in CI/CD.

**Problem:** The app requires email verification (magic link) so automated testing can't log in via the normal flow without intercepting email. Solution: add a protected `POST /auth/dev-login` endpoint gated behind `ASPNETCORE_ENVIRONMENT != Production` that creates a verified session without sending email.

**Work:**

1. **Dev-only login endpoint** — `POST /auth/dev-login` with `{ email }`:
   - Only registered when `app.Environment.IsDevelopment()` is true
   - Creates or finds user, marks `EmailVerified = true`, creates session
   - Returns 200 `{ "status": "ok" }` with Set-Cookie

2. **Smoke test environment** — run the app locally in `Development` mode for the smoke test, or:
   - Alternative: add a `SMOKE_TEST_SECRET` env var; when present the dev-login endpoint is enabled even in Production (gated by a shared secret header)
   - Chosen approach: `SMOKE_TEST_SECRET` header approach so the script can test the actual deployed container

3. **`smoke-test.sh`** — executable bash script:
   ```
   Usage: ./smoke-test.sh <base-url> [smoke-test-secret]
   Example: ./smoke-test.sh https://gasoholic.yellowcliff-a9ca470c.eastus.azurecontainerapps.io mysecret
   ```
   Steps:
   - GET /health → assert 200 `{"status":"ok"}`
   - POST /auth/dev-login → assert 200, capture cookie
   - GET /auth/me → assert 200, check email field
   - POST /api/autos → assert 201, capture autoId
   - GET /api/autos → assert auto appears in list
   - POST /api/autos/{autoId}/fillups (2 full fills) → assert 201 each
   - GET /api/autos/{autoId}/fillups → assert MPG is computed (not null)
   - DELETE /api/autos/{autoId}/fillups/{id} → assert 204
   - PUT /api/autos/{autoId} → assert 200
   - DELETE /api/autos/{autoId} → assert 204
   - POST /auth/logout → assert 200
   - GET /auth/me → assert 401
   - Print PASS / FAIL summary

4. **GitHub Actions integration** — add smoke test step to `azure-deploy.yml` after deployment:
   ```yaml
   - name: Smoke test
     run: ./smoke-test.sh $APP_URL ${{ secrets.SMOKE_TEST_SECRET }}
   ```

5. **Bicep/env var** — add `SMOKE_TEST_SECRET` as a Container App env var, stored in Key Vault

**Acceptance criteria:**
- [x] `./smoke-test.sh <live-url> <secret>` passes against the live deployment
- [x] `./smoke-test.sh <live-url> wrong-secret` fails at auth step (403 → FAIL on dev-login step)
- [x] `./smoke-test.sh <live-url>` without secret fails gracefully ("no SMOKE_TEST_SECRET provided — cannot authenticate")
- [x] Script exits 0 on all-pass, non-zero if any step fails
- [x] All steps print a clear PASS/FAIL line with the HTTP response
- [x] GitHub Actions workflow includes the smoke test step after image deploy
- [x] git commit created: `feat: task 14 — deployment smoke test script`

**Completion signal:** When all acceptance criteria above are checked `[x]` and the git commit exists, output exactly: `<promise>TESTS COMPLETE</promise>`

---

### Task 15 — Persistent Storage (Fix Ephemeral SQLite)

**Goal:** Replace ephemeral SQLite at `/tmp/gasoholic.db` with a persistent database so data and sessions survive Container App restarts and deployments.

**Root cause:** Container Apps consumption plan scales to zero after ~5 minutes of inactivity, wiping the in-memory session store and the SQLite file at `/tmp`. Every cold start creates a fresh empty database — user accounts and session cookies are lost.

**Architecture options evaluated:**
| Option | Cost | Feasibility |
|---|---|---|
| Azure SQL (DTU tiers) | ~$5/mo | Blocked by new-account quota in East US + East US 2 |
| Azure SQL (serverless) | ~$0 idle | Same quota issue |
| Azure PostgreSQL Flexible | ~$15/mo | Try West US 2 or West Europe |
| Azure Files (SQLite) | ~$2/mo | SMB port 445 blocked on consumption plan |
| Neon.tech Postgres | Free tier | External, no Azure infra needed |
| minReplicas: 1 + ephemeral | ~$15/mo | Prevents scale-to-zero but data still lost on redeploy |

**Chosen approach:** Try Azure SQL in West US 2 (different region), fall back to Neon.tech free Postgres if quota-blocked.

**Work:**

1. **Azure SQL in West US 2** — provision `gasoholic-sql` server + `gasoholic` database (Serverless, S0 or Free tier if available)
2. **Connection string** — store in Key Vault as `SqlConnection`; Container App reads as secret ref
3. **`DATABASE_PROVIDER=sqlserver`** — update Container App env var
4. **EF Core migrations** — run `dotnet ef database update` against Azure SQL on deploy
5. **Session cache** — `AddDistributedSqlServerCache` already in code; create `SessionCache` table
6. **Update `infra/main.bicep`** — add SQL server + database + connection string KV secret
7. **Update `deploy.sh`** — run migrations after Bicep deploy
8. **Firewall** — allow Azure services to connect to the SQL server

**Acceptance criteria:**
- [x] Azure SQL (West US 2, gasoholic-sql.database.windows.net) provisioned and accessible from the Container App
- [x] `DATABASE_PROVIDER=sqlserver` set on Container App
- [x] EF Core migrations applied (tables: Users, Autos, Fillups, VerificationTokens, SessionCache, __EFMigrationsHistory)
- [x] Login, add auto, restart Container App revision, data persists (verified via smoke test across cold starts)
- [x] Sessions survive Container App cold-start (SqlServer distributed cache replaces in-memory cache)
- [x] Smoke test (`./smoke-test.sh`) passes end-to-end against live deployment
- [x] git commit created: `feat: task 15 — persistent database storage`

**Completion signal:** When all acceptance criteria above are checked `[x]` and the git commit exists, output exactly: `<promise>TESTS COMPLETE</promise>`

---

### Task 16 — Default Auto Selector to Most Recently Fueled Auto

**Goal:** When the app loads and the user has at least one auto, pre-select the auto that had the most recent fillup instead of showing "— select auto —". If no auto has any fillups, fall back to the first auto in the list.

**Work:**

1. **API** — add `latestFillupAt` (ISO datetime string or `null`) to the `GET /api/autos` response via a left-join subquery on `Fillups.FilledAt`. No new endpoint; just extend the existing projection.

2. **Frontend** — in `renderAutoSelector`, on first load (when `prev` is empty and no `currentAutoId` is set), pick the auto with the highest `latestFillupAt`; if all are `null`, pick `autos[0]`. Set `autoSelector.value` and `currentAutoId` accordingly, then call `renderFillups()`.

**Acceptance criteria:**
- [x] User with 2+ autos: selector defaults to the auto whose most recent fillup is latest
- [x] User with autos but no fillups: selector defaults to the first auto in the list
- [x] After manually switching to a different auto, reloading autos (add/edit/delete) preserves the user's selection if the auto still exists
- [x] User with no autos: selector shows "— select auto —" as before
- [x] On initial page load with at least one auto, the fillup log renders immediately without requiring a manual selector interaction
- [x] Smoke test regression: `auto selector defaults to first auto when no fillups exist` passes in `happy-path.spec.ts` (@smoke tagged)
- [x] git commit created: `feat: task 16 — default selector to most recently fueled auto`

**Completion signal:** When all acceptance criteria above are checked `[x]` and the git commit exists, output exactly: `<promise>TESTS COMPLETE</promise>`

---

### Task 17 — Speed Up User Experience (Eliminate Cold Start + Fix Session Persistence + Faster Render)

**Goal:** Make the app feel instant at the pump. Three problems: (1) container cold-starts take 5-10s because `minReplicas: 0`, (2) sessions evict after ~20 minutes despite 30-day cookie because the distributed cache has no explicit sliding expiration, (3) the frontend shows a blank page while sequential API calls complete.

**Key decisions:**
| Concern | Choice |
|---|---|
| Cold start | `minReplicas: 1` — always-warm replica (~$15/mo, user-approved) |
| Session eviction | Set `DefaultSlidingExpiration = 30 days` on `AddDistributedSqlServerCache` to match cookie lifetime |
| Frontend render | Show app shell + skeleton immediately; parallelize auth check and data fetch; defer non-critical work |

---

**Work:**

1. **Eliminate cold start — Bicep (`infra/main.bicep`)**
   - Change `minReplicas: 0` → `minReplicas: 1` in the Container App scale config
   - Container stays warm; no more 5-10s spin-up delay

2. **Fix session persistence — `Program.cs`**
   - Add `DefaultSlidingExpiration = TimeSpan.FromDays(30)` to `AddDistributedSqlServerCache` options
   - This ensures the SQL Server `SessionCache` entry lives as long as the cookie (currently defaults to 20 minutes, causing premature eviction)
   - Add `options.Cookie.SameSite = SameSiteMode.Lax` explicitly for clarity (default behavior, but makes intent visible)

3. **Faster frontend render — `wwwroot/app.html`**
   - **Immediate shell**: render nav bar, tabs, and a loading skeleton/spinner *before* any fetch calls
   - **Parallel fetches**: fire `/auth/me` and `/api/autos` simultaneously on page load instead of sequentially (auth check → then load autos → then load fillups)
   - **Optimistic rendering**: show the app shell immediately; if auth fails, redirect; if auth succeeds, the autos data is already in flight
   - **Skeleton states**: show placeholder rows in the fillup table and auto cards while data loads, replace with real content on response

4. **Faster frontend render — `wwwroot/index.html`**
   - Same pattern: show the login form immediately, fire `/auth/me` in the background to check if already logged in, redirect only if the check succeeds (don't block form render on the auth check)

---

**Acceptance criteria:**
- [x] `infra/main.bicep` has `minReplicas: 1` (verified by reading the file)
- [x] `Program.cs` sets `DefaultSlidingExpiration = TimeSpan.FromDays(30)` on the distributed SQL Server cache
- [x] `Program.cs` sets `SameSite = SameSiteMode.Lax` on session cookie explicitly
- [x] `index.html` renders the login form immediately without waiting for `/auth/me` response
- [x] `app.html` shows the app shell (nav + tabs + skeleton) before any API response arrives
- [x] `app.html` fires `/auth/me` and `/api/autos` in parallel (not sequentially)
- [x] Fillup table shows a loading skeleton/spinner while data is being fetched
- [x] Auto cards show a loading skeleton while data is being fetched
- [x] `dotnet build` passes
- [x] Local dev: login persists across app restart when using `DATABASE_PROVIDER=sqlserver` (session not lost)
- [x] Local dev: app.html renders app shell within 100ms of navigation (before API responses)
- [x] git commit created: `feat: task 17 — speed up UX (warm replica, session persistence, fast render)`

**Completion signal:** When all acceptance criteria above are checked `[x]` and the git commit exists, output exactly: `<promise>TESTS COMPLETE</promise>`

---

### Task 18 — ACS Custom Domain for Email Deliverability

**Goal:** Replace the Azure-managed sending domain (`DoNotReply@<generated>.azurecomm.net`) with a custom domain (`gas.sdir.cc`) so magic link emails stop landing in spam. Add deployment verification tests to catch email config regressions.

**Root cause:** Azure-managed ACS domains are shared across all ACS tenants. Email providers (Gmail, Outlook, etc.) assign low trust scores to these shared domains, causing emails to land in spam. A custom domain with proper SPF/DKIM/DMARC records establishes sender reputation tied to `sdir.cc`.

**Key decisions:**
| Concern | Choice |
|---|---|
| Sending domain | `gas.sdir.cc` (subdomain isolates email reputation) |
| Sender address | `verify@gas.sdir.cc` |
| Email service | Stay on ACS free tier (2,000/mo is plenty for 1-10 emails/day) |
| DNS provider | User-managed (`sdir.cc` — user has full DNS access) |
| Fallback plan | If deliverability still poor after custom domain, migrate to Resend |

---

**Work:**

1. **Bicep — replace Azure-managed domain with custom domain (`infra/main.bicep`)**
   - Change `acsDomain` resource from `domainManagement: 'AzureManaged'` to `domainManagement: 'CustomerManaged'`
   - Set domain `name` to `gas.sdir.cc` instead of `AzureManagedDomain`
   - ACS will generate the required DNS records (SPF, DKIM, DKIM2) after deployment — these must be added manually (see DEPLOYMENT.md)
   - Update `acsDomainSecret` to output the custom domain name

2. **Update sender address (`Services/VerificationEmailSender.cs`)**
   - Change from address from `DoNotReply@{domain}` to `verify@{domain}`
   - The `AcsSenderDomain` env var will now resolve to `gas.sdir.cc` instead of the Azure-managed domain

3. **DMARC DNS record (manual — documented in DEPLOYMENT.md)**
   - Add `_dmarc.gas.sdir.cc` TXT record: `v=DMARC1; p=quarantine; rua=mailto:dmarc@sdir.cc`
   - DMARC is not managed by ACS but is required for good deliverability

4. **Email health check — extend `/health` endpoint**
   - Add an `email` section to the health response: `{ "status": "ok", "email": { "configured": true, "senderDomain": "gas.sdir.cc", "senderAddress": "verify@gas.sdir.cc" } }`
   - When ACS is not configured (dev mode): `{ "email": { "configured": false } }`

5. **Smoke test — email config verification (`smoke-test.sh`)**
   - Add step after health check: `GET /health` → assert `email.configured == true` and `email.senderDomain == "gas.sdir.cc"`
   - This catches regressions where the domain reverts to Azure-managed or ACS becomes unconfigured

6. **Smoke test — email send verification (`smoke-test.sh`)**
   - Add a new protected endpoint: `POST /auth/test-email` (gated by `SMOKE_TEST_SECRET`, like dev-login)
   - Sends a test email to a configurable address via ACS and returns the ACS message ID + status
   - Smoke test calls this endpoint and asserts ACS accepted the message (HTTP 200 + `"status": "sent"`)
   - This verifies the full ACS → custom domain → send pipeline works, without needing to check an inbox

7. **Playwright e2e regression — login flow still works**
   - Existing `login.spec.ts` should continue to pass (dev mode bypasses email)
   - No new Playwright tests needed — email delivery is verified by smoke test

---

**Acceptance criteria:**
- [x] `infra/main.bicep` declares a `CustomerManaged` domain for `gas.sdir.cc` (not `AzureManaged`)
- [x] DNS records (SPF, DKIM, DKIM2, DMARC) are documented in `DEPLOYMENT.md` with exact values from ACS
- [x] `VerificationEmailSender.cs` sends from `verify@gas.sdir.cc`
- [x] `/health` endpoint returns `email.configured`, `email.senderDomain`, and `email.senderAddress`
- [x] `smoke-test.sh` verifies email config via `/health` response
- [x] `POST /auth/test-email` endpoint exists (gated by `SMOKE_TEST_SECRET`) and sends a real test email via ACS
- [x] `smoke-test.sh` calls `/auth/test-email` and asserts ACS accepted the message
- [ ] ACS domain verification status is `Verified` in Azure Portal (manual check after DNS setup)
- [ ] Test email sent from `verify@gas.sdir.cc` lands in inbox (not spam) on Gmail and Outlook (manual check)
- [x] Existing Playwright e2e tests pass (no regressions)
- [x] `dotnet build` passes
- [x] git commit created: `feat: task 18 — ACS custom domain for email deliverability`

**Completion signal:** When all acceptance criteria above are checked `[x]` and the git commit exists, output exactly: `<promise>TESTS COMPLETE</promise>`

---

### Task 19 — User Sign-In & Activity Tracking

**Goal:** Add visibility into when users last authenticated and when they were last active. Two new nullable `DateTime` columns on the `Users` table, database-only (no UI changes).

**Key decisions:**
| Concern | Choice |
|---|---|
| `LastSignIn` trigger | Magic link verification (`/auth/verify`) and dev-login only |
| `LastInteraction` trigger | Once per session start — first `/auth/me` call with valid cookie |
| Throttle strategy | Session-scoped flag (`interactionLogged`) — writes once per session, resets on logout/expiry |
| UI visibility | Database-only for now — queryable via SQL or future admin API |
| Testing | Migration + build verification only — no smoke test changes |

---

**Work:**

1. **Data model (`Models/User.cs`)**
   - Add `DateTime? LastSignIn` property
   - Add `DateTime? LastInteraction` property
   - No `AppDbContext` changes needed — nullable DateTime requires no special config

2. **EF migration**
   - `dotnet ef migrations add AddSignInTracking`
   - Adds two nullable `datetime2` columns to `Users` table

3. **Update `/auth/verify` (`Endpoints/AuthEndpoints.cs` ~line 69)**
   - After marking `EmailVerified = true`, set `vt.User.LastSignIn = DateTime.UtcNow`

4. **Update `/auth/dev-login` (`Endpoints/SmokeTestEndpoints.cs` ~line 64)**
   - Set `user.LastSignIn = DateTime.UtcNow` on both new-user creation and existing-user paths

5. **Update `/auth/me` for LastInteraction (`Endpoints/AuthEndpoints.cs` ~line 121)**
   - After confirming user is valid and verified, check a session flag `interactionLogged`
   - If flag is absent: set `user.LastInteraction = DateTime.UtcNow`, save, set flag in session
   - Flag lives in session — resets naturally on logout or session expiry
   - This ensures exactly one DB write per session resume, not per page load

---

**Acceptance criteria:**
- [x] `Models/User.cs` has `DateTime? LastSignIn` and `DateTime? LastInteraction` properties
- [x] EF migration `AddSignInTracking` exists and adds two nullable columns to `Users`
- [x] `/auth/verify` sets `LastSignIn` on successful magic link verification
- [x] `/auth/dev-login` sets `LastSignIn` on successful dev login
- [x] `/auth/me` sets `LastInteraction` once per session (uses session flag to avoid repeated writes)
- [x] `dotnet build` passes
- [x] git commit created: `feat: task 19 — user sign-in and activity tracking`

**Completion signal:** When all acceptance criteria above are checked `[x]` and the git commit exists, output exactly: `<promise>TESTS COMPLETE</promise>`

---

### Task 20 — Smoke Test User Cleanup

**Goal:** Smoke and e2e test runs create users via `/auth/dev-login` that persist in the production database forever. Add a cleanup mechanism so test users are removed after each test run.

**Key decisions:**
| Concern | Choice |
|---|---|
| Identification | Test users have emails matching `*@example.com` or `*@test.com` |
| Cleanup trigger | New `DELETE /auth/dev-cleanup` endpoint (gated by `SMOKE_TEST_SECRET`) |
| Scope | Deletes the user **and** cascades to their autos, fillups, and verification tokens |
| When to call | At the end of the smoke test suite (Playwright `afterAll` or final test) |
| Safety | Endpoint only exists when `SMOKE_TEST_SECRET` is configured; rejects non-test emails |

---

**Work:**

1. **New endpoint (`Endpoints/SmokeTestEndpoints.cs`)**
   - `DELETE /auth/dev-cleanup` — accepts `{ email: string }`, gated by `X-Smoke-Test-Secret` header
   - Validates the email matches a test domain (`@example.com` or `@test.com`) — refuses to delete real users
   - Deletes the user and all related data (cascade handles autos/fillups/tokens)
   - Returns `204` on success, `404` if user not found, `400` if email is not a test domain

2. **Smoke test cleanup (`e2e/smoke/happy-path.spec.ts`)**
   - Add a final test (or `test.afterAll`) that calls `DELETE /auth/dev-cleanup` for each test email used
   - Verify the user no longer exists (optional: `GET /auth/me` returns 401 after cleanup)

3. **Playwright e2e cleanup (`e2e/helpers/auth.ts`)**
   - Export a `cleanupUser(request, email)` helper
   - Wire into existing test suites that use `devLogin` / `uniqueEmail`

---

**Acceptance criteria:**
- [x] `DELETE /auth/dev-cleanup` endpoint exists, gated by `SMOKE_TEST_SECRET`
- [x] Endpoint refuses to delete emails that don't match `@example.com` or `@test.com`
- [x] Endpoint deletes user + cascaded data (autos, fillups, verification tokens)
- [x] Smoke test suite calls cleanup for all test users at end of run
- [x] Running smoke tests twice leaves no duplicate/orphaned test users in the database
- [x] Existing e2e tests still pass
- [x] `dotnet build` passes
- [x] git commit created: `feat: task 20 — smoke test user cleanup`

**Completion signal:** When all acceptance criteria above are checked `[x]` and the git commit exists, output exactly: `<promise>TESTS COMPLETE</promise>`

---

## Loop Execution Notes

**Start the full sequential loop (first tasks with unchecked criteria) with:**
```
/ralph-loop:ralph-loop "$(cat .claude/PLAN.md)" --completion-promise "TESTS COMPLETE"
```

- Each iteration: find the first task with unchecked `[ ]` criteria → implement → check off each criterion → write/run Playwright test → **git commit** → loop again
- ALL task completion _should_ emits `<promise>TESTS COMPLETE</promise>` to end the loop (10 and above)
- Commit message format: `feat: task N — <short description>`
- Some tasks are already complete; the loop will skip them automatically (all `[x]`)
