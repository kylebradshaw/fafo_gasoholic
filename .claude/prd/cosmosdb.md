## Problem Statement

The `gasoholic` fillup tracker currently runs SQLite in the Container App's **ephemeral filesystem** (no volume mount), so every container restart wipes all user data. Previous attempts to fix this — running on SQL Server, then pivoting to SQLite on Azure Files — were either too expensive (SQL Server cold starts + cost) or guided by bad advice. The app has ~5 users/month with ~4 fillups each (~20 rows/month total), so the persistence layer needs to be cheap, durable, and low-operations.

## Solution

Replace the SQLite data layer entirely with **Azure Cosmos DB for NoSQL (serverless)** in East US, accessed via the EF Core Cosmos provider. Container-per-entity modeling mirrors the current relational schema. Verification tokens use native Cosmos TTL (replacing the cleanup background service). Because no production data was ever preserved, we start from an empty database — no migration. Local development uses the Dockerized Cosmos emulator; unit tests use EF Core InMemory; a post-deploy smoke test hits the live website to guarantee Cosmos wiring works in production.

## User Stories

1. As the app owner, I want a persistence layer that does **not** wipe data on container restart, so that users can trust their fillups are saved.
2. As the app owner, I want Cosmos DB in **serverless** mode, so that my monthly bill scales with actual (tiny) usage instead of a $24+/mo provisioned floor.
3. As the app owner, I want Cosmos DB in **East US**, so that resources are co-located with the existing resource group.
4. As the app owner, I want backups handled by Cosmos's **free periodic backup** default, so that I pay nothing extra for disaster recovery at this scale.
5. As the app owner, I want all previously-SQLite-related code, config, and infra removed, so that there is no dead code or accidental SQLite fallback.
6. As a developer, I want the `Migrations/` folder deleted, so that the repo does not contain stale relational migration files.
7. As a developer, I want the Cosmos connection string stored as a **Key Vault secret** referenced by the Container App's managed identity, so that no credentials live in the repo.
8. As a developer, I want the `ConnectionStrings__Sqlite` env var removed from `main.bicep` and replaced with a `ConnectionStrings__Cosmos` secret reference, so that production points only at Cosmos.
9. As a developer, I want a **Cosmos DB account, database, and containers** provisioned by `main.bicep`, so that `azd up` or equivalent stands up the full stack from scratch.
10. As a developer, I want containers auto-created on app startup via EF Core `EnsureCreatedAsync()`, so that I do not need a separate provisioning step for the schema.
11. As a developer, I want local development to run against the **Azure Cosmos DB Emulator in Docker**, so that I can work offline without hitting Azure.
12. As a developer, I want a `docker-compose.yml` entry (or `start.sh` integration) for the emulator, so that starting local dev is a single command.
13. As a user, I want each of my `Auto` documents partitioned by `userId`, so that reading all my cars is a single-partition query.
14. As a user, I want each `Fillup` partitioned by `autoId`, so that reading one car's fillup history is a single-partition query.
15. As a user, I want each `MaintenanceRecord` partitioned by `autoId`, so that reading one car's maintenance history is a single-partition query.
16. As a user, I want `User` documents partitioned by `/id`, so that user lookups remain cheap.
17. As a user, I want `VerificationToken` documents partitioned by `/userId` **with native Cosmos TTL**, so that expired tokens vanish without a cleanup service.
18. As a developer, I want the `VerificationCleanupService` background service **deleted**, so that token expiration is owned by the database.
19. As a user, when my account is deleted, I want all my Autos, Fillups, MaintenanceRecords, and VerificationTokens deleted too, so that no orphan data remains.
20. As a user, when a car is deleted, I want all its Fillups and MaintenanceRecords deleted too, so that no orphan data remains.
21. As a developer, I want cascade-delete behavior implemented **explicitly in repository/endpoint code** (Cosmos has no FK cascades), so that delete semantics match the previous relational model.
22. As a developer, I want unit tests using **EF Core InMemory provider** for business logic, so that tests run fast and do not require a live database.
23. As a developer, I want a **post-deploy smoke test script** that hits the live production URL and exercises all CRUD paths (create user → create auto → create fillup → create maintenance → delete user cascade), so that I know with certainty Cosmos is wired end-to-end after every deploy.
24. As a developer, I want the smoke test to run automatically after `azd deploy` (or equivalent), so that a broken deploy is caught before users notice.
25. As a developer, I want the smoke test to **clean up its own test data**, so that the production database does not accumulate test artifacts.
26. As a developer, I want `AppDbContext` to configure partition keys via `HasPartitionKey()` in `OnModelCreating`, so that partitioning is explicit and version-controlled.
27. As a developer, I want `AppDbContext` to configure TTL on `VerificationToken` via `HasDefaultTimeToLive()`, so that TTL is declared alongside the model.
28. As a developer, I want the `gasoholic.db` file and `reset-db.sh` removed from the repo, so that no SQLite artifacts remain.
29. As a developer, I want `appsettings.*.json` files updated to remove `ConnectionStrings:Sqlite` and add `ConnectionStrings:Cosmos`, so that config matches the new provider.
30. As a developer, I want `DEPLOYMENT.md`, `LOCAL_DEVELOPMENT.md`, `README.md`, and `DEVELOPING.md` updated to reflect Cosmos instead of SQLite, so that onboarding docs are accurate.
31. As a developer, I want `AZURE_SETUP_CHECKLIST.md` updated with the Cosmos account provisioning step, so that a fresh Azure setup works end-to-end.
32. As a developer, I want the Dockerfile to no longer reference SQLite runtime packages (if any), so that the image is minimal.

## Implementation Decisions

**Data layer**
- Replace SQLite provider with **EF Core Cosmos provider** (`Microsoft.EntityFrameworkCore.Cosmos`).
- Keep `AppDbContext` and `DbSet<T>` shape. Reconfigure `OnModelCreating` for Cosmos: declare container names, partition keys, and TTL.
- **Container-per-entity**: `Users`, `Autos`, `Fillups`, `Maintenance`, `VerificationTokens`.
- **Partition keys**: `User → /id`, `Auto → /userId`, `Fillup → /autoId`, `MaintenanceRecord → /autoId`, `VerificationToken → /userId`.
- **TTL**: `VerificationToken` container configured with default TTL matching current token lifetime; individual docs set their own TTL on insert if needed.
- **Cascade deletes**: removed from EF Core config (Cosmos does not support them). Explicit multi-step deletes implemented in the endpoints or a thin repository layer.
- **Migrations**: `Migrations/` folder deleted. Schema provisioned by `DbContext.Database.EnsureCreatedAsync()` at startup.
- **Schema evolution**: handled by nullable/defaulted properties on models; bulk reshapes handled by one-off scripts when needed.

**Services**
- Delete `VerificationCleanupService.cs` and its registration.
- Keep `VerificationEmailSender` unchanged.

**Config**
- `appsettings.json`: remove `ConnectionStrings:Sqlite`, add `ConnectionStrings:Cosmos`.
- `appsettings.Development.json`: point Cosmos connection at the emulator's default well-known endpoint + key.
- `appsettings.Production.json`: reference the Key Vault secret via Container App env var.
- `appsettings.Testing.json`: configure EF Core InMemory provider for unit tests.
- `Program.cs`: branch DbContext registration on environment (InMemory for Testing, Cosmos emulator for Development, Cosmos for Production).

**Local development**
- Add Azure Cosmos DB Emulator to local Docker workflow (either `docker-compose.yml` or `start.sh`).
- Document emulator cert trust / connection string in `LOCAL_DEVELOPMENT.md`.
- Remove `gasoholic.db` and `reset-db.sh`.

**Infra (`infra/main.bicep`)**
- **Keep**: ACR, ACS Email, ACS Communication Service, Key Vault (+ existing secrets), Container Apps Environment, Container App, managed identity, existing Key Vault Secrets User role assignment.
- **Remove**: `ConnectionStrings__Sqlite` env var from the Container App template.
- **Add**:
  - `Microsoft.DocumentDB/databaseAccounts` (Cosmos DB account) — API: SQL, capacity mode: **Serverless**, location: East US, consistency: Session (default), free tier: off (serverless incompatible).
  - Cosmos SQL database (`gasoholic`) and five containers with partition keys as specified above.
  - Key Vault secret `CosmosConnection` populated from `listConnectionStrings()` of the Cosmos account.
  - `ConnectionStrings__Cosmos` env var on the Container App, `secretRef` to a new Key Vault-backed secret `cosmos-connection`.
  - (Existing Key Vault Secrets User role on the Container App's managed identity already covers the new secret — no new RBAC needed.)
- Verify resource group is in East US, or set an explicit `location: 'eastus'` param on the Cosmos resource.

**Deployment verification**
- Add a `smoke-test.sh` (or extend the existing one) that, given a base URL and the `SMOKE_TEST_SECRET`, runs the full CRUD cascade against the live site and reports pass/fail with non-zero exit on failure.
- Run the smoke test as the final step of the deploy pipeline.

## Testing Decisions

**Principle**: tests assert externally observable behavior (HTTP responses, DB state after an operation), not internal call patterns or EF Core provider internals.

**Unit tests (`Tests/`)** — EF Core InMemory provider:
- Endpoint handlers: create/read/update/delete for User, Auto, Fillup, MaintenanceRecord.
- Cascade delete logic: deleting a User removes all dependent docs; deleting an Auto removes its Fillups + Maintenance.
- Verification token issuance and consumption.
- Auth/magic-link flow.
- **Known gap**: InMemory does not validate partition keys, Cosmos-specific LINQ translation, or TTL behavior. This is intentional — unit tests cover logic, the smoke test covers wiring.

**Post-deploy smoke test** — runs against the live production URL:
- Authenticates with `SMOKE_TEST_SECRET`.
- Executes a full CRUD cascade: create user → create auto → create fillup → create maintenance → read each → delete user → verify all dependents are gone.
- Exits non-zero on any failure so CI/CD marks the deploy broken.
- Cleans up its own data in both success and failure paths.

**Prior art**: the existing `smoke-test.sh` at the repo root is the starting point.

## Out of Scope

- Data migration from the old SQLite database (nothing was saved; starting empty).
- Switching from EF Core to the raw `Microsoft.Azure.Cosmos` SDK (revisit if EF Core provider limitations bite).
- Switching from connection strings to managed identity RBAC for Cosmos auth (revisit later if secret rotation becomes painful).
- Continuous (point-in-time) backups (free periodic is sufficient at this scale).
- Single-container modeling with discriminated documents (rejected in favor of container-per-entity for easier mental mapping).
- Integration tests against the Docker Cosmos emulator in CI (rejected in favor of a single post-deploy smoke test against the live site).
- Changes to the frontend, auth flow, or any endpoint contracts.
- Performance/indexing policy tuning beyond Cosmos defaults.

## Further Notes

- **Cost expectation**: at ~20 writes/month, Cosmos serverless should cost well under $1/month (request units + <1 GB storage).
- **Gotcha**: the EF Core Cosmos provider is less mature than the SQL Server provider. Some LINQ expressions that work on SQL Server throw at runtime on Cosmos. Watch for `.GroupBy`, complex joins, and certain `.Include` patterns. The smoke test is the safety net.
- **Gotcha**: `EnsureCreatedAsync()` is idempotent but not free — it makes metadata calls on startup. Acceptable for this app; revisit if cold start becomes a concern.
- **Follow-up candidate**: if token expiration volume grows, Cosmos TTL deletes consume RUs. At current scale, negligible.