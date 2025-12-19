@description('Name of the Container App')
param name string

@description('Location for resources')
param location string = resourceGroup().location

@description('Container Apps Environment ID')
param containerAppsEnvironmentId string

@description('Container image to deploy')
param containerImage string

@description('Container registry login server')
param registryServer string

@description('Container registry username')
param registryUsername string

@description('Container registry password')
@secure()
param registryPassword string

@description('Target port for the container')
param targetPort int = 8080

@description('Whether ingress is external')
param externalIngress bool = true

@description('Environment variables for the container')
param envVars array = []

@description('Additional secrets for the container (beyond registry password)')
param additionalSecrets array = []

@description('CPU cores allocated to the container')
param cpu string = '0.5'

@description('Memory allocated to the container')
param memory string = '1Gi'

@description('Minimum number of replicas')
param minReplicas int = 0

@description('Maximum number of replicas')
param maxReplicas int = 3

@description('Tags to apply to resources')
param tags object = {}

@description('Whether to use managed identity')
param useManagedIdentity bool = false

@description('User-assigned managed identity ID (optional)')
param userAssignedIdentityId string = ''

var identityType = useManagedIdentity ? (empty(userAssignedIdentityId) ? 'SystemAssigned' : 'UserAssigned') : 'None'
var userAssignedIdentities = !empty(userAssignedIdentityId) ? { '${userAssignedIdentityId}': {} } : null

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  tags: tags
  identity: useManagedIdentity ? {
    type: identityType
    userAssignedIdentities: userAssignedIdentities
  } : null
  properties: {
    managedEnvironmentId: containerAppsEnvironmentId
    configuration: {
      ingress: {
        external: externalIngress
        targetPort: targetPort
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: registryServer
          username: registryUsername
          passwordSecretRef: 'registry-password'
        }
      ]
      secrets: concat([
        {
          name: 'registry-password'
          value: registryPassword
        }
      ], additionalSecrets)
    }
    template: {
      containers: [
        {
          name: name
          image: containerImage
          resources: {
            cpu: json(cpu)
            memory: memory
          }
          env: envVars
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-scale'
            http: {
              metadata: {
                concurrentRequests: '100'
              }
            }
          }
        ]
      }
    }
  }
}

@description('Container App FQDN')
output fqdn string = containerApp.properties.configuration.ingress.fqdn

@description('Container App URL')
output url string = 'https://${containerApp.properties.configuration.ingress.fqdn}'

@description('Container App ID')
output id string = containerApp.id

@description('Container App principal ID (if system-assigned identity)')
output principalId string = useManagedIdentity && identityType == 'SystemAssigned' ? containerApp.identity.principalId : ''

