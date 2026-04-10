# Gasoholic — Azure Infrastructure (Bicep)

`infra/main.bicep` defines all Azure resources for Gasoholic using Infrastructure as Code.

## Architecture

- **Azure Container Apps** — runs the .NET app on a consumption plan (scale 0–1)
- **Azure Container Registry (ACR)** — stores Docker images
- **Azure Files (SMB mount)** — persistent storage for the SQLite database at `/data/gasoholic.db`
- **Azure Key Vault** — stores ACS email secrets and smoke test secret
- **Azure Communication Services** — sends magic link emails

## Estimated monthly cost

| Resource | Tier | Cost/mo |
|---|---|---|
| Container Apps | Consumption (1 replica) | ~$0–5 |
| ACR | Basic | ~$5 |
| Azure Files | Standard SMB | ~$0.01 |
| Key Vault | Standard | ~$0.03 |
| **Total** | | **~$5–10** |

## Deploying infrastructure

```bash
az deployment group create \
  --resource-group gasoholic-rg \
  --template-file infra/main.bicep \
  --parameters appName=gasoholic
```

Or use the deploy script:

```bash
./deploy.sh --infra-only
```

## Key outputs

- `appUrl` — the Container App URL
- `acrLoginServer` — ACR login server (e.g. `gasoholicacr.azurecr.io`)
- `keyVaultUri` — Key Vault URI

## Database

SQLite on Azure Files. The database file (`/data/gasoholic.db`) is mounted via SMB from the `gasoholicdata` storage account's `data` file share. EF Core migrations run automatically on container startup.

## Subsequent deployments

Every push to `main` triggers a build + deploy via GitHub Actions. No manual steps needed. Database migrations run automatically on app startup (`Database.Migrate()` in `Program.cs`).
