targetScope = 'resourceGroup'

@description('Deployment environment name (e.g. prod, staging, dev).')
@allowed([
  'prod'
  'staging'
  'dev'
])
param environment string = 'prod'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('SQL Server administrator username.')
param sqlAdministratorLogin string = 'sqladmin'

@description('SQL Server administrator password.')
@secure()
param sqlAdministratorLoginPassword string

@description('Microsoft Entra ID administrator display name.')
param entraAdminLogin string = 'Muhammed Aslam'

@description('Microsoft Entra ID administrator Object ID.')
param entraAdminObjectId string = '78710e79-cdd8-44d8-ba24-adb943c9cfe8'

@description('JWT Secret Key to seed into Key Vault.')
@secure()
param jwtKeySecretValue string = ''

@description('Backend container image reference.')
param apiImage string = 'crvitalqprod.azurecr.io/vitalq-api:latest'

@description('Frontend container image reference.')
param webImage string = 'crvitalqprod.azurecr.io/vitalq-web:latest'

@description('Minimum replicas for API container app (0 for cost savings, 1 for warm baseline).')
param apiMinReplicas int = 0

@description('Minimum replicas for Web container app.')
param webMinReplicas int = 0

@description('Email address to receive production DevOps health alerts.')
param alertEmailAddress string = ''

var namingPrefix = 'vitalq-${environment}'
var tags = {
  Project: 'VitalQ'
  Environment: environment
  ManagedBy: 'Bicep-IaC'
}

// 1. Log Analytics Workspace (APM & Container Telemetry Sink)
module logAnalytics 'modules/logAnalytics.bicep' = {
  name: 'deploy-log-analytics'
  params: {
    workspaceName: 'log-${namingPrefix}'
    location: location
    tags: tags
  }
}

// 2. Application Insights (Distributed Tracing & Metrics)
module appInsights 'modules/appInsights.bicep' = {
  name: 'deploy-app-insights'
  params: {
    appInsightsName: 'appi-${namingPrefix}'
    location: location
    workspaceId: logAnalytics.outputs.id
    tags: tags
  }
}

// 3. Azure Key Vault (Zero-Secret Hardware Security Store)
module keyVault 'modules/keyVault.bicep' = {
  name: 'deploy-key-vault'
  params: {
    keyVaultName: 'kv-${namingPrefix}'
    location: location
    tags: tags
    jwtKeySecretValue: jwtKeySecretValue
  }
}

// 4. Azure SQL Server & Serverless Database
module sqlServer 'modules/sqlServer.bicep' = {
  name: 'deploy-sql-server'
  params: {
    serverName: 'sql-${namingPrefix}'
    databaseName: 'sqldb-${namingPrefix}'
    location: location
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorLoginPassword
    entraAdminLogin: entraAdminLogin
    entraAdminObjectId: entraAdminObjectId
    tags: tags
  }
}

// 5. Container Apps Environment & Microservices (Redis, API, Web)
var passwordlessSqlConnectionString = 'Server=tcp:${sqlServer.outputs.serverFqdn},1433;Database=${sqlServer.outputs.databaseName};Authentication=Active Directory Default;TrustServerCertificate=True;'

module containerApps 'modules/containerApps.bicep' = {
  name: 'deploy-container-apps'
  params: {
    environmentName: 'cae-${namingPrefix}'
    location: location
    logAnalyticsCustomerId: logAnalytics.outputs.customerId
    logAnalyticsSharedKey: logAnalytics.outputs.primarySharedKey
    apiImage: apiImage
    webImage: webImage
    sqlConnectionString: passwordlessSqlConnectionString
    keyVaultUri: keyVault.outputs.vaultUri
    appInsightsConnectionString: appInsights.outputs.connectionString
    apiMinReplicas: apiMinReplicas
    webMinReplicas: webMinReplicas
    tags: tags
  }
}

// 6. Azure Monitor Production Alerts & Action Groups
module alerts 'modules/alerts.bicep' = {
  name: 'deploy-alerts'
  params: {
    actionGroupName: 'ag-${namingPrefix}-devops'
    actionGroupShortName: 'VitalQAlert'
    alertEmailAddress: alertEmailAddress
    appInsightsId: appInsights.outputs.id
    apiAppId: containerApps.outputs.apiAppId
    sqlDatabaseId: sqlServer.outputs.databaseId
    tags: tags
  }
}

output liveWebUrl string = 'https://${containerApps.outputs.webFqdn}'
output liveApiUrl string = 'https://${containerApps.outputs.apiFqdn}'
output liveHealthCheckUrl string = 'https://${containerApps.outputs.apiFqdn}/health/live'
output sqlServerFqdn string = sqlServer.outputs.serverFqdn
output keyVaultUri string = keyVault.outputs.vaultUri
output apiPrincipalId string = containerApps.outputs.apiPrincipalId
output actionGroupId string = alerts.outputs.actionGroupId
