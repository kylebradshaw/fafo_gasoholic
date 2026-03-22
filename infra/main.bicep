@description('Base name for all resources. Must be globally unique (used for ACR, Key Vault).')
param appName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

// Derived names
var acrName = '${replace(appName, '-', '')}acr'   // ACR: alphanumeric only
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

// Azure-managed domain — no DNS config required; Azure provides the sender address.
// Sender: DoNotReply@<generated-domain>.azurecomm.net
resource acsDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: acsEmail
  name: 'AzureManagedDomain'
  location: 'global'
  properties: {
    domainManagement: 'AzureManaged'
  }
}

// ACS Communication Service (links to email service for sending)
resource acsService 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: '${appName}-comms'
  location: 'global'
  properties: {
    dataLocation: 'UnitedStates'
    linkedDomains: [ acsDomain.id ]
  }
}

// ── Key Vault (stores ACS connection string) ──────────────────────────────────

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

// ACS sender domain name — stored for the app to build the from-address
resource acsDomainSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AcsSenderDomain'
  properties: {
    value: acsDomain.properties.mailFromSenderDomain
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
      ]
    }
    template: {
      containers: [
        {
          name: appName
          image: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
          env: [
            { name: 'DATABASE_PROVIDER',                       value: 'sqlite' }
            { name: 'ConnectionStrings__DefaultConnection',    value: 'Data Source=/tmp/gasoholic.db' }
            { name: 'ConnectionStrings__ACS',                  secretRef: 'acs-connection' }
            { name: 'AcsSenderDomain',                         secretRef: 'acs-sender-domain' }
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
        minReplicas: 0
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
output keyVaultUri string = keyVault.properties.vaultUri
