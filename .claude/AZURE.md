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
  - Provisions: ACR (Basic), Container Apps Environment (consumption), Container App, Azure Files storage, Key Vault
  - `AZURE_CREDENTIALS` service principal created automatically, stored in `.deploy-secrets`
  - Database: SQLite on Azure Files SMB mount at `/data/gasoholic.db`
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
- **Database:** SQLite at `/data/gasoholic.db` on Azure Files SMB mount — persistent across Container App revisions and restarts
- **Session cache:** in-memory (`AddDistributedMemoryCache`), cleared on restart
- **Scale:** 1 replica (min/max); SQLite does not support concurrent multi-instance writes
- **Secrets:** Key Vault stores ACS connection string, ACS sender domain, and smoke test secret
- **Cost estimate:** ~$5/mo (ACR Basic) + negligible Container Apps consumption + ~$1/mo Azure Files
