# Plan: Migrate data layer to Azure Cosmos DB for NoSQL

> Source PRD: GitHub issue #10 — "PRD: Migrate data layer from SQLite to Azure Cosmos DB for NoSQL (serverless)"

## Architectural decisions

Durable decisions that apply across all phases:

- **Data provider**: EF Core Cosmos provider (`Microsoft.EntityFrameworkCore.Cosmos`). `AppDbContext` + `DbSet<T>` shape preserved.
- **Cosmos account**: API = SQL (NoSQL), capacity mode = Serverless, location = East US, consistency = Session (default).
- **Container layout**: container-per-entity.
  - `Users` — partition key `/id`
  - `Autos` — partition key `/userId`
  - `Fillups` — partition key `/autoId`
  - `Maintenance` — partition key `/autoId`
  - `VerificationTokens` — partition key `/userId`, native Cosmos TTL
- **Schema provisioning**: EF Core `EnsureCreatedAsync()` on startup. No migrations.
- **Cascade deletes**: implemented explicitly in endpoint code (Cosmos has no FK cascades). User → Autos → Fillups + Maintenance + Tokens. Auto → Fillups + Maintenance.
- **Auth**: magic-link signups and 30-day session renewal, backed by `VerificationTokens` container with native TTL replacing the old `VerificationCleanupService`.
- **Config**:
  - `appsettings.Development.json` → Cosmos emulator connection string
  - `appsettings.Testing.json` → EF Core InMemory provider
  - `appsettings.Production.json` → `ConnectionStrings:Cosmos` from env var, secret-ref'd to Key Vault
- **Local dev**: Azure Cosmos DB Emulator in Docker, single-command startup via `start.sh` or `docker-compose.yml`.
- **Testing strategy**: fast unit tests via EF Core InMemory provider (logic coverage) + post-deploy smoke test against the live production URL (wiring coverage).
- **Infra (`infra/main.bicep`)**: keep ACR, ACS, Key Vault, Container Apps Env, Container App, managed identity, existing RBAC. Add Cosmos account + database + five containers + `CosmosConnection` Key Vault secret + `ConnectionStrings__Cosmos` secret-ref env var. Remove `ConnectionStrings__Sqlite` env var.
- **Deploy pipeline**: `deploy.sh` (recreated from scratch) drives local/manual deploys; a GitHub Actions workflow wraps `deploy.sh` + `smoke-test.sh` for CI/CD.

---

## Phase 1: Cosmos foundation running locally (no auth)

**User stories**: 1, 5, 6, 10, 11, 12, 13, 14, 15, 16, 19, 20, 21, 22, 26, 28, 29

### What to build

Strip SQLite from the project entirely and stand up a working Cosmos-backed local stack for the four non-auth entities. After this phase the app builds, boots against a Dockerized Cosmos emulator, and all CRUD + cascade behavior for `User`, `Auto`, `Fillup`, and `MaintenanceRecord` is covered by fast in-memory unit tests. Magic-link auth is **not yet** wired to Cosmos — signup/session endpoints may be temporarily disabled or stubbed, and all endpoint testing in this phase goes through direct HTTP calls (e.g. `gasoholic.http`, `curl`) rather than a browser signup flow.

Scope includes: removing the EF Core SQLite package, deleting `Migrations/`, deleting `gasoholic.db`, deleting `reset-db.sh`, purging `ConnectionStrings:Sqlite` from `appsettings.Development.json` and `appsettings.Testing.json`, adding `Microsoft.EntityFrameworkCore.Cosmos`, reconfiguring `AppDbContext.OnModelCreating` for the four entities with their partition keys, implementing explicit cascade deletes in endpoint code, adding the emulator to the local Docker workflow, and branching `Program.cs` DbContext registration by environment (InMemory for Testing, Cosmos emulator for Development).

### Acceptance criteria

- [ ] `dotnet build` succeeds with no SQLite package references.
- [ ] `Migrations/`, `gasoholic.db`, and `reset-db.sh` are deleted from the repo.
- [ ] Azure Cosmos DB Emulator starts via a single local command (part of `start.sh` or `docker-compose.yml`).
- [ ] The app boots against the emulator, creates all four containers on first run, and health-check endpoints succeed.
- [ ] CRUD on `User`, `Auto`, `Fillup`, and `MaintenanceRecord` works end-to-end via direct HTTP requests against the locally running app.
- [ ] Deleting an Auto removes its Fillups and Maintenance records.
- [ ] Deleting a User removes all its Autos and their dependents.
- [ ] Unit tests (EF Core InMemory) cover CRUD + cascade for all four entities, run in under a few seconds, and are green.
- [ ] No references to `Sqlite`, `gasoholic.db`, or the removed migration folder remain in source files touched by this phase.

---

## Phase 2: Magic-link auth + 30-day session renewal on Cosmos

**User stories**: 17, 18, 27

### What to build

Bring authentication onto the new data layer. Add the `VerificationTokens` container configuration to `AppDbContext.OnModelCreating` with partition key `/userId` and native TTL declared via `HasDefaultTimeToLive()`. Delete `VerificationCleanupService` and its DI registration — token expiration is now owned by Cosmos. Wire the magic-link signup flow and the 30-day session renewal flow against the emulator. Exit criteria for this phase require a full manual end-to-end walkthrough in a browser against the locally running stack: sign up a new user, receive the magic link, complete the signup, stay signed in, trigger a session renewal, sign out, and confirm data persists across an app restart.

### Acceptance criteria

- [ ] `VerificationTokens` container is declared in `AppDbContext` with partition key `/userId` and default TTL.
- [ ] `VerificationCleanupService` and its DI registration are deleted.
- [ ] Magic-link signup works end-to-end locally: new user receives a token, consumes it, and is signed in.
- [ ] 30-day session renewal works locally against the emulator.
- [ ] Unit tests (InMemory) cover token issuance, token consumption, and session renewal.
- [ ] A full manual browser walkthrough (signup → add car → log fillup → log maintenance → renew session → delete account → verify cascade) succeeds against the local emulator.
- [ ] Restarting the app against the same emulator instance preserves all data.

---

## Phase 3: Production infra + manual `deploy.sh` verified live

**User stories**: 2, 3, 4, 7, 8, 9, 23, 25

### What to build

Update `infra/main.bicep` with the Cosmos DB account, database, and five containers (serverless, East US), the `CosmosConnection` Key Vault secret populated from `listConnectionStrings()`, and a `ConnectionStrings__Cosmos` secret-ref env var on the Container App. Remove the `ConnectionStrings__Sqlite` env var. Set `appsettings.Production.json` to read the Cosmos connection from configuration. Recreate `deploy.sh` from scratch as a manual deploy driver: build image, push to ACR, run `az deployment group create` against the bicep template, confirm rollout, and emit a summary of the deployed URL and resource names.

Write `smoke-test.sh` that takes a base URL and `SMOKE_TEST_SECRET` and runs the full CRUD cascade against the live site (create user → create auto → create fillup → create maintenance → delete user → verify all dependents gone), self-cleaning on both success and failure paths and exiting non-zero on any failure. Run `deploy.sh` manually from a clean shell, then run `smoke-test.sh` manually against the deployed URL and confirm it passes. Manually verify in a browser that magic-link signup works on the live site and that data persists across a Container App restart — the specific failure mode that motivated this whole PRD.

### Acceptance criteria

- [ ] `main.bicep` provisions the Cosmos account, database, and all five containers with correct partition keys.
- [ ] `CosmosConnection` Key Vault secret is populated from the Cosmos account's connection string.
- [ ] Container App reads `ConnectionStrings__Cosmos` from a secret-ref backed by the Key Vault secret.
- [ ] `ConnectionStrings__Sqlite` env var is removed from `main.bicep`.
- [ ] `deploy.sh` runs clean on a fresh machine and on a re-invocation (idempotent).
- [ ] `smoke-test.sh` runs green against the deployed URL and leaves no test data behind.
- [ ] Manual magic-link signup succeeds on the live URL.
- [ ] Data persists across a manually-triggered Container App restart (the original bug is fixed).

---

## Phase 4: GitHub Actions CI/CD wrapping `deploy.sh` + smoke test

**User stories**: 24

### What to build

Create or update a GitHub Actions workflow that runs `deploy.sh` on pushes to the main branch (or whichever trigger the repo uses) and runs `smoke-test.sh` as the final step. A failing smoke test must fail the workflow. Confirm all required secrets are configured in the repo settings: Azure service principal credentials, ACR credentials, `SMOKE_TEST_SECRET`, and any others `deploy.sh` needs. Verify an end-to-end run from a real commit: push, watch the workflow, confirm it deploys and the smoke test passes in CI.

### Acceptance criteria

- [ ] A GitHub Actions workflow runs `deploy.sh` followed by `smoke-test.sh` on the configured trigger.
- [ ] All required secrets are present in the repo's Actions secrets.
- [ ] A real push triggers the workflow; it deploys and the smoke test passes.
- [ ] A deliberately-broken smoke test fails the workflow (manually verified once, then reverted).

---

## Phase 5: Docs & final repo cleanup

**User stories**: 30, 31, 32

### What to build

Audit the Dockerfile for any leftover SQLite runtime packages and remove them. Grep the whole repo for `sqlite`/`Sqlite` and remove any stragglers from source, configs, and docs. Update `DEPLOYMENT.md`, `LOCAL_DEVELOPMENT.md`, `README.md`, `DEVELOPING.md`, and `AZURE_SETUP_CHECKLIST.md` to describe the Cosmos + emulator workflow, the `deploy.sh` manual path, and the GitHub Actions pipeline. Verify onboarding docs by walking them on a fresh clone or fresh shell.

### Acceptance criteria

- [ ] No SQLite references remain in source, configs, or documentation (verified via repo-wide grep).
- [ ] Dockerfile contains no SQLite runtime packages.
- [ ] `LOCAL_DEVELOPMENT.md` describes the emulator-based local workflow and matches actual behavior.
- [ ] `DEPLOYMENT.md` describes `deploy.sh` and the GitHub Actions workflow accurately.
- [ ] `AZURE_SETUP_CHECKLIST.md` lists the Cosmos account provisioning step.
- [ ] `README.md` and `DEVELOPING.md` reflect the new stack.
- [ ] A fresh clone can reach a running local app by following `LOCAL_DEVELOPMENT.md` alone.
