# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

`gasoholic` is a fuel fillup tracker — .NET 10 Minimal API + Cosmos DB (EF Core Cosmos provider) backend, Angular frontend in `client/`.

Session interaction log: `.claude/FAFO.md`

prds go in `.claude/prd/`

plans go in `.claude/plans/`

## Workflow Rules

- **After completing any plan task:** append a summary of the changes to `.claude/FAFO.md` under the current session date heading. Include the task number, what was changed (files + key details), and why.

## Cosmos / EF conventions — **READ BEFORE TOUCHING `Data/`, `Models/`, `Endpoints/`, or `infra/main.bicep`**

Before writing or modifying any Cosmos-backed entity, DbContext config, endpoint query, or partition-key path, read `NAMING.md` at the repo root. Two rules you MUST follow:

1. **Cosmos JSON property names are camelCase.** `AppDbContext.OnModelCreating` runs `ApplyCamelCaseJsonPropertyNames` at the end to enforce this — do NOT add explicit `.ToJsonProperty(…)` calls; the convention handles them. Hand-written Cosmos SQL in this repo (REST API queries, diagnostics, one-off scripts) must also use camelCase field references.
2. **Partition-key JSON paths must match `infra/main.bicep` exactly.** When adding an entity, update `AppDbContext`, `main.bicep`, the table in `NAMING.md`, AND the `expected` array in `Tests/NamingConventionTests.cs::PartitionKeyFields_MatchBicepPaths`. All four must agree.

Both rules are gated by `Tests/NamingConventionTests.cs`, which runs on every CI deploy via `dotnet test Tests/Tests.csproj`. If you change the model and don't run tests locally first, assume you will break the deploy.

### Cosmos gotchas that have bitten us (don't repeat)

- **`AnyAsync` with partition-key predicate on `VerificationTokens`** miscompiles in EF Core 10.0.5 Cosmos ("Identifier 'root' could not be resolved" SC2001). Use `.Where(…).Take(1).ToListAsync()` instead. See `Endpoints/AuthEndpoints.cs` for the pattern.
- **`FindAsync(id)` on composite keys** (Auto, Fillup, Maintenance, VerificationToken — any entity where partition key ≠ primary key) throws because Cosmos composite keys need both values. Use `FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId)`.
- **Multi-field `OrderBy(...).ThenBy(...)`** on a Cosmos container without a matching composite index returns a BadRequest. Sort in memory after `ToListAsync()` for small per-user result sets, or add a composite index in bicep.
- **Sub-queries inside a LINQ projection** (`.Select(a => new { Latest = db.Fillups.Where(f => f.AutoId == a.Id)... })`) don't translate. Run separate per-partition queries in a loop.

### Before any change that touches Cosmos schema or queries

1. Run `dotnet test Tests/Tests.csproj` locally.
2. Run `./smoke-test.sh https://gas.sdir.cc "$(az keyvault secret show --vault-name gasoholic-kv --name SmokeTestSecret --query value -o tsv)"` after deploying to dev or prod.
3. If you purge Cosmos containers, remember the app's `EnsureCreatedAsync` will re-create them with partition-key paths derived from the EF model (not bicep). Make sure those agree or you'll hit "requested partition key path does not match existing Container".
