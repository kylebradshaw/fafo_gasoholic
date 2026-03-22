# Gasoholic — Azure Deployment Guide

## Prerequisites

```bash
brew install azure-cli       # macOS
az login                     # opens browser authentication
```

## Estimated monthly cost

| Resource | Tier | Cost/mo |
|---|---|---|
| App Service Plan | B1 Basic (Linux) | ~$13 |
| Azure SQL Database | Basic 5 DTU | ~$5 |
| Key Vault | Standard | ~$0.03 |
| **Total** | | **~$18** |

> Start with B1. The app isn't designed for F1 free tier (no always-on, 60 min/day CPU cap).

---

## Step 1 — Create a resource group

Pick `eastus` or `westus2` for lowest cost.

```bash
az group create --name gasoholic-rg --location eastus
```

## Step 2 — Provision infrastructure

```bash
az deployment group create \
  --resource-group gasoholic-rg \
  --template-file infra/main.bicep \
  --parameters appName=gasoholic sqlAdminPassword='YourStr0ngP@ss!'
```

This provisions: App Service Plan, Web App (HTTPS-only, managed identity), Azure SQL (Basic),
Key Vault (connection string stored as a secret, referenced by the Web App automatically).

> The `sqlAdminPassword` is a `@secure()` parameter — it is **not** logged in deployment history.
> After provisioning, the password is stored in Key Vault. You can discard it.

Note the outputs:
- `appUrl` — your app URL (e.g. `https://gasoholic.azurewebsites.net`)
- `sqlServerFqdn` — the SQL server hostname
- `keyVaultUri` — the Key Vault URI

## Step 3 — Create the SessionCache table for distributed sessions

SQL Server distributed sessions require a table. Run this once:

```bash
# Get the connection string from Key Vault
CONN=$(az keyvault secret show \
  --vault-name gasoholic-kv \
  --name SqlConnection \
  --query value -o tsv)

# Create the session cache table
dotnet tool install --global dotnet-sql-cache
dotnet sql-cache create "$CONN" dbo SessionCache
```

## Step 4 — Run database migrations

Apply EF Core migrations to Azure SQL:

```bash
CONN=$(az keyvault secret show \
  --vault-name gasoholic-kv \
  --name SqlConnection \
  --query value -o tsv)

DATABASE_PROVIDER=sqlserver dotnet ef database update \
  --connection "$CONN"
```

## Step 5 — Set up GitHub Actions deployment

1. In Azure Portal: go to your App Service → **Overview** → **Get publish profile** → download the file
2. In GitHub: **repo Settings → Secrets and variables → Actions → New repository secret**
   - Name: `AZURE_WEBAPP_PUBLISH_PROFILE`
   - Value: paste the entire contents of the downloaded `.PublishSettings` file
3. Delete the downloaded file from your machine
4. Push to `main` — the GitHub Actions workflow deploys automatically

## Step 6 — Tighten SQL firewall (recommended)

The initial Bicep allows all Azure services to reach SQL. After first deploy, restrict it
to your App Service's outbound IPs:

1. Azure Portal → App Service → **Properties** → copy **Outbound IP addresses**
2. Azure Portal → SQL Server → **Networking** → add each IP as a firewall rule
3. Remove the "AllowAzureServices" rule

## Subsequent deployments

Every push to `main` triggers a build + deploy via GitHub Actions. No manual steps needed.

Database migrations run automatically on app startup (`Database.Migrate()` in `Program.cs`).

## Running locally with SQL Server (optional)

To test the SQL Server path locally (e.g. with a dev Azure SQL or local SQL Server):

```bash
export DATABASE_PROVIDER=sqlserver
export ConnectionStrings__SqlServer="Server=...;Database=gasoholic;..."
dotnet run
```

Leave both unset to use the default SQLite path.
