@description('Base name for all resources. Must be globally unique (used for ACR, storage account).')
param appName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

// Derived names — all resources in the same East US location
var acrName = '${replace(appName, '-', '')}acr'       // ACR: alphanumeric only
var storageName = '${replace(appName, '-', '')}data'   // Storage: alphanumeric only, ≤24 chars
var envName = '${appName}-env'
var shareQuotaGiB = 1

// ── Azure Container Registry (Basic — ~$5/mo) ────────────────────────────────

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: true   // used by Container App to pull images
  }
}

// ── Storage Account + Azure Files share (Standard LRS — ~$0.02/GiB/mo) ──────
// Replaces Azure SQL: SQLite database file lives on the mounted file share.

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource fileService 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource fileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: fileService
  name: 'data'
  properties: {
    shareQuota: shareQuotaGiB
    enabledProtocols: 'SMB'
  }
}

// ── Container Apps Environment (free — consumption plan) ─────────────────────

resource containerEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: envName
  location: location
  properties: {}
}

// Link the Azure Files share to the managed environment so Container Apps can mount it.
resource envStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: containerEnv
  name: 'gasoholic-data'
  properties: {
    azureFile: {
      accountName: storageAccount.name
      accountKey: storageAccount.listKeys().keys[0].value
      shareName: fileShare.name
      accessMode: 'ReadWrite'
    }
  }
}

// ── Container App ────────────────────────────────────────────────────────────

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
      volumes: [
        {
          name: 'data'
          storageType: 'AzureFile'
          storageName: envStorage.name
        }
      ]
      containers: [
        {
          name: appName
          // Placeholder image — CI replaces this on first push
          image: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
          env: [
            { name: 'DATABASE_PROVIDER',                       value: 'sqlite' }
            { name: 'ConnectionStrings__DefaultConnection',    value: 'Data Source=/data/gasoholic.db' }
            { name: 'CORS_ORIGINS',                            value: 'https://${appName}.${containerEnv.properties.defaultDomain}' }
            { name: 'ASPNETCORE_ENVIRONMENT',                  value: 'Production' }
            { name: 'ASPNETCORE_URLS',                         value: 'http://+:8080' }
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          volumeMounts: [
            {
              volumeName: 'data'
              mountPath: '/data'
            }
          ]
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
output storageAccountName string = storageAccount.name
