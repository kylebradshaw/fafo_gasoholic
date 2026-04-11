# Plan: Cosmos Deployment Recovery

> Source PRD: `.claude/prd/cosmos-deployment-recovery.md`

## Architectural decisions

Durable decisions that apply across all phases:

- **Infrastructure posture**: bicep is frozen. No phase runs `az deployment group create`. All Azure reconciliation happens via `az containerapp` / `az storage account` CLI calls inside the GitHub Actions workflow.
- **Deploy trigger**: push to `main` (plus `workflow_dispatch`). No new triggers, no staging environment, no blue/green.
- **Identity**: all Azure CLI calls in the workflow authenticate via the existing `AZURE_CREDENTIALS` service principal. The Container App continues to read Key Vault secrets via its existing system-assigned managed identity `53207aa7-f35d-45c5-a499-858997f91ec4`.
- **Cosmos contract**: database `gasoholic`, containers `Users` (pk `/id`), `Autos` (pk `/userId`), `Fillups` (pk `/autoId`), `Maintenance` (pk `/autoId`), `VerificationTokens` (pk `/userId`). This matches `Data/AppDbContext.cs` and the live account; no phase touches it.
- **Runtime config contract**: the deployed revision must expose `ConnectionStrings__Cosmos` bound to the `cosmos-connection` Key Vault secret reference. This is the invariant the wiring guard enforces.
- **ID type**: all entity primary keys are `string` (GUID). The Angular client treats auto IDs as opaque strings everywhere, including the app-shell selector.
- **Smoke test gate**: the existing `e2e/` Playwright suite running against `BASE_URL=https://gasoholic.yellowcliff-a9ca470c.eastus.azurecontainerapps.io` is the end-to-end verification for every phase. A phase is not done until smoke tests pass post-deploy.
- **Legacy storage**: `gasoholicdata` storage account is retained and tagged, never deleted or renamed (Azure storage account names are immutable).

---

## Phase 1: Angular build fix

**User stories**: 1, 2, 8, 10

### What to build

A minimum end-to-end slice that restores the ability to ship any image at all. The Angular app-shell feature currently passes a `parseInt`-coerced `number` into `AutosService.setCurrentAuto`, which since the GUID PK reshape expects a `string`. This phase narrows the app-shell's `selectedAutoId` signal to `string`, drops the numeric parsing from the change handler, and passes the value straight through to the service. After this phase merges, `git push` to `main` should: compile Angular in the Docker build stage, publish a new image to ACR, deploy a new revision to the Container App, and have the backend initialize against Cosmos successfully.

Smoke tests may still fail in this phase because the stale `sqlite-data` volume mount is still present on the revision template and the workflow still runs the SQLite backup steps. That is expected and explicitly deferred to Phase 2. The bar for this phase is "a Cosmos-era image is running in production and `/health` returns 200."

### Acceptance criteria

- [ ] `ng build --configuration production` compiles cleanly from the `client/` directory with no TypeScript errors.
- [ ] `dotnet build gasoholic.csproj -c Release` still passes.
- [ ] The `selectedAutoId` signal in the app-shell component is typed as `string` (no `number | string` hybrid remains).
- [ ] No remaining `parseInt` / `Number(...)` calls in the app-shell feature that operate on auto IDs.
- [ ] A push to `main` produces a new image in `gasoholicacr` tagged with the commit SHA, and `az containerapp show` reports that image as the active revision's container image.
- [ ] `GET /health` against the deployed Container App returns 200 within three minutes of revision rollout.
- [ ] The backend does not throw `Cosmos connection string not configured` on startup (verify via `az containerapp logs show`).
- [ ] Phase 1 summary appended to `.claude/FAFO.md` per the project workflow rule.

---

## Phase 2: Workflow overhaul and Container App reconciliation

**User stories**: 3, 4, 5, 6, 7, 9, 10, 11

### What to build

A single workflow rewrite that removes every SQLite-era artifact from the deploy pipeline, automates the runtime reconciliation of the Container App, adds a static guard that the Cosmos connection is wired on every revision, and tags the unused storage account as legacy. After this phase, the workflow is self-documenting: every step it runs is directly motivated by the Cosmos-backed architecture, and a completed run leaves no drift between the bicep description of the Container App and its actual runtime shape.

Four concerns land together in this phase because they share the same file, the same review surface, and the same verification step (the Playwright smoke suite). They are:

1. **SQLite residue removal.** Delete the `STORAGE_ACCOUNT`, `FILE_SHARE`, and `BACKUP_RETENTION_DAYS` entries from the workflow `env` block, and delete the three SQLite backup steps (download from Azure Files, export tables to CSV, upload CSV artifact). The workflow no longer references the `data` share or the `gasoholic.db` file at all.
2. **Container App template reconciliation.** After the existing image deploy, the workflow issues an idempotent `az containerapp` call that removes the `sqlite-data` volume mount from the container template and drops the corresponding volume declaration. If the volume is already absent (steady state), the call is a no-op and succeeds.
3. **Cosmos wiring assertion.** Immediately after reconciliation, the workflow queries the active revision with `az containerapp show` and asserts that `ConnectionStrings__Cosmos` is present in the container's env list and bound to the `cosmos-connection` secret. Missing or wrong → job fails with a clear message before smoke tests run. This is the guard that catches "someone edited the Container App in the portal and broke it."
4. **Legacy storage tagging.** A final `az storage account update` call applies `legacy=true` and `status=unused-post-cosmos-pivot` tags to `gasoholicdata`. Idempotent — re-running applies the same tags. No data is read, moved, or deleted.

The Playwright smoke suite runs after all of the above, as it already does, and remains the final gate. When this phase is green, pushing to `main` produces a revision that is architecturally clean, statically verified, and behaviorally tested against real Cosmos reads and writes.

### Acceptance criteria

- [ ] `azure-deploy.yml` `env` block no longer references `STORAGE_ACCOUNT`, `FILE_SHARE`, or `BACKUP_RETENTION_DAYS`.
- [ ] The three SQLite backup steps are deleted from the workflow. No step in the workflow references `gasoholic.db`, `sqlite3`, or the `data` file share.
- [ ] A new step between "Deploy to Container App" and "Smoke test" reconciles the Container App template, and after a successful run `az containerapp show -n gasoholic -g gasoholic-rg` reports zero volumes and zero volume mounts.
- [ ] The reconciliation step is verified idempotent: running the workflow twice in a row against a clean revision succeeds both times with no error.
- [ ] A new step asserts that the deployed revision exposes `ConnectionStrings__Cosmos` bound to `cosmos-connection`. If the assertion is manually broken (e.g. by a test mutation) the workflow fails with a non-zero exit code before smoke tests run.
- [ ] The `gasoholicdata` storage account has `legacy=true` and `status=unused-post-cosmos-pivot` tags applied, verified via `az storage account show -n gasoholicdata -g gasoholic-rg --query tags`.
- [ ] The full Playwright smoke suite (`e2e/` `test:smoke`) passes against the deployed revision in the workflow run.
- [ ] `GET /health` returns 200 and `POST /auth/dev-login` returns 200 in the post-deploy smoke run.
- [ ] No new steps reference bicep, `az deployment group create`, or any infrastructure-provisioning command.
- [ ] Smoke tests run successfully against production deployed Cosmos DB-driven Gasoholic.
- [ ] Phase 2 summary appended to `.claude/FAFO.md` per the project workflow rule.
