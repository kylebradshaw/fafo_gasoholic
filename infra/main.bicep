@description('Base name for all resources. Must be globally unique (used for ACR, Key Vault).')
param appName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

// Derived names
var acrBase = toLower(replace(appName, '-', ''))
var acrName = '${acrBase}acr'   // ACR: alphanumeric only (e.g. gasoholicacr)
var kvName = '${appName}-kv'
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

// ── Azure Communication Services — Email (free tier: 2,000 emails/mo) ────────

resource acsEmail 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: '${appName}-acs'
  location: 'global'           // ACS email services are global resources
  properties: {
    dataLocation: 'UnitedStates'
  }
}

// Azure-managed domain — kept as fallback and used until custom domain is verified.
resource acsManagedDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: acsEmail
  name: 'AzureManagedDomain'
  location: 'global'
  properties: {
    domainManagement: 'AzureManaged'
  }
}

// Custom domain — requires DNS records (SPF, DKIM, DKIM2) added manually after deploy.
// See DEPLOYMENT.md "Email Domain Setup (Task 18)" for DNS setup steps.
// Sender: verify@gas.sdir.cc
resource acsCustomDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: acsEmail
  name: 'gas.sdir.cc'
  location: 'global'
  properties: {
    domainManagement: 'CustomerManaged'
  }
}

@description('Set to true after custom domain DNS is verified in Azure Portal. This links the custom domain to ACS for sending.')
param useCustomEmailDomain bool = false

// ACS Communication Service (links to email service for sending)
// Phase 1 (default): links Azure-managed domain so email keeps working
// Phase 2 (after DNS verification): set useCustomEmailDomain=true to switch to custom domain
resource acsService 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: '${appName}-comms'
  location: 'global'
  properties: {
    dataLocation: 'UnitedStates'
    linkedDomains: useCustomEmailDomain ? [ acsCustomDomain.id ] : [ acsManagedDomain.id ]
  }
}

// ── Cosmos DB (serverless) ───────────────────────────────────────────────────

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: '${appName}-cosmos'
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: { defaultConsistencyLevel: 'Session' }
    locations: [{ locationName: location, failoverPriority: 0 }]
    capabilities: [{ name: 'EnableServerless' }]
  }
}

resource cosmosDb 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosAccount
  name: 'gasoholic'
  properties: {
    resource: { id: 'gasoholic' }
  }
}

resource containerUsers 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDb
  name: 'Users'
  properties: {
    resource: {
      id: 'Users'
      partitionKey: { paths: [ '/id' ], kind: 'Hash' }
    }
  }
}

resource containerAutos 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDb
  name: 'Autos'
  properties: {
    resource: {
      id: 'Autos'
      partitionKey: { paths: [ '/userId' ], kind: 'Hash' }
    }
  }
}

resource containerFillups 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDb
  name: 'Fillups'
  properties: {
    resource: {
      id: 'Fillups'
      partitionKey: { paths: [ '/autoId' ], kind: 'Hash' }
    }
  }
}

resource containerMaintenance 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDb
  name: 'Maintenance'
  properties: {
    resource: {
      id: 'Maintenance'
      partitionKey: { paths: [ '/autoId' ], kind: 'Hash' }
    }
  }
}

resource containerVerificationTokens 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: cosmosDb
  name: 'VerificationTokens'
  properties: {
    resource: {
      id: 'VerificationTokens'
      partitionKey: { paths: [ '/userId' ], kind: 'Hash' }
      defaultTtl: 604800
    }
  }
}

// ── Key Vault (stores ACS connection string + Cosmos connection) ─────────────

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: kvName
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

// ACS connection string — Container App reads it via managed identity
resource acsSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AcsConnection'
  properties: {
    value: acsService.listKeys().primaryConnectionString
  }
}

// Cosmos DB connection string — Container App reads it via managed identity
resource cosmosSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'CosmosConnection'
  properties: {
    value: cosmosAccount.listConnectionStrings().connectionStrings[0].connectionString
  }
}

// ACS sender domain name — stored for the app to build the from-address
resource acsDomainSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AcsSenderDomain'
  properties: {
    value: useCustomEmailDomain ? acsCustomDomain.properties.mailFromSenderDomain : acsManagedDomain.properties.mailFromSenderDomain
  }
}

// ── Container Apps Environment (free — consumption plan) ─────────────────────

resource containerEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: envName
  location: location
  properties: {}
}

// ── Container App ────────────────────────────────────────────────────────────

var acrPassword = acr.listCredentials().passwords[0].value

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  identity: { type: 'SystemAssigned' }
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
        { name: 'acr-password', value: acrPassword }
        {
          name: 'acs-connection'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/AcsConnection'
          identity: 'system'
        }
        {
          name: 'acs-sender-domain'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/AcsSenderDomain'
          identity: 'system'
        }
        {
          name: 'cosmos-connection'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/CosmosConnection'
          identity: 'system'
        }
        {
          name: 'smoke-test-secret'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/SmokeTestSecret'
          identity: 'system'
        }
      ]
    }
    template: {
      containers: [
        {
          name: appName
          image: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
          env: [
            { name: 'ConnectionStrings__Cosmos',                secretRef: 'cosmos-connection' }
            { name: 'ConnectionStrings__ACS',                  secretRef: 'acs-connection' }
            { name: 'AcsSenderDomain',                         secretRef: 'acs-sender-domain' }
            { name: 'CORS_ORIGINS',                            value: 'https://${appName}.${containerEnv.properties.defaultDomain},https://gas.sdir.cc' }
            { name: 'SMOKE_TEST_SECRET',                       secretRef: 'smoke-test-secret' }
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
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

// Grant the Container App's managed identity "Key Vault Secrets User" role
resource kvSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, containerApp.id, '4633458b-17de-408a-b874-0445c86b69e6')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: containerApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ── Outputs ──────────────────────────────────────────────────────────────────

output appUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output acrLoginServer string = acr.properties.loginServer
output acrName string = acr.name
output keyVaultUri string = keyVault.properties.vaultUri
