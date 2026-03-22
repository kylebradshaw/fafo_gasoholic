@description('Base name for all resources. Must be globally unique (used for ACR).')
param appName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

// Derived names
var acrName = '${replace(appName, '-', '')}acr'   // ACR: alphanumeric only
var envName = '${appName}-env'

// ── Azure Container Registry (Basic — ~$5/mo) ────────────────────────────────

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: true   // used by Container App to pull images
  }
}

// ── Container Apps Environment (free — consumption plan) ─────────────────────

resource containerEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: envName
  location: location
  properties: {}
}

// ── Container App ────────────────────────────────────────────────────────────
// SQLite database stored at /tmp/gasoholic.db (ephemeral per-revision).
// Azure Files SMB (port 445) is blocked on Container Apps consumption plan.
// For production persistence, migrate to Azure SQL or upgrade to a dedicated
// Container Apps environment with VNet integration.

var acrPassword = acr.listCredentials().passwords[0].value

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  properties: {
    managedEnvironmentId: containerEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
      }
      registries: [
        {
          server: acr.properties.loginServer
          username: acr.listCredentials().username
          passwordSecretRef: 'acr-password'
        }
      ]
      secrets: [
        {
          name: 'acr-password'
          value: acrPassword
        }
      ]
    }
    template: {
      containers: [
        {
          name: appName
          // Placeholder image — CI replaces this on first push
          image: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
          env: [
            { name: 'DATABASE_PROVIDER',                       value: 'sqlite' }
            { name: 'ConnectionStrings__DefaultConnection',    value: 'Data Source=/tmp/gasoholic.db' }
            { name: 'CORS_ORIGINS',                            value: 'https://${appName}.${containerEnv.properties.defaultDomain}' }
            { name: 'ASPNETCORE_ENVIRONMENT',                  value: 'Production' }
            { name: 'ASPNETCORE_URLS',                         value: 'http://+:8080' }
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
      scale: {
        minReplicas: 0   // scales to zero when idle — free when not in use
        maxReplicas: 1
      }
    }
  }
}

// ── Outputs ──────────────────────────────────────────────────────────────────

output appUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output acrLoginServer string = acr.properties.loginServer
