## Problem Statement

Gasoholic is a low-traffic PWA (<5 users/month, <100 visits/month) running on Azure Container Apps with an Azure SQL Database (General Purpose Serverless, Gen5, 1 vCore). The SQL Server tier costs ~$5+/month in storage and compute charges — overkill for a 28 MB dataset. Additionally, a duplicate container registry (`gasoholicgasacr`) adds unnecessary cost. The current infrastructure was built for scale that hasn't materialized, and the ongoing Azure bill is disproportionate to actual usage.

## Solution

Replace Azure SQL Database with SQLite stored on an Azure Files volume mounted to the Container App. This eliminates the database server entirely, reduces monthly cost to effectively $0 (Azure Container Apps consumption tier free allowance + pennies for Azure Files storage), and simplifies the entire stack. After migration, delete unused Azure resources (SQL Server, SQL Database, duplicate container registry) to eliminate ongoing cost.

## User Stories

1. As a developer, I want the production database to run on SQLite, so that I pay $0/month for data storage instead of ~$5+/month for Azure SQL.
2. As a developer, I want a one-time data migration from SQL Server to SQLite, so that no existing production data is lost during the transition.
3. As a developer, I want the migration workflow to be re-runnable via manual trigger, so that I can retry if it fails without worrying about partial state.
4. As a developer, I want the migration workflow to verify row counts between source and target, so that I can confirm data integrity before cutting over.
5. As a developer, I want CSV backups of all tables uploaded as GitHub Actions artifacts during migration, so that I have a fallback copy of the data regardless of migration outcome.
6. As a developer, I want an automatic CSV backup step in the CI deploy pipeline, so that every production deploy snapshots the current data before shipping new code.
7. As a developer, I want CSV backup artifacts retained in GitHub Actions history, so that I can retrieve historical snapshots if needed.
8. As a developer, I want session storage to use in-memory distributed cache instead of SQL Server distributed cache, so that there is no dependency on SQL Server for sessions.
9. As a user, I accept that a container restart requires re-login, since session state is in-memory and the app has fewer than 5 active users.
10. As a developer, I want local development to use a SQLite file on disk instead of a Docker Compose SQL Server container, so that local setup is simpler and faster.
11. As a developer, I want integration tests to use temporary SQLite files instead of per-test SQL Server databases, so that tests run faster and don't require SA_PASSWORD or Docker.
12. As a developer, I want EF Core migrations regenerated fresh for SQLite, so that the migration history is clean and provider-specific SQL is eliminated.
13. As a developer, I want unused Azure resources deleted after migration (SQL Server, SQL Database, duplicate container registry), so that they stop incurring monthly charges.
14. As a developer, I want the Docker Compose file and all SQL Server local dev infrastructure removed, so that the codebase has no dead references to the old stack.
15. As a developer, I want the SQLite database file stored on an Azure Files volume, so that data persists across container restarts and redeployments.
16. As a developer, I want the Dockerfile to remain compatible with the Azure Files SMB mount at `/data`, so that SQLite can write to persistent storage in production.

## Implementation Decisions

### Database Layer
- Replace `Microsoft.EntityFrameworkCore.SqlServer` NuGet package with `Microsoft.EntityFrameworkCore.Sqlite`
- Remove `Microsoft.Extensions.Caching.SqlServer` NuGet package
- Update `Program.cs` to use `options.UseSqlite(connStr)` instead of `options.UseSqlServer(connStr)`
- Replace `AddDistributedSqlServerCache` with `AddDistributedMemoryCache` — sessions are lost on container restart, acceptable at current scale
- Connection string changes from SQL Server format to SQLite format (e.g., `Data Source=/data/gasoholic.db`)
- Update `AppDbContextFactory` (design-time) to target SQLite instead of SQL Server
- Remove SA_PASSWORD dependency from all application and test code

### Migrations
- Delete all existing migrations in `Migrations/` (SQL Server-specific)
- Generate a fresh `InitialCreate` migration targeting SQLite
- `Database.Migrate()` on startup creates the schema (existing behavior, no change)

### Session Storage
- Replace `AddDistributedSqlServerCache` with `AddDistributedMemoryCache`
- Remove SessionCache table dependency — no SQL-based session storage needed
- Tests already use `AddDistributedMemoryCache`, so test infrastructure is unaffected

### One-Time Data Migration (GitHub Actions workflow)
- New `workflow_dispatch`-triggered workflow: `.github/workflows/migrate-sqlserver-to-sqlite.yml`
- Connects to Azure SQL Database and exports Users, Autos, Fillups, MaintenanceRecords, VerificationTokens tables to CSV
- Uploads CSVs as GitHub Actions artifacts (backup regardless of migration outcome)
- Creates a new SQLite database, imports CSVs, preserving primary key values
- Verifies row counts match between SQL Server source and SQLite target
- Uploads the SQLite file to the Azure Files volume
- Fully idempotent — recreates SQLite DB from scratch on each run, safe to retry
- Delete this workflow after successful migration

### CI/CD Backup Step
- Add a pre-deploy step in `azure-deploy.yml` that downloads the SQLite file from Azure Files
- Dump all tables to CSV using a lightweight script
- Upload CSVs as GitHub Actions artifacts with configurable retention
- This runs automatically before every deploy — zero maintenance

### Local Development
- Delete `docker-compose.yml` (no more local SQL Server container)
- Local dev uses a SQLite file on disk (e.g., `gasoholic.db` in project root, added to `.gitignore`)
- No Docker dependency for local development

### Integration Tests
- Update `GasoholicWebAppFactory` to create per-test temporary SQLite databases instead of SQL Server databases
- Remove SQL Server connection string logic, SA_PASSWORD requirement, and `DROP DATABASE` teardown
- Tests become faster and simpler — no external database server needed

### Azure Resource Cleanup (post-migration)
- Delete `gasoholic-sql` (SQL Server)
- Delete `gasoholic` database on `gasoholic-sql` (SQL Database)
- Delete `gasoholicgasacr` (unused duplicate Container Registry)
- Keep: `gasoholic` (Container App), `gasoholic-env` (Container Apps Environment), `gasoholicdata` (Storage Account for Azure Files), `gasoholic-kv` (Key Vault), `gasoholic-logs` (Log Analytics), `gasoholic-acs`/`gasoholic-comms`/email domains (active email verification)

### Configuration
- Update `appsettings.json` connection string key from `SqlServer` to `Sqlite` (or similar)
- Production connection string points to Azure Files mount path (e.g., `Data Source=/data/gasoholic.db`)
- Remove Key Vault SQL Server password secret after migration is verified

## Testing Decisions

Good tests verify external behavior through the public API, not internal implementation details. Tests should confirm that the application works correctly with SQLite as the backing store.

### Modules to test
- **All existing integration tests** (`AuthEndpointTests`, `AutoEndpointTests`, `FillupEndpointTests`, `MpgComputationTests`) — these must pass unchanged against SQLite, validating that the database swap is transparent to the API layer
- **Data migration verification** — the migration workflow includes built-in row count verification between SQL Server source and SQLite target

### Prior art
- `Tests/IntegrationTestBase.cs` and `Tests/GasoholicWebAppFactory.cs` — existing integration test infrastructure. The factory will be simplified to use SQLite instead of SQL Server, but the test patterns remain identical.
- The existing tests already use `AddDistributedMemoryCache` for session storage, so no session-related test changes are needed.

## Out of Scope

- Changing the Container Apps hosting model or compute tier
- Modifying the Angular frontend
- Changing the email/communication services setup
- Adding new application features
- Database schema changes beyond what's needed for SQLite compatibility
- Automated scheduled backups outside of the CI deploy pipeline
- Multi-region replication or high-availability configuration

## Further Notes

- **Data volume context:** The production database is 28.56 MB with 0.09% of 32 GB used. SQLite handles this trivially.
- **Concurrency:** At <5 users/month, SQLite's write locking is a non-issue. WAL mode should be enabled for best concurrent read performance.
- **Azure Files volume:** The Container App already runs as root in the Dockerfile to support Azure Files SMB mount writes. This setup continues to work for SQLite.
- **Rollback plan:** The CSV artifacts from the migration workflow serve as the rollback data source. If SQLite proves problematic, the CSVs can be re-imported into a new Azure SQL Database.
- **Cost savings estimate:** Eliminating Azure SQL (~$5+/month), duplicate container registry (~$5/month), and staying within Container Apps free tier should reduce the monthly bill to near $0 for compute + storage.