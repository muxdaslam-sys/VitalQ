@description('Name of the Key Vault.')
param keyVaultName string = 'kv-vitalq-prod'

@description('Azure region for the deployment.')
param location string = resourceGroup().location

@description('Principal ID of the ca-vitalq-api Managed Identity for secret access.')
param apiPrincipalId string = ''

@description('Optional initial JWT Secret Key to store in Key Vault.')
@secure()
param jwtKeySecretValue string = ''

@description('Resource ID of the Log Analytics workspace for audit diagnostic settings.')
param logAnalyticsWorkspaceId string = ''

@description('Resource tags.')
param tags object = {}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enableRbacAuthorization: false
    accessPolicies: !empty(apiPrincipalId) ? [
      {
        tenantId: subscription().tenantId
        objectId: apiPrincipalId
        permissions: {
          secrets: [
            'get'
            'list'
          ]
        }
      }
    ] : []
  }
}

resource jwtSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(jwtKeySecretValue)) {
  parent: keyVault
  name: 'JwtKey'
  properties: {
    value: jwtKeySecretValue
  }
}

// HIPAA & SOC2 Compliant Audit Logging: Streams Key Vault access logs to Log Analytics
resource keyVaultDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (!empty(logAnalyticsWorkspaceId)) {
  name: 'diag-${keyVaultName}-logs'
  scope: keyVault
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AuditEvent'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

output id string = keyVault.id
output name string = keyVault.name
output vaultUri string = keyVault.properties.vaultUri
