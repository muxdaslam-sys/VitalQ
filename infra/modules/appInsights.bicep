@description('Name of the Application Insights resource.')
param appInsightsName string = 'appi-vitalq-prod'

@description('Azure region for the deployment.')
param location string = resourceGroup().location

@description('Resource ID of the Log Analytics workspace.')
param workspaceId string

@description('Resource tags.')
param tags object = {}

@description('Target public URL for 24/7 availability ping tests.')
param pingTestUrl string = 'https://ca-vitalq-web.politeisland-88851c55.centralindia.azurecontainerapps.io/'

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

// 24/7 Automated Availability Ping Test (Evaluates health from Singapore, Dublin, and Virginia)
resource availabilityTest 'Microsoft.Insights/webtests@2022-06-15' = {
  name: 'webtest-${appInsightsName}-ping'
  location: location
  tags: union(tags, {
    'hidden-link:${appInsights.id}': 'Resource'
  })
  properties: {
    SyntheticMonitorId: 'vitalq-ping-monitor'
    Name: 'VitalQ Production Availability Ping'
    Description: 'Continuous 24/7 multi-region ping test evaluating web availability every 5 minutes.'
    Enabled: true
    Frequency: 300
    Timeout: 30
    Kind: 'standard'
    RetryEnabled: true
    Locations: [
      {
        Id: 'apac-sg-sin-azr'
      }
      {
        Id: 'emea-gb-db3-azr'
      }
      {
        Id: 'us-va-ash-azr'
      }
    ]
    Configuration: null
    Request: {
      RequestUrl: pingTestUrl
      HttpVerb: 'GET'
      ParseDependentRequests: false
    }
    ValidationRules: {
      ExpectedHttpStatusCode: 200
      IgnoreHttpStatusCode: false
      SSLCheck: true
      SSLCertRemainingLifetimeCheck: 7
    }
  }
}

output id string = appInsights.id
output connectionString string = appInsights.properties.ConnectionString
output instrumentationKey string = appInsights.properties.InstrumentationKey
