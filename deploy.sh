#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# deploy.sh — Provision Azure infrastructure and deploy gasoholic
#
# Usage:
#   ./deploy.sh              # full provision + deploy
#   ./deploy.sh --infra-only # provision Azure resources, skip app deploy
#   ./deploy.sh --app-only   # deploy app to existing infrastructure
#
# Architecture:
#   Azure Container Apps (consumption, East US) — scales to zero
#   Azure Container Registry (Basic, ~$5/mo)
#   Azure Files (Standard LRS, ~$0.02/GiB/mo) — SQLite database persisted here
#
# Prerequisites:
#   brew install azure-cli
#   az login
#   dotnet SDK 10.0.103+
#   docker with buildx
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

# ── Config ────────────────────────────────────────────────────────────────────
APP_NAME="gasoholic"
RESOURCE_GROUP="gasoholic-rg"
LOCATION="eastus"          # East US — all resources deployed here

# ── Flags ─────────────────────────────────────────────────────────────────────
INFRA_ONLY=false
APP_ONLY=false
for arg in "$@"; do
  case $arg in
    --infra-only) INFRA_ONLY=true ;;
    --app-only)   APP_ONLY=true ;;
  esac
done

# ── Helpers ───────────────────────────────────────────────────────────────────
log()  { echo "▶ $*"; }
ok()   { echo "✓ $*"; }
fail() { echo "✗ $*" >&2; exit 1; }

require() {
  command -v "$1" &>/dev/null || fail "Required tool not found: $1. Install it and try again."
}

# ── Preflight ─────────────────────────────────────────────────────────────────
require az
require dotnet
require docker

log "Checking Azure login..."
az account show &>/dev/null || fail "Not logged in to Azure. Run: az login"
ok "Subscription: $(az account show --query name -o tsv)"

SECRETS_FILE="$(dirname "$0")/.deploy-secrets"
if [[ -f "$SECRETS_FILE" ]]; then
  # shellcheck source=/dev/null
  source "$SECRETS_FILE"
  log "Loaded secrets from .deploy-secrets"
fi

# ── Providers ─────────────────────────────────────────────────────────────────
if [[ "$APP_ONLY" == false ]]; then
  log "Registering resource providers..."
  for ns in Microsoft.App Microsoft.ContainerRegistry Microsoft.Storage; do
    state=$(az provider show --namespace "$ns" --query registrationState -o tsv 2>/dev/null || echo "NotRegistered")
    if [[ "$state" != "Registered" ]]; then
      az provider register --namespace "$ns" --wait &>/dev/null
    fi
  done
  ok "All providers registered"

  # ── Resource group ──────────────────────────────────────────────────────────
  log "Ensuring resource group $RESOURCE_GROUP in $LOCATION..."
  az group create --name "$RESOURCE_GROUP" --location "$LOCATION" &>/dev/null
  ok "Resource group ready"

  # ── Bicep deployment ────────────────────────────────────────────────────────
  log "Deploying infrastructure (this takes ~2 min)..."
  DEPLOY_OUT=$(az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --template-file "$(dirname "$0")/infra/main.bicep" \
    --parameters appName="$APP_NAME" \
    --output json)

  APP_URL=$(echo "$DEPLOY_OUT"    | python3 -c "import sys,json; print(json.load(sys.stdin)['properties']['outputs']['appUrl']['value'])")
  ACR_SERVER=$(echo "$DEPLOY_OUT" | python3 -c "import sys,json; print(json.load(sys.stdin)['properties']['outputs']['acrLoginServer']['value'])")
  ACR_NAME=$(echo "$DEPLOY_OUT"   | python3 -c "import sys,json; print(json.load(sys.stdin)['properties']['outputs']['acrName']['value'])")

  # Save outputs for --app-only runs
  {
    echo "APP_URL='${APP_URL}'"
    echo "ACR_SERVER='${ACR_SERVER}'"
    echo "ACR_NAME='${ACR_NAME}'"
  } > "$SECRETS_FILE"
  chmod 600 "$SECRETS_FILE"

  ok "Infrastructure deployed — $APP_URL"

  # ── Service principal for GitHub Actions ────────────────────────────────────
  log "Creating GitHub Actions service principal..."
  SUBSCRIPTION_ID=$(az account show --query id -o tsv)
  AZURE_CREDENTIALS=$(az ad sp create-for-rbac \
    --name "${APP_NAME}-deployer" \
    --role contributor \
    --scopes "/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}" \
    --json-auth \
    2>/dev/null)
  echo "AZURE_CREDENTIALS='${AZURE_CREDENTIALS}'" >> "$SECRETS_FILE"
  ok "Service principal created"
fi

# ── Build & push image ────────────────────────────────────────────────────────
if [[ "$INFRA_ONLY" == false ]]; then
  source "$SECRETS_FILE"
  # Derive ACR_NAME from ACR_SERVER if not stored in secrets file (older deploys)
  ACR_NAME="${ACR_NAME:-${ACR_SERVER%%.*}}"

  log "Logging in to ACR ($ACR_SERVER)..."
  az acr login --name "$ACR_NAME"

  log "Building and pushing Docker image (linux/amd64 for Azure)..."
  IMAGE_TAG="${ACR_SERVER}/${APP_NAME}:$(git rev-parse --short HEAD)"
  docker buildx build --platform linux/amd64 \
    -t "$IMAGE_TAG" \
    -t "${ACR_SERVER}/${APP_NAME}:latest" \
    --push \
    "$(dirname "$0")"

  log "Updating Container App to new image..."
  az containerapp update \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --image "$IMAGE_TAG" \
    --output none

  ok "App deployed → $APP_URL"

  # ── Run EF migrations against Azure SQL ─────────────────────────────────────
  log "Running database migrations..."
  SQL_CONN=$(az keyvault secret show \
    --vault-name "${APP_NAME}-kv" \
    --name SqlConnection \
    --query value -o tsv 2>/dev/null || echo "")

  if [[ -z "$SQL_CONN" ]]; then
    echo "  ⚠  SqlConnection secret not found in Key Vault — skipping migrations"
  else
    DATABASE_PROVIDER=sqlserver dotnet ef database update \
      --project "$(dirname "$0")" \
      --connection "$SQL_CONN"

    # Create SessionCache table if it doesn't exist yet
    dotnet-sql-cache create "$SQL_CONN" dbo SessionCache 2>/dev/null && \
      ok "SessionCache table ready" || \
      ok "SessionCache table already exists"

    ok "Migrations applied"
  fi
fi

# ── Summary ───────────────────────────────────────────────────────────────────
source "$SECRETS_FILE"
echo ""
echo "═══════════════════════════════════════════════════════"
echo "  Deployment complete"
echo "  App URL:    ${APP_URL:-run ./deploy.sh --infra-only first}"
echo "═══════════════════════════════════════════════════════"
if [[ "${AZURE_CREDENTIALS:-}" != "" ]] && [[ "$APP_ONLY" == false ]]; then
  echo ""
  echo "  GitHub Actions secret to add:"
  echo "  Name:  AZURE_CREDENTIALS"
  echo "  Value: (see .deploy-secrets — do not share or commit)"
  echo ""
  echo "  Repo → Settings → Secrets → Actions → New repository secret"
  echo "═══════════════════════════════════════════════════════"
fi
