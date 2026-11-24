@description('Name of the Log Analytics workspace.')
param workspaceName string = 'log-vitalq-prod'

@description('Azure region for the deployment.')
param location string = resourceGroup().location

@description('Resource tags.')
param tags object = {}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: workspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

output id string = logAnalyticsWorkspace.id
output customerId string = logAnalyticsWorkspace.properties.customerId
#disable-next-line outputs-should-not-contain-secrets
output primarySharedKey string = logAnalyticsWorkspace.listKeys().primarySharedKey
