@description('Location for Action Group and Alert Rules (Action Groups are global).')
param location string = 'global'

@description('Action Group Name.')
param actionGroupName string = 'ag-vitalq-devops'

@description('Short name for Action Group (max 12 characters).')
param actionGroupShortName string = 'VitalQAlert'

@description('Email address to receive production alerts.')
param alertEmailAddress string = ''

@description('Resource ID of Application Insights for failed request alerts.')
param appInsightsId string

@description('Resource ID of the API Container App for CPU and memory alerts.')
param apiAppId string

@description('Resource ID of the Azure SQL Database for compute alerts.')
param sqlDatabaseId string

@description('Resource tags.')
param tags object = {}

// 1. Central DevOps Action Group
resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: actionGroupName
  location: location
  tags: tags
  properties: {
    groupShortName: actionGroupShortName
    enabled: true
    emailReceivers: !empty(alertEmailAddress) ? [
      {
        name: 'DevOpsLead'
        emailAddress: alertEmailAddress
        useCommonAlertSchema: true
      }
    ] : []
  }
}

// 2. Alert Rule 1: HTTP 5xx Server Error Spike (Failed Requests > 5 in 5 min)
resource alertFailedRequests 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-vitalq-api-5xx-errors'
  location: location
  tags: tags
  properties: {
    description: 'Triggers when failed HTTP requests exceed 5 within a 5-minute rolling window.'
    severity: 1
    enabled: true
    scopes: [
      appInsightsId
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'FailedRequestsThreshold'
          metricName: 'requests/failed'
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Count'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

// 3. Alert Rule 2: Container Memory Spike (> 800 MiB / 80% RAM limit)
resource alertContainerMemory 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-vitalq-api-high-memory'
  location: location
  tags: tags
  properties: {
    description: 'Triggers when API container working memory exceeds 800 MiB (proactive OOM warning).'
    severity: 2
    enabled: true
    scopes: [
      apiAppId
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'HighMemoryWorkingSet'
          metricName: 'WorkingSetBytes'
          operator: 'GreaterThan'
          threshold: 838860800 // 800 MiB in bytes
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

// 4. Alert Rule 3: Container CPU Spike (> 80% utilization)
resource alertContainerCpu 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-vitalq-api-high-cpu'
  location: location
  tags: tags
  properties: {
    description: 'Triggers when API container CPU exceeds 80% capacity for 5 minutes.'
    severity: 2
    enabled: true
    scopes: [
      apiAppId
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'HighCpuUsage'
          metricName: 'UsageNanoCores'
          operator: 'GreaterThan'
          threshold: 400000000 // 0.4 Cores (80% of 0.5 vCore allocation)
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

// 5. Alert Rule 4: Azure SQL Serverless High Compute (> 85% vCore)
resource alertSqlDatabaseCpu 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-vitalq-sql-high-compute'
  location: location
  tags: tags
  properties: {
    description: 'Triggers when Azure SQL Serverless compute exceeds 85% utilization for 10 minutes.'
    severity: 2
    enabled: true
    scopes: [
      sqlDatabaseId
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT10M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'HighSqlCpuPercent'
          metricName: 'cpu_percent'
          operator: 'GreaterThan'
          threshold: 85
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

output actionGroupId string = actionGroup.id
output actionGroupName string = actionGroup.name
