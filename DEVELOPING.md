# Gasoholic — Development & Deployment Guide

## Local development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10)
- SQLite (bundled via NuGet — no install needed)

### Run locally

```bash
dotnet run
```

Open [http://localhost:5000](http://localhost:5000). The SQLite database (`gasoholic.db`) is created automatically on first run.

### Environment

| Variable | Default | Notes |
|---|---|---|
| `DATABASE_PROVIDER` | `sqlite` | `sqlite` or `sqlserver` |
| `ConnectionStrings__DefaultConnection` | `Data Source=gasoholic.db` | SQLite path |
| `ConnectionStrings__SqlServer` | _(none)_ | Azure SQL connection string |
| `CORS_ORIGINS` | `http://localhost:5000,https://localhost:5001` | Comma-separated allowed origins |
| `ASPNETCORE_ENVIRONMENT` | `Development` | Set to `Production` in Azure |

### Build

```bash
dotnet build
dotnet build -c Release
```

---

## Docker (local testing)

### Build

```bash
# x64 (production / CI):
docker build -t gasoholic .

# Apple Silicon (Docker Desktop uses an x86 VM — build arm64 natively):
docker buildx build --platform linux/arm64 -t gasoholic --load .
```

### Run

```bash
# x64:
docker run -e DATABASE_PROVIDER=sqlite \
  -e "ConnectionStrings__DefaultConnection=Data Source=/tmp/gasoholic.db" \
  -p 8080:8080 gasoholic

# Apple Silicon:
docker run --platform linux/arm64 \
  -e DATABASE_PROVIDER=sqlite \
  -e "ConnectionStrings__DefaultConnection=Data Source=/tmp/gasoholic.db" \
  -p 8080:8080 gasoholic
```

Health check: `curl http://localhost:8080/health` → `{"status":"ok"}`

---

## Deploying to Azure

### One-time setup

**1. Install Azure CLI and log in**

```bash
brew install azure-cli
az login
```

**2. Create a resource group**

```bash
az group create --name gasoholic-rg --location eastus
```

**3. Register required providers** (new subscriptions only)

```bash
az provider register --namespace Microsoft.Sql --wait
az provider register --namespace Microsoft.Web --wait
az provider register --namespace Microsoft.KeyVault --wait
```

**4. Provision infrastructure**

```bash
az deployment group create \
  --resource-group gasoholic-rg \
  --template-file infra/main.bicep \
  --parameters appName=gasoholic sqlAdminPassword='YourStr0ngP@ss!'
```

> The SQL admin password is marked `@secure()` in Bicep — it is not logged in deployment history.
> After provisioning it is stored in Key Vault. You can discard it.

Default tier is B1 Basic (~$13/mo). To start with the free F1 tier:

```bash
--parameters appName=gasoholic sqlAdminPassword='...' appServiceSku=F1
```

**5. Create the session cache table** (SQL Server distributed session)

```bash
CONN=$(az keyvault secret show --vault-name gasoholic-kv --name SqlConnection --query value -o tsv)
dotnet tool install --global dotnet-sql-cache 2>/dev/null || true
dotnet sql-cache create "$CONN" dbo SessionCache
```

**6. Run database migrations**

```bash
CONN=$(az keyvault secret show --vault-name gasoholic-kv --name SqlConnection --query value -o tsv)
DATABASE_PROVIDER=sqlserver dotnet ef database update --connection "$CONN"
```

**7. Connect GitHub Actions**

- Azure Portal → App Service → **Overview** → **Get publish profile** → download
- GitHub → repo **Settings** → **Secrets and variables** → **Actions** → **New repository secret**
  - Name: `AZURE_WEBAPP_PUBLISH_PROFILE`
  - Value: contents of the downloaded file
- Push to `main` — CI builds and deploys automatically

**8. Tighten SQL firewall** (recommended)

- Azure Portal → SQL Server → **Networking**
- Add your App Service's outbound IPs (found under App Service → **Properties**)
- Remove the broad `AllowAzureServices` rule

---

## Subsequent deployments

Every push to `main` triggers `.github/workflows/azure-deploy.yml` — no manual steps needed.

Database migrations run automatically on startup (`Database.Migrate()` in `Program.cs`).

---

## Project structure

```
gasoholic.csproj      .NET 10 Web API project
Program.cs            App entry point, DI, middleware
Data/                 EF Core DbContext
Endpoints/            Auth, Autos, Fillups minimal API endpoints
Migrations/           EF Core migration files
Models/               Entity models and enums
wwwroot/              Static frontend (HTML/CSS/JS)
infra/
  main.bicep          Azure infrastructure (App Service, SQL, Key Vault)
  README.md           Detailed Azure deployment guide
.github/
  workflows/
    azure-deploy.yml  CI/CD pipeline
Dockerfile            Multi-stage container build
```

---

## Cost estimate

| Resource | Tier | $/mo |
|---|---|---|
| App Service Plan | F1 Free | $0 (60 min/day CPU cap, no always-on) |
| App Service Plan | B1 Basic | ~$13 (recommended for production) |
| Azure SQL Database | Basic 5 DTU | ~$5 |
| Key Vault | Standard | ~$0.03 |
