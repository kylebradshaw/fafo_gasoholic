# Azure One-Time Manual Setup Checklist

These steps are done once by you before CI/CD can run autonomously. Everything below this section is assumed complete during the automated build and deploy.

## Step 1 — Azure account and tooling

- [X] Create an Azure account at azure.microsoft.com (free tier gives $200 credit for 30 days)
- [X] `brew install azure-cli` (or `winget install Microsoft.AzureCLI` on Windows)
- [X] `az login` — authenticate in browser

## Step 2 — Resource group

- [X] Choose a region (`eastus` — East US, all resources deployed here)
- [X] `az group create --name gasoholic-rg --location eastus`

## Step 3 — Provision infrastructure

- [X] `./deploy.sh` — single script handles full provision + image push + deploy
  - Provisions: ACR (Basic), Container Apps Environment (consumption, scales to zero), Container App
  - `AZURE_CREDENTIALS` service principal created automatically, stored in `.deploy-secrets`
  - No SQL Server needed — SQLite at `/tmp/gasoholic.db` in the container (ephemeral per revision)
- [X] All resources appear in Azure Portal under `gasoholic-rg` in East US

## Step 4 — GitHub Actions secret

- [X] `AZURE_CREDENTIALS` secret set in GitHub repo (written by `deploy.sh` to `.deploy-secrets`, then uploaded via `gh secret set`)
- [X] Push to `main` triggers CI/CD: build → push to ACR → deploy to Container App

## Step 5 — Live deployment (confirmed 2026-03-22)

- **App URL:** `https://gasoholic.yellowcliff-a9ca470c.eastus.azurecontainerapps.io`
- `/health` → `{"status":"ok"}`
- `/auth/me` → HTTP 401 (unauthenticated, as expected)
- Login, add auto, add fillup — all working
- GitHub Actions CI/CD: push to `main` deploys in ~1m12s ✓

## Architecture notes

- **Region:** East US (`eastus`) for all resources
- **Database:** SQLite at `/tmp/gasoholic.db` — ephemeral per Container App revision
  - Data does NOT persist across redeployments (acceptable for demo; see Task 13+ for upgrade path)
  - Azure Files SMB (port 445) is blocked on Container Apps consumption plan — investigated and ruled out
  - Future option: migrate to Azure SQL Flexible Server or Container Apps with VNet + NFS Azure Files
- **Session cache:** in-memory (`AddDistributedMemoryCache`), cleared on restart
- **Scale:** 0–1 replicas; scales to zero when idle (free when not in use)
- **Cost estimate:** ~$5/mo (ACR Basic) + negligible Container Apps consumption billing

---

> Everything below this line is reference material from prior planning conversations.

---

 /btw is this the most secure approach? how do I set up my microsoft cloud environment? this is new, I want it to be
 the lowest cost possible

  /btwis this the most secure approach? how do I set up my microsoft cloud environment? this is new, I want it to be
      the lowest cost possible

    Security assessment of the planned approach

    The plan is reasonable for a personal app but not hardened. Key gaps:

    - No HTTPS enforcement — App Service handles TLS termination, but the app should redirect HTTP → HTTPS
    - SQL admin password as a Bicep parameter — this ends up in deployment history in plain text; better to use Azure
    Key Vault
    - Publish profile as a GitHub secret — workable, but the modern best practice is OIDC federated identity (no
    long-lived secrets at all)
    - No network isolation — the SQL server is publicly accessible by default; a production setup would use a private
    endpoint or firewall rules to only allow the App Service IP
    - DATABASE_PROVIDER env var — not a security issue, just worth noting that all sensitive env vars (connection
    strings, passwords) should be stored in Azure Key Vault, not App Service config directly

    For a personal fuel tracker these are acceptable tradeoffs. For anything with other users' data, tighten them.

    ---
    Lowest-cost Azure setup from scratch

    1. Create an account

    Go to azure.microsoft.com → Free account. You get $200 credit for 30 days + some always-free services.

    2. Install tooling

    brew install azure-cli          # or winget install Microsoft.AzureCLI
    az login                        # opens browser auth

    3. Create a resource group

    az group create --name gasoholic-rg --location eastus
    Pick eastus or westus2 — they're among the cheapest regions.

    4. Cheapest viable stack

    ┌────────────────────┬───────────────┬────────────────────────────────────────────────────────────────────┐
    │      Resource      │     Tier      │                            Est. cost/mo                            │
    ├────────────────────┼───────────────┼────────────────────────────────────────────────────────────────────┤
    │ App Service Plan   │ F1 Free       │ $0 (but: no custom domain TLS, 60 min/day CPU limit, no always-on) │
    ├────────────────────┼───────────────┼────────────────────────────────────────────────────────────────────┤
    │ App Service Plan   │ B1 Basic      │ ~$13 (recommended — removes all F1 limitations)                    │
    ├────────────────────┼───────────────┼────────────────────────────────────────────────────────────────────┤
    │ Azure SQL Database │ Basic (5 DTU) │ ~$5                                                                │
    ├────────────────────┼───────────────┼────────────────────────────────────────────────────────────────────┤
    │ Total              │               │ $5–18/mo                                                           │
    └────────────────────┴───────────────┴────────────────────────────────────────────────────────────────────┘

    F1 is fine for testing. Switch to B1 when you want it to feel like a real app.

    5. Provision with the Bicep file (once Task 11 is implemented)

    az deployment group create \
      --resource-group gasoholic-rg \
      --template-file infra/main.bicep \
      --parameters appName=gasoholic sqlAdminPassword='YourStr0ngP@ss!'

    6. Deploy the app

    After provisioning, download the publish profile from the Azure Portal (App Service → Get publish profile), add it
     as a GitHub Actions secret named AZURE_WEBAPP_PUBLISH_PROFILE, then push to main.

    7. Run migrations against Azure SQL

    dotnet ef database update \
      --connection "Server=<your-sql-server>.database.windows.net;Database=gasoholic;..."

    ---
    Bottom line: For a personal app, the plan is secure enough. Start with F1 + Basic SQL ($5/mo), upgrade to B1 when
    you're ready. The biggest real risk is leaving the SQL server publicly accessible — add a firewall rule to
    restrict it to your App Service's outbound IPs once you're provisioned.

---

/btw I want it to be secure with https and a keyvault for db connectivity

Both are standard additions to the Bicep and app config. Here's how they fit in:

HTTPS

Azure App Service enforces HTTPS automatically — you just need to enable two settings on the Web App resource in
Bicep:

httpsOnly: true  // redirects HTTP → HTTPS

For a custom domain you'd also add a managed certificate (Microsoft.Web/sites/hostNameBindings +
Microsoft.Web/certificates), but if you use the default *.azurewebsites.net domain, TLS is provided free with no
extra config.

Key Vault for the DB connection string

The pattern is:

1. Bicep provisions a Key Vault, stores the SQL connection string as a secret, and grants the Web App's managed
identity get/list secret access via a role assignment.
2. App Service app setting references the secret using a Key Vault reference instead of the raw value:
@Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/SqlConnection/)
2. App Service resolves this at runtime — your app code just reads it as a normal env var/connection string and
never sees the vault.
3. The Web App needs a system-assigned managed identity (identity: { type: 'SystemAssigned' }), which is what the
role assignment targets.

No changes needed in Program.cs — the Key Vault reference is transparent to the app. The connection string arrives
 as a plain string; only App Service knows it came from the vault.

These two items are clean additions to Task 11's Bicep work — httpsOnly: true is one line, and the Key Vault
pattern adds a vault resource, a secret resource, and a role assignment. Worth calling out explicitly in the
task's acceptance criteria so they don't get skipped.