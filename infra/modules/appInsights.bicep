@description('Name of the Application Insights resource.')
param appInsightsName string = 'appi-vitalq-prod'

@description('Azure region for the deployment.')
param location string = resourceGroup().location

@description('Resource ID of the Log Analytics workspace.')
param workspaceId string

@description('Resource tags.')
param tags object = {}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspaceId
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    IngestionMode: 'LogAnalytics'
  }
}

output id string = appInsights.id
output connectionString string = appInsights.properties.ConnectionString
output instrumentationKey string = appInsights.properties.InstrumentationKey
