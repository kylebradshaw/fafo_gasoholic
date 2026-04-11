# Plan: SQL Server to SQLite Transition

> Source PRD: `.claude/prd/database-transition.md`

## Architectural decisions

Durable decisions that apply across all phases:

- **Database provider**: `Microsoft.EntityFrameworkCore.Sqlite` replaces `Microsoft.EntityFrameworkCore.SqlServer`
- **Session storage**: `AddDistributedMemoryCache` replaces `AddDistributedSqlServerCache` — sessions lost on container restart, acceptable at <5 users
- **Connection string key**: `ConnectionStrings:Sqlite` replaces `ConnectionStrings:SqlServer` across all appsettings files
- **Connection strings**:
  - Local dev: `Data Source=gasoholic.db` (relative path, file in project root, gitignored)
  - Production: `Data Source=/data/gasoholic.db` (`/data` is Azure Files SMB mount)
  - Tests: per-test temp file created by test factory, no config file needed
- **No secrets**: SQLite has no credentials. Key Vault SQL Server secret and `SA_PASSWORD` env var are eliminated entirely
- **Schema**: identical tables (Users, Autos, Fillups, MaintenanceRecords, VerificationTokens) — no schema changes beyond provider compatibility
- **WAL mode**: enable SQLite WAL journal mode for concurrent read performance
- **Migrations**: fresh `InitialCreate` migration targeting SQLite; all SQL Server migrations deleted

---

## Phase 1: Swap EF Core provider to SQLite (local dev + tests)

**User stories**: 8, 9, 10, 11, 12, 14

### What to build

Replace the SQL Server EF Core provider with SQLite across the entire application. This is a single vertical slice: NuGet packages, `Program.cs` wiring, connection strings, design-time factory, session storage, migrations, test factory, and local dev infrastructure all change together. At the end of this phase, `dotnet run` starts locally against a SQLite file and all integration tests pass against per-test SQLite databases.

### Acceptance criteria

- [x] `Microsoft.EntityFrameworkCore.SqlServer` and `Microsoft.Extensions.Caching.SqlServer` NuGet packages removed; `Microsoft.EntityFrameworkCore.Sqlite` added
- [x] `Program.cs` uses `UseSqlite()` and `AddDistributedMemoryCache()`; all `SA_PASSWORD` logic removed
- [x] `AppDbContextFactory` targets SQLite for design-time migrations
- [x] All existing SQL Server migrations deleted; fresh `InitialCreate` migration generated for SQLite
- [x] `appsettings.Development.json` has `ConnectionStrings:Sqlite` pointing to a local file path
- [x] `appsettings.Testing.json` updated (or removed if unnecessary) — test factory creates temp SQLite files
- [x] `GasoholicWebAppFactory` creates per-test temporary SQLite databases; no SQL Server connection logic, no `SA_PASSWORD`, no `DROP DATABASE` teardown
- [x] `docker-compose.yml` deleted
- [x] `gasoholic.db` added to `.gitignore`
- [x] All existing integration tests pass (`AuthEndpointTests`, `AutoEndpointTests`, `FillupEndpointTests`, `MpgComputationTests`)

---

## Phase 1b: Update local dev documentation for SQLite

### What to build

Update all project documentation that references SQL Server for local development to reflect the SQLite transition. Production-facing docs (DEPLOYMENT.md, AZURE_SETUP_CHECKLIST.md, infra/README.md) are deferred to Phase 3/4 when the production infrastructure actually changes. Historical records (.claude/PLAN.md, .claude/FAFO.md) are left as-is.

### Acceptance criteria

- [x] `DEVELOPING.md` — Remove Docker Desktop prerequisite, `SA_PASSWORD`/`.env` setup, all `docker compose` commands; update environment variables table (`ConnectionStrings__Sqlite`); update project structure (no `docker-compose.yml`, migrations description updated); update testing section to remove "real SQL Server" language
- [x] `README.md` — Update stack line to "SQLite" (no "Azure SQL" split); update tech stack table (remove SQL Server row, update SQLite row, update Distributed Caching row); remove Docker from Infrastructure & DevOps table
- [x] `DOCUMENTATION_INDEX.md` — Update architecture table ("SQLite" for database, not "SQLite (local), Azure SQL Server (prod)"); update decision log (remove "Azure SQL in West US 2" row); update last-updated date

---

## Phase 2: One-time data migration workflow

**User stories**: 2, 3, 4, 5

### What to build

A `workflow_dispatch`-triggered GitHub Actions workflow that exports all production data from Azure SQL Database to CSV, creates a fresh SQLite database with the correct schema, imports the CSV data preserving primary keys, verifies row counts match, and uploads the SQLite file to the Azure Files volume. CSVs are uploaded as workflow artifacts regardless of migration outcome, serving as a backup.

### Acceptance criteria

- [x] `.github/workflows/migrate-sqlserver-to-sqlite.yml` exists and is manually triggered
- [x] Workflow connects to Azure SQL Database and exports Users, Autos, Fillups, MaintenanceRecords, VerificationTokens to CSV
- [x] CSVs uploaded as GitHub Actions artifacts
- [x] Workflow creates SQLite database, runs EF Core migrations to create schema, imports CSV data preserving PKs
- [x] Row counts verified between SQL Server source and SQLite target; workflow fails if mismatch
- [x] SQLite file uploaded to Azure Files volume at `/data/gasoholic.db`
- [x] Workflow is idempotent — recreates SQLite DB from scratch on each run

---

## Phase 3: Update CI/CD deploy pipeline for SQLite

**User stories**: 1, 6, 7, 15, 16

### What to build

Update the production deploy pipeline so the container app runs against SQLite on Azure Files. Add a pre-deploy CSV backup step that snapshots the current database before every deploy. Update `appsettings.Production.json` with the SQLite connection string. Verify the Dockerfile and container app configuration are compatible with the Azure Files mount at `/data`.

### Acceptance criteria

- [x] `appsettings.Production.json` has `ConnectionStrings:Sqlite` set to `Data Source=/data/gasoholic.db`
- [x] `azure-deploy.yml` includes a pre-deploy step that downloads the SQLite file from Azure Files, dumps all tables to CSV, and uploads CSVs as GitHub Actions artifacts
- [x] CSV backup artifacts have configurable retention
- [x] Dockerfile remains compatible with Azure Files SMB mount at `/data`
- [x] Container app deploys successfully and serves traffic using SQLite
- [x] Health endpoint returns 200 after deploy
- [x] Smoke tests pass against the deployed container
- [x] `DEPLOYMENT.md` updated — Remove Azure SQL provisioning steps, SessionCache table creation, `DATABASE_PROVIDER` references; replace with SQLite on Azure Files architecture
- [x] `AZURE_SETUP_CHECKLIST.md` updated — Remove Step 1.5 (Azure SQL Database), update troubleshooting (remove SqlConnection references), update cost table, update Quick Reference table (remove Azure SQL row)
- [x] `infra/README.md` updated — Reflect SQLite + Azure Files instead of Azure SQL in Bicep description

---

## Phase 4: Azure resource cleanup

**User stories**: 13

### What to build

Delete unused Azure resources that were only needed for SQL Server. Remove the one-time migration workflow created in Phase 2. Remove the Key Vault SQL Server password secret.

### Acceptance criteria

- [x] Azure SQL Server (`gasoholic-sql`) deleted
- [x] Azure SQL Database (`gasoholic` on `gasoholic-sql`) deleted
- [x] Duplicate container registry (`gasoholicgasacr`) deleted
- [x] Key Vault SQL Server password secret removed
- [x] `.github/workflows/migrate-sqlserver-to-sqlite.yml` deleted from repo
- [x] `.claude/AZURE.md` updated — Remove Azure SQL references, update architecture notes for SQLite
- [x] No remaining references to SQL Server in the codebase (connection strings, env vars, comments)
