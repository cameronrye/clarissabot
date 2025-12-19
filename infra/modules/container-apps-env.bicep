@description('Name of the Container Apps Environment')
param name string

@description('Location for resources')
param location string = resourceGroup().location

@description('Log Analytics workspace customer ID')
param logAnalyticsCustomerId string

@description('Log Analytics primary shared key')
@secure()
param logAnalyticsPrimaryKey string

@description('Tags to apply to resources')
param tags object = {}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsCustomerId
        sharedKey: logAnalyticsPrimaryKey
      }
    }
    zoneRedundant: false
  }
}

@description('Container Apps Environment ID')
output id string = containerAppsEnvironment.id

@description('Container Apps Environment name')
output name string = containerAppsEnvironment.name

@description('Container Apps Environment default domain')
output defaultDomain string = containerAppsEnvironment.properties.defaultDomain

