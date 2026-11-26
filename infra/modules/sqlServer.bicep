@description('Name of the logical Azure SQL Server.')
param serverName string = 'sql-vitalq-prod'

@description('Azure region for the deployment.')
param location string = resourceGroup().location

@description('Name of the serverless database.')
param databaseName string = 'sqldb-vitalq-prod'

@description('SQL administrator username.')
param administratorLogin string = 'vitalqadmin'

@description('SQL administrator password.')
@secure()
param administratorLoginPassword string

@description('Microsoft Entra ID administrator display name.')
param entraAdminLogin string = ''

@description('Microsoft Entra ID administrator Object ID.')
param entraAdminObjectId string = ''

@description('Auto-pause delay in minutes for serverless database.')
param autoPauseDelayMinutes int = 60

@description('Resource tags.')
param tags object = {}

resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    version: '12.0'
    publicNetworkAccess: 'Enabled'
    administrators: !empty(entraAdminObjectId) ? {
      administratorType: 'ActiveDirectory'
      principalType: 'Group'
      login: entraAdminLogin
      sid: entraAdminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: false
    } : null
  }
}

resource allowAzureIps 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 34359738368 // 32 GB
    minCapacity: json('0.5')
    autoPauseDelay: autoPauseDelayMinutes
  }
}

output serverId string = sqlServer.id
output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseId string = sqlDatabase.id
output databaseName string = sqlDatabase.name
