# Naming conventions

One page. Two rules. Don't skim.

## Rule 1 — Cosmos JSON property names are **camelCase**

Every property on every Cosmos-backed entity is serialized as camelCase in the document:

```jsonc
// stored in Cosmos
{
  "id":           "63f1a161-…",
  "email":        "gas@sdir.cc",
  "emailVerified": true,
  "lastSignIn":   "2026-04-11T18:11:23Z"
}
```

**Why:** EF Core 10 Cosmos defaults to PascalCase (matching C# property names), but the partition-key paths declared in `infra/main.bicep` are camelCase (`/userId`, `/autoId`). A single convention eliminates the mismatch that caused the "SC2001 Identifier could not be resolved" / "partition key path does not match" class of outages on 2026-04-11.

**How it's enforced:**

1. `Data/AppDbContext.cs` → `ApplyCamelCaseJsonPropertyNames()` runs at the end of `OnModelCreating` and rewrites every property's JSON name to camelCase. This means you **do not** need to call `.ToJsonProperty("…")` on individual properties — it happens automatically for every new entity and every new property.
2. `Tests/NamingConventionTests.cs::AllCosmosJsonPropertyNames_AreCamelCase` fails the build if any property (other than Cosmos system fields `id`, `_etag`, `_ts`, `__type`) starts with an uppercase letter. This is the lint gate — it runs on every `dotnet test` and on every CI deploy.

**Hand-written Cosmos SQL must use camelCase too:**

```sql
-- ✓ correct
SELECT c.id, c.email, c.emailVerified FROM c WHERE c.email = @e

-- ✗ wrong — will silently return 0 rows
SELECT c.Id, c.Email, c.EmailVerified FROM c WHERE c.Email = @e
```

**Exceptions:** the Cosmos reserved system fields keep their underscore-prefixed names (`_etag`, `_ts`, `_rid`, `_self`, etc.) and the primary key is always stored at the reserved `id` field — both handled by EF and the convention skips them.

## Rule 2 — Partition-key JSON paths must match `infra/main.bicep` exactly

The partition key path in bicep (`/userId`, `/autoId`, `/id`) is the source of truth for container layout. The EF model must produce documents where the partition-key property lands at exactly that path.

**Current mapping** (must match `infra/main.bicep:93-147`):

| Entity              | Cosmos container      | Partition key path | CLR property |
|---------------------|-----------------------|--------------------|--------------|
| `User`              | `Users`               | `/id`              | `Id`         |
| `Auto`              | `Autos`               | `/userId`          | `UserId`     |
| `Fillup`            | `Fillups`             | `/autoId`          | `AutoId`     |
| `MaintenanceRecord` | `Maintenance`         | `/autoId`          | `AutoId`     |
| `VerificationToken` | `VerificationTokens`  | `/userId`          | `UserId`     |

**How it's enforced:** `Tests/NamingConventionTests.cs::PartitionKeyFields_MatchBicepPaths` fails the build if any entry above drifts from the EF model.

## Adding a new entity

1. Add the model class under `Models/`.
2. Register it in `Data/AppDbContext.cs` with `ToContainer("…")` and `HasPartitionKey(x => x.Foo)`. Do **not** add `.ToJsonProperty(…)` — the convention handles it.
3. Add the container to `infra/main.bicep` with a camelCase `partitionKey.paths` entry.
4. Add a row to the table above.
5. Add an entry to the `expected` array in `PartitionKeyFields_MatchBicepPaths`.
6. `dotnet test` locally before pushing. The two naming-convention tests must pass.

## Things the convention does **not** cover

- **C# code style** (PascalCase classes, camelCase locals, etc.) — use normal .NET conventions; `.editorconfig` handles it.
- **JSON responses from the API** — those go through `System.Text.Json` and may use different casing (currently PascalCase because no `JsonNamingPolicy` is set). If you want API responses camelCase too, that's a separate change in `Program.cs` (`ConfigureHttpJsonOptions` → `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`).
- **Angular client field names** — separately conventional; unaffected by this document.
