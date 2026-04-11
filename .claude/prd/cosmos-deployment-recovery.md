# Cosmos Deployment Recovery

## Problem Statement

After pivoting the data layer from SQLite to Cosmos DB, every push to `main` has failed to produce a healthy Container App revision. The deployed `gasoholic` Container App is still serving the pre-Cosmos image `gasoholicacr.azurecr.io/gasoholic:wal-fix-1775877246`, and the most recent two GitHub Actions runs have failed — the latest during Angular compilation, the one before that during Playwright smoke tests. The team needs new commits to actually reach production again.

## Solution

Fix the Angular compile error that is blocking the Docker build, strip the SQLite-era steps from the deploy workflow, and have the workflow automatically reconcile the Container App's runtime shape (remove the stale `sqlite-data` volume mount) on every push. After this, `git push` to `main` should build, publish, deploy, and pass smoke tests against the real Cosmos-backed backend with no manual Azure clicks.

Azure infrastructure is already in the correct state and will not be touched:

- `gasoholic-cosmos` account, `gasoholic` database, and the `Users` / `Autos` / `Fillups` / `Maintenance` / `VerificationTokens` containers all exist, and their partition keys match `Data/AppDbContext.cs` exactly (`/id`, `/userId`, `/autoId`, `/autoId`, `/userId`).
- `gasoholic-kv` holds `CosmosConnection`, `AcsConnection`, `AcsSenderDomain`, and `SmokeTestSecret`.
- The Container App already has `ConnectionStrings__Cosmos` wired to the `cosmos-connection` Key Vault secret via its system-assigned managed identity.

So this PRD is not an infrastructure project. It is a build-fix-plus-workflow-hygiene project. Bicep is deliberately left alone.

## User Stories

1. As the developer, I want `git push origin main` to produce a new, healthy Container App revision, so that I can ship changes without manual Azure intervention.
2. As the developer, I want the Angular production build to compile after the int-to-GUID primary key reshape, so that the Docker image stage succeeds.
3. As the developer, I want the deploy workflow to stop trying to download a SQLite file that never existed in the Cosmos era, so that logs stay clean and no misleading "backup not found" warnings appear.
4. As the developer, I want the workflow to remove the orphan `sqlite-data` AzureFile volume mount from the Container App template automatically, so that the running revision accurately reflects the Cosmos-only architecture.
5. As the developer, I want the workflow to fail loudly if `ConnectionStrings__Cosmos` is ever missing from the deployed revision, so that a silently misconfigured app cannot reach production.
6. As the developer, I want Playwright smoke tests to run against the freshly deployed revision and block the workflow on failure, so that a broken backend is caught before I find out from the UI.
7. As the developer, I want the legacy `gasoholicdata` storage account to be clearly marked as unused without actually being deleted, so that I know at a glance it is safe to ignore but can still recover anything inside it.
8. As the developer, I want the `selectedAutoId` signal in the app shell to be a plain `string` rather than a hybrid `number | string`, so that future code touching auto IDs does not reintroduce numeric parsing.
9. As the developer, I want the deploy workflow's environment variables to only reference resources that actually matter to the Cosmos-backed app, so that the workflow definition is self-documenting.
10. As a user loading the app in a browser, I want the backend to respond to `/auth/dev-login` and `/api/autos` correctly, so that the smoke suite passes and my real session works.
11. As the developer, I want a single place to read about why this deployment was broken and what was done to fix it, so that a future "it broke again" session has context.

## Implementation Decisions

### Modules to modify

**Angular app shell (build unblocker).** The `selectedAutoId` signal and its change handler in the app-shell feature are the only TypeScript consumers still treating auto IDs as numeric. The signal becomes `signal<string>('')`, the change handler drops its `parseInt` round-trip, and the handler passes the string straight through to `AutosService.setCurrentAuto`. `AutosService` itself is already correct — its field is `signal<string | null>` and its setter takes `string`. No service changes.

**GitHub Actions deploy workflow.** The workflow loses its SQLite backup concerns entirely and gains two Azure reconciliation steps that run on every push. Concretely:

- Delete the `STORAGE_ACCOUNT`, `FILE_SHARE`, and `BACKUP_RETENTION_DAYS` entries from the workflow `env` block.
- Delete the "Download SQLite database from Azure Files", "Export SQLite tables to CSV", and "Upload CSV backup artifacts" steps.
- After the existing `az containerapp update --image …` step, add a "Reconcile Container App template" step that issues `az containerapp update` with `--remove-all-volume-mounts` (and, if the revision still carries the template-level `sqlite-data` volume, a follow-up `az containerapp update --yaml` or `--remove-volume sqlite-data` to drop the volume declaration itself). The step is idempotent: if there are no volumes, it is a no-op.
- Add a "Verify Cosmos wiring" step immediately after reconciliation that asserts the active revision exposes `ConnectionStrings__Cosmos` pointing at the `cosmos-connection` secret. Implementation: `az containerapp show` piped through `jq` (or `--query`), failing the job if the env var is missing. This is the contract guard that prevents a silent regression.
- Keep the existing Playwright smoke-test step as the end-to-end gate. It already waits up to three minutes for `/health` to return 200 before running the suite, which is the right behavior for a just-replaced revision.

**Legacy storage account marking.** Storage account *names* in Azure are immutable, so "rename to `gasoholicdata-LEGACY`" is implemented via resource tagging rather than a true rename. A workflow step (or a one-off in the reconciliation step) applies a `legacy=true` and `status=unused-post-cosmos-pivot` tag pair to `gasoholicdata`. In the Portal the tags surface next to the name, which gives the same "this is dead" signal without forcing a delete-and-recreate. Deleting the account is explicitly out of scope.

### Interfaces touched

- `AutosService.setCurrentAuto(id: string)` — unchanged. The contract is already right; the caller is the bug.
- `AppShellComponent.selectedAutoId` — type narrows from `number | string` to `string`.
- `AppShellComponent.onAutoChange()` — drops numeric parsing; passes the raw signal value to the service.
- Workflow `env` block — shrinks.
- Workflow job graph — gains "Reconcile Container App template" and "Verify Cosmos wiring" steps between "Deploy to Container App" and "Smoke test".

### Architectural decisions

- **Bicep stays frozen.** Per direction, the workflow does not run `az deployment group create`. Infrastructure is treated as an established, hand-managed artifact. The bicep file remains in the repo for reference and disaster recovery, but the hello-world image default and the missing-volume drift are known and tolerated; they are landmines only if someone manually re-applies bicep, which this PRD does not do.
- **Drift is reconciled at the Container App layer, not the bicep layer.** Removing the `sqlite-data` volume is done with an `az containerapp` call, not by re-rendering the template from bicep. This keeps bicep-vs-reality divergence visible but harmless.
- **Cosmos wiring is guarded at deploy time, not at startup.** `Program.cs` already throws on missing `ConnectionStrings__Cosmos`, but that only surfaces after the image rolls out and the health probe fails. The workflow verification step catches the same class of regression earlier and with a clearer error.
- **Smoke tests remain the final gate.** They are the only check that exercises Cosmos reads and writes end-to-end, so they are the authoritative answer to "did the Cosmos pivot actually work in production." Keeping them load-bearing is deliberate.

### Schema and API contracts

None. All IDs are already `string` (GUID) in the backend and in `AutosService`. This PRD only aligns one Angular component with that existing contract.

## Testing Decisions

A good test here exercises *external behavior at a deploy boundary*, not implementation details of individual workflow steps. Two tests matter:

1. **`ng build --configuration production` at the Angular layer.** Runs locally before commit and inside the Docker build stage on every push. If the `app-shell.component.ts` fix is wrong, the TypeScript compiler is the fastest, loudest way to know. No unit test can do better than the compiler here.
2. **Playwright smoke tests against the deployed revision.** This is the prior art — the `e2e/` project already runs `test:smoke` against `BASE_URL` in CI and hits `/auth/dev-login`, `/auth/me`, `/api/autos`, and `/auth/dev-cleanup`. It is the only suite that proves Cosmos reads and writes actually work in the deployed environment. The prior failed run (`24270545903`) shows exactly the class of failure it catches: eleven assertions firing when the backend could not serve authenticated requests. No new smoke tests are needed; keeping the existing suite green is the bar.

The workflow's new "Verify Cosmos wiring" step is effectively a third test, but it is a static assertion over `az containerapp show` output rather than a behavioral test. It is cheap and catches the specific regression class of "managed identity lost its KV role" or "someone removed the env var in a manual portal edit."

No new unit tests are written for `AppShellComponent.onAutoChange`. The component is shallow and the fix is a type correction; the production build is a sufficient regression guard.

## Out of Scope

- Any bicep edits. Infra is established and hand-managed.
- Running `az deployment group create` from the workflow.
- Deleting the `gasoholicdata` storage account. It is tagged, not removed.
- Deleting the unused Azure Files `data` share contents.
- Adding new Cosmos containers, indexes, or TTLs.
- Changing partition keys or EF Core model configuration.
- Data migration from the legacy SQLite file. Per direction, SQLite never held real data.
- Refactoring `AutosService`, `App-Shell` beyond the minimum needed to compile.
- Writing new Playwright tests.
- Removing the `gasoholic-logs` Log Analytics workspace or any ACS resources.
- Introducing a separate staging environment or blue/green deploys.

## Further Notes

- The currently running image is `gasoholicacr.azurecr.io/gasoholic:wal-fix-1775877246`. It is a pre-Cosmos SQLite build and will be replaced by the first successful post-fix push. Any state it holds in the `sqlite-data` volume is presumed worthless.
- `Program.cs` already wires Cosmos correctly for both Development and Production and throws on a missing connection string, so once the image actually rolls out, the backend should initialize cleanly against the existing Cosmos account.
- The Container App's managed identity `53207aa7-f35d-45c5-a499-858997f91ec4` already has the Key Vault secrets-user role binding; no RBAC changes are required.
- Session log entry for this work should go in `.claude/FAFO.md` per the project workflow rule after each task lands.
- Related prior PRDs: `.claude/prd/database-transition.md` and `.claude/prd/cosmosdb.md` describe the pivot itself and are the "why" behind the broken deploy. This PRD is the cleanup that the pivot deferred.
