@description('Base name for all resources (e.g. "gasoholic"). Must be globally unique for SQL server and Key Vault.')
param appName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('SQL Server administrator login username.')
param sqlAdminLogin string = 'gasoadmin'

@description('SQL Server administrator password. Stored in Key Vault — never logged.')
@secure()
param sqlAdminPassword string

// ── App Service Plan (Linux B1 — ~$13/mo) ───────────────────────────────────

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${appName}-plan'
  location: location
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true
  }
}

// ── Web App ──────────────────────────────────────────────────────────────────

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      appSettings: [
        {
          name: 'DATABASE_PROVIDER'
          value: 'sqlserver'
        }
        {
          // Key Vault reference — App Service resolves this at runtime.
          // The app reads it as a plain connection string; the vault secret never leaves Azure.
          name: 'ConnectionStrings__SqlServer'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/SqlConnection/)'
        }
        {
          name: 'CORS_ORIGINS'
          value: 'https://${appName}.azurewebsites.net'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
      ]
    }
  }
}

// ── Azure SQL Server + Database (Basic 5 DTU — ~$5/mo) ──────────────────────

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: '${appName}-sql'
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: appName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
}

// Allow the App Service outbound IPs to reach SQL — add specific IPs after first deploy.
// This rule is intentionally permissive for initial provisioning; tighten it after.
resource sqlFirewallAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// ── Key Vault ────────────────────────────────────────────────────────────────

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${appName}-kv'
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

// Store the SQL connection string as a Key Vault secret
resource sqlConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'SqlConnection'
  properties: {
    value: 'Server=${sqlServer.properties.fullyQualifiedDomainName};Database=${appName};User Id=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=true;TrustServerCertificate=false;'
  }
}

// Grant the Web App's managed identity "Key Vault Secrets User" role (read secrets)
resource kvSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webApp.id, '4633458b-17de-408a-b874-0445c86b69e6')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ── Outputs ──────────────────────────────────────────────────────────────────

output appUrl string = 'https://${webApp.properties.defaultHostName}'
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output keyVaultUri string = keyVault.properties.vaultUri
