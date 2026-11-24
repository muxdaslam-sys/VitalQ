@description('Name of the Container Apps Environment.')
param environmentName string = 'cae-vitalq-prod'

@description('Azure region for the deployment.')
param location string = resourceGroup().location

@description('Log Analytics Workspace Customer ID.')
param logAnalyticsCustomerId string

@description('Log Analytics Workspace Shared Key.')
@secure()
param logAnalyticsSharedKey string

@description('Backend container image reference.')
param apiImage string = 'crvitalqprod.azurecr.io/vitalq-api:latest'

@description('Frontend container image reference.')
param webImage string = 'crvitalqprod.azurecr.io/vitalq-web:latest'

@description('Passwordless Azure SQL connection string.')
@secure()
param sqlConnectionString string

@description('URI of the Azure Key Vault for secret references.')
param keyVaultUri string

@description('Application Insights connection string.')
param appInsightsConnectionString string = ''

@description('Minimum replicas for API service (0 for serverless cost savings, 1 for zero cold-start).')
param apiMinReplicas int = 0

@description('Minimum replicas for Web client.')
param webMinReplicas int = 0

@description('Resource tags.')
param tags object = {}

// 1. Managed Container Apps Environment
resource managedEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: environmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsCustomerId
        sharedKey: logAnalyticsSharedKey
      }
    }
  }
}

// 2. Redis Distributed Cache Service (Internal Cluster Ingress)
resource redisApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: 'ca-vitalq-redis'
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: managedEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 6379
        transport: 'tcp'
      }
    }
    template: {
      containers: [
        {
          name: 'redis'
          image: 'redis:alpine'
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

// 3. Backend API Service (.NET 8 Chiseled with System-Assigned Managed Identity)
resource apiApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: 'ca-vitalq-api'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: managedEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      secrets: [
        {
          name: 'sql-connection-string'
          value: sqlConnectionString
        }
        {
          name: 'jwt-key'
          keyVaultUrl: '${keyVaultUri}secrets/JwtKey'
          identity: 'system'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'vitalq-api'
          image: apiImage
          resources: {
            cpu: json('0.5')
            memory: '1.0Gi'
          }
          env: [
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'sql-connection-string'
            }
            {
              name: 'ConnectionStrings__RedisConnection'
              value: 'ca-vitalq-redis:6379'
            }
            {
              name: 'Jwt__Key'
              secretRef: 'jwt-key'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: appInsightsConnectionString
            }
          ]
        }
      ]
      scale: {
        minReplicas: apiMinReplicas
        maxReplicas: 5
        rules: [
          {
            name: 'http-rule'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
  dependsOn: [
    redisApp
  ]
}

// 4. Frontend Web Client (Angular 19 + Nginx Alpine)
resource webApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: 'ca-vitalq-web'
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: managedEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 80
        transport: 'auto'
        allowInsecure: false
      }
    }
    template: {
      containers: [
        {
          name: 'vitalq-web'
          image: webImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'BACKEND_URL'
              value: 'https://${apiApp.properties.configuration.ingress.fqdn}'
            }
          ]
        }
      ]
      scale: {
        minReplicas: webMinReplicas
        maxReplicas: 5
        rules: [
          {
            name: 'http-rule'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
}

output apiFqdn string = apiApp.properties.configuration.ingress.fqdn
output webFqdn string = webApp.properties.configuration.ingress.fqdn
output apiPrincipalId string = apiApp.identity.principalId
output apiAppId string = apiApp.id
output environmentId string = managedEnvironment.id
