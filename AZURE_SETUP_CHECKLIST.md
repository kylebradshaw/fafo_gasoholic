# Gasoholic — Azure Setup Checklist

Complete this checklist to deploy Gasoholic to Azure Container Apps for the first time, or to restore a deployment after resource deletion.

**Estimated time:** 15–30 minutes for infrastructure, 5 minutes for image deploy.

---

## Prerequisites

Before starting, ensure you have:

- [x] Azure subscription (free tier eligible)
- [x] Azure CLI (`az` command) installed and authenticated: `az login`
- [x] Docker installed and running
- [x] Git repository cloned locally
- [x] `deploy.sh` is executable: `chmod +x deploy.sh`
- [x] GitHub repo set up (for CI/CD)

---

## Phase 1: Initial Infrastructure Setup

These steps create all Azure resources. Run them once.

### Step 1.1: Create resource group

```bash
az group create \
  --name gasoholic-rg \
  --location eastus
```

**Expected output:** JSON with `"provisioningState": "Succeeded"`

### Step 1.2: Create Key Vault (secrets storage)

```bash
az keyvault create \
  --name gasoholic-kv \
  --resource-group gasoholic-rg \
  --location eastus \
  --enable-rbac-authorization
```

**Expected output:** JSON with vault details. Note the `id`.

### Step 1.3: Grant yourself Key Vault access

Replace `$USER_ID` with your Azure user ID (`az ad signed-in-user show --query id`):

```bash
USER_ID=$(az ad signed-in-user show --query id -o tsv)

az role assignment create \
  --role "Key Vault Secrets Officer" \
  --assignee $USER_ID \
  --scope "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/gasoholic-rg/providers/Microsoft.KeyVault/vaults/gasoholic-kv"
```

Wait 30 seconds for RBAC to propagate.

### Step 1.4: Run full infrastructure deploy

This creates ACR, Container Apps environment, and other resources:

```bash
./deploy.sh --infra-only
```

**What this does:**
- Registers necessary Azure providers
- Runs Bicep IaC template (`infra/main.bicep`)
- Creates:
  - Azure Container Registry (ACR): `gasoholicacr`
  - Container Apps Environment: `gasoholic-env`
  - Container App: `gasoholic`
  - Managed Identity for the Container App
  - Azure Communication Services (ACS) for email

**Expected output:**
```
✓ Resource group gasoholic-rg ready
✓ Registered providers
✓ Running Bicep deployment...
✓ ACR created: gasoholicacr.azurecr.io
✓ Container App environment created: gasoholic-env
✓ Container App created: gasoholic
✓ Secrets created in Key Vault
  - SmokeTestSecret
  - AcsConnection
  - AcsSenderDomain
```

**Check status:**

```bash
az containerapp show \
  --name gasoholic \
  --resource-group gasoholic-rg \
  --query "{name: name, status: properties.provisioningState, url: properties.configuration.ingress.fqdn}"
```

Expected: `provisioningState: "Succeeded"` and `url: "gasoholic.xxxxx.eastus.azurecontainerapps.io"`

### Step 1.5: Create Azure SQL Database

The app needs a persistent database. Skip this if using SQLite locally only.

```bash
az sql server create \
  --name gasoholic-sql \
  --resource-group gasoholic-rg \
  --location "West US 2" \
  --admin-user sqladmin \
  --admin-password "$(openssl rand -base64 16)"
```

Create the database:

```bash
az sql db create \
  --server gasoholic-sql \
  --resource-group gasoholic-rg \
  --name gasoholic \
  --edition Basic
```

Get the connection string and store in Key Vault:

```bash
CONNECTION_STRING=$(az sql db show-connection-string \
  --client=ado.net \
  --server gasoholic-sql \
  --name gasoholic \
  -o tsv)

# Replace password placeholder
CONNECTION_STRING="${CONNECTION_STRING/<password>/<password-you-set-above>}"

az keyvault secret set \
  --vault-name gasoholic-kv \
  --name SqlConnection \
  --value "$CONNECTION_STRING"
```

Configure firewall to allow Azure services:

```bash
az sql server firewall-rule create \
  --server gasoholic-sql \
  --resource-group gasoholic-rg \
  --name "Allow Azure Services" \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

---

## Phase 2: Configure Email (Optional but Recommended)

If you want magic link emails to work in production, set up Azure Communication Services with a custom domain.

### Step 2.1: Verify DNS access

You need to add DNS records to your domain. Verify you have access to your DNS provider for `gas.sdir.cc` (or your domain).

### Step 2.2: Deploy ACS (already done in Phase 1)

Skip — `deploy.sh --infra-only` already created the ACS resource.

### Step 2.3: Retrieve DNS records from Azure Portal

1. Go to [Azure Portal](https://portal.azure.com)
2. Search for **Communication Services** → `gasoholic-acs`
3. Click **Email** → **Domains** → **Custom domains**
4. Click **Set up custom domain for `gas.sdir.cc`**
5. Copy the three DNS records (SPF, DKIM, DKIM2):

| Record Type | Host | Value |
|---|---|---|
| TXT | `gas.sdir.cc` | *(copy from portal)* |
| CNAME | `selector1-...` | *(copy from portal)* |
| CNAME | `selector2-...` | *(copy from portal)* |

### Step 2.4: Add DNS records to your domain

Go to your DNS provider (e.g., Route53, Cloudflare, GoDaddy) and add:

1. **SPF record** (TXT): Exact value from portal
2. **DKIM records** (2x CNAME): Exact values from portal
3. **DMARC record** (TXT):
   ```
   Host: _dmarc.gas.sdir.cc
   Value: v=DMARC1; p=quarantine; rua=mailto:dmarc@sdir.cc
   ```

### Step 2.5: Verify records in Azure Portal

1. Click **Verify** for each record (SPF, DKIM, DKIM2)
2. Wait for all to show **Verified** (5–30 minutes, depends on DNS propagation)
3. When all verified, domain status changes to **Ready**

### Step 2.6: Link custom domain (Phase 2 infra deploy)

Once all DNS records are verified in the portal:

```bash
./deploy.sh --infra-only
```

This updates the Container App to use the custom domain.

### Step 2.7: Deploy updated app code

```bash
./deploy.sh --app-only
```

Emails now send from `verify@gas.sdir.cc` instead of the Azure-managed domain.

### Step 2.8: Verify email delivery

```bash
SECRET=$(az keyvault secret show --vault-name gasoholic-kv --name SmokeTestSecret --query value -o tsv)
./smoke-test.sh https://gas.sdir.cc $SECRET
```

Check the email steps pass. Then:

1. Log in at https://gas.sdir.cc with a real email (gmail.com, outlook.com, etc.)
2. Check that the magic link arrives in **inbox** (not spam)
3. Click the link and verify you can log in

---

## Phase 3: Build and Deploy the App

### Step 3.1: Build Docker image locally

```bash
docker buildx build --platform linux/amd64 \
  -t gasoholicacr.azurecr.io/gasoholic:latest \
  .
```

**Expected output:** Successfully built, image ready locally.

### Step 3.2: Log in to ACR and push

```bash
# Get ACR credentials
az acr login --name gasoholicacr

# Push image
docker push gasoholicacr.azurecr.io/gasoholic:latest
```

**Expected output:** Image successfully pushed to ACR.

### Step 3.3: Update Container App with new image

```bash
az containerapp update \
  --name gasoholic \
  --resource-group gasoholic-rg \
  --image gasoholicacr.azurecr.io/gasoholic:latest
```

**Expected output:** Container App revision created and started.

### Step 3.4: Get the live app URL

```bash
az containerapp show \
  --name gasoholic \
  --resource-group gasoholic-rg \
  --query "properties.configuration.ingress.fqdn" -o tsv
```

Example output: `gasoholic.yellowcliff-a9ca470c.eastus.azurecontainerapps.io`

---

## Phase 4: Verify Deployment

### Step 4.1: Health check

```bash
LIVE_URL=$(az containerapp show \
  --name gasoholic \
  --resource-group gasoholic-rg \
  --query "properties.configuration.ingress.fqdn" -o tsv)

curl -s https://$LIVE_URL/health | jq .
```

Expected output:
```json
{
  "status": "ok",
  "email": {
    "configured": true,
    "senderDomain": "gas.sdir.cc"
  }
}
```

### Step 4.2: Login test

```bash
curl -X POST https://$LIVE_URL/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com"}'
```

Expected: HTTP 202 `{"status": "pending"}` (email would be sent, but we can't receive in test)

Or use the smoke test:

```bash
SECRET=$(az keyvault secret show --vault-name gasoholic-kv --name SmokeTestSecret --query value -o tsv)
./smoke-test.sh https://$LIVE_URL $SECRET
```

Expected: All 14 steps PASS

### Step 4.3: Manual smoke test (browser)

1. Open https://<LIVE_URL> in a browser
2. Enter any email and click "Sign In"
3. Look for the "Check your inbox" message
4. (Optional) Use the dev-login endpoint to bypass email:
   ```bash
   curl -X POST https://<LIVE_URL>/auth/dev-login \
     -H "Content-Type: application/json" \
     -H "X-Smoke-Test-Secret: $SECRET" \
     -d '{"email":"test@example.com"}'
   ```
5. Copy the returned session cookie and make authenticated requests

---

## Phase 5: GitHub Actions CI/CD (Optional)

If you want to deploy on every push to `main`:

### Step 5.1: Create GitHub secret for Azure credentials

```bash
az ad sp create-for-rbac \
  --name "github-gasoholic-deploy" \
  --role "Contributor" \
  --scopes /subscriptions/$(az account show --query id -o tsv)
```

Copy the JSON output and create a GitHub secret:

1. Go to GitHub repo → **Settings** → **Secrets and variables** → **Actions**
2. Create new secret: `AZURE_CREDENTIALS`
3. Paste the JSON from above
4. Create another secret: `SMOKE_TEST_SECRET`
   ```bash
   az keyvault secret show --vault-name gasoholic-kv --name SmokeTestSecret --query value -o tsv
   ```
   Paste this value as the secret

### Step 5.2: Verify GitHub Actions workflow

The workflow file already exists at `.github/workflows/azure-deploy.yml`. Verify it:

```bash
cat .github/workflows/azure-deploy.yml
```

Expected: Workflow builds image, pushes to ACR, updates Container App, runs smoke test.

### Step 5.3: Test CI/CD

Make a trivial commit and push:

```bash
echo "# test" >> README.md
git add README.md
git commit -m "test: trigger CI/CD"
git push origin main
```

Go to GitHub → **Actions** and watch the workflow run. Expected: Build → Push → Deploy → Smoke test → All pass.

---

## Post-Deployment Maintenance

### Monitor the live app

**View logs:**
```bash
az containerapp logs show \
  --name gasoholic \
  --resource-group gasoholic-rg \
  --follow
```

**Check replica status:**
```bash
az containerapp replica list \
  --name gasoholic \
  --resource-group gasoholic-rg
```

**Query the database:**
```bash
# If using Azure SQL
az sql query --database gasoholic --name gasoholic-sql --query-string "SELECT COUNT(*) as user_count FROM Users"
```

### Backup the database

Automated backups are enabled by default (7-day retention on Basic tier). Manual backup:

```bash
az sql db backup \
  --server gasoholic-sql \
  --resource-group gasoholic-rg \
  --name gasoholic \
  --backup-name "manual-$(date +%Y%m%d-%H%M%S)"
```

### Clean up old container images

ACR accumulates images over time. Remove images older than 7 days but keep the 5 most recent:

```bash
az acr task create \
  --registry gasoholicacr \
  --name purge-old-images \
  --cmd "acr purge --filter 'gasoholic:.*' --ago 7d --keep 5" \
  --schedule "0 2 * * 0" \
  --context /dev/null
```

---

## Troubleshooting

### Container App won't start

**Check logs:**
```bash
az containerapp logs show --name gasoholic --resource-group gasoholic-rg
```

**Common causes:**
- Image not found in ACR → ensure image was pushed with correct tag
- Database connection string missing → check Key Vault secret `SqlConnection` exists
- Port wrong → Dockerfile exposes 8080, make sure it matches

### DNS records not verifying

**Cause:** DNS propagation takes time.

**Fix:**
- Wait 10–30 minutes and retry
- Verify DNS record is correct: `dig TXT gas.sdir.cc` (should show SPF value)

### Emails landing in spam

**Cause:** Custom domain not fully verified or DMARC missing.

**Fix:**
1. Verify DMARC record exists: `dig TXT _dmarc.gas.sdir.cc`
2. Allow 24–48 hours for sender reputation to build
3. Check spam folder, don't mark as spam (helps reputation)

### High costs

**Cause:** Container always running (minReplicas: 1) or large ACR image size.

**Fix:**
- Change minReplicas to 0 (cold starts ~5s) to save ~$15/mo
- Purge old images from ACR (see Maintenance section)
- Monitor via Azure Cost Management

---

## Rollback (If Deployment Fails)

If the live app is broken and you need to go back to the previous version:

```bash
# List previous revisions
az containerapp revision list \
  --name gasoholic \
  --resource-group gasoholic-rg \
  --query "[].{name: name, image: properties.template.containers[0].image}" -o table

# Point traffic to previous revision
az containerapp revision set-traffic \
  --name gasoholic \
  --resource-group gasoholic-rg \
  --traffic-weights <previous-revision-name>=100
```

---

## Disaster Recovery

If the entire resource group is deleted:

```bash
# Start over from Phase 1, Step 1.1
az group create --name gasoholic-rg --location eastus

# Then run full deploy
./deploy.sh
```

The database is separate from the resource group, so user data survives.

---

## Next Steps

- [ ] Set up GitHub Actions CI/CD (Phase 5)
- [ ] Configure custom email domain (Phase 2)
- [ ] Set up Azure Monitor / Application Insights for observability
- [ ] Enable Azure Defender for vulnerability scanning
- [ ] Document runbook for on-call engineers

---

## Quick Reference

| Resource | Location | Notes |
|----------|----------|-------|
| Resource group | `gasoholic-rg` | East US |
| Container App | `gasoholic` | `gasoholic-env` |
| ACR | `gasoholicacr` | East US |
| Key Vault | `gasoholic-kv` | East US |
| Azure SQL | `gasoholic-sql` | West US 2 |
| ACS | `gasoholic-acs` | Global |

**Handy commands:**

```bash
# Get live app URL
az containerapp show -n gasoholic -g gasoholic-rg --query properties.configuration.ingress.fqdn -o tsv

# Get all secrets
az keyvault secret list --vault-name gasoholic-kv --query [].name -o tsv

# Get secret value
az keyvault secret show --vault-name gasoholic-kv --name SmokeTestSecret --query value -o tsv

# Check Container App status
az containerapp show -n gasoholic -g gasoholic-rg --query properties.provisioningState -o tsv

# View logs (last 100 lines)
az containerapp logs show -n gasoholic -g gasoholic-rg --tail 100
```

---

## Support

- **Bicep errors:** Check `infra/main.bicep` syntax: `az bicep build --file infra/main.bicep`
- **Deployment scripts:** Review `deploy.sh` and `smoke-test.sh`
- **Azure docs:** https://learn.microsoft.com/en-us/azure/container-apps/
- **GitHub Actions:** https://github.com/anthropics/gasoholic/actions
