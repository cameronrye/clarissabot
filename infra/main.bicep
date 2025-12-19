@description('Base name for all resources')
param baseName string = 'clarissabot'

@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment name (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environment string = 'dev'

@description('Azure OpenAI endpoint URL')
param azureOpenAIEndpoint string

@description('Azure OpenAI deployment name')
param azureOpenAIDeploymentName string = 'gpt-4.1'

@description('API container image')
param apiImage string = ''

@description('Web container image')
param webImage string = ''

@description('Container registry password')
@secure()
param registryPassword string = ''

@description('API key for authenticating requests (leave empty to disable)')
@secure()
param apiKey string = ''

@description('Custom domain for the web app (e.g., bot.clarissa.run)')
param webCustomDomain string = ''

@description('Managed certificate ID for the custom domain (leave empty for initial setup)')
param webCustomDomainCertificateId string = ''

var resourceToken = toLower(uniqueString(subscription().id, resourceGroup().id, baseName))
var tags = {
  application: baseName
  environment: environment
}

// Container Registry
module containerRegistry 'modules/container-registry.bicep' = {
  name: 'container-registry'
  params: {
    name: '${baseName}${resourceToken}'
    location: location
    sku: 'Basic'
    tags: tags
  }
}

// Monitoring (Log Analytics + Application Insights)
module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    logAnalyticsName: '${baseName}-logs-${environment}'
    appInsightsName: '${baseName}-insights-${environment}'
    location: location
    tags: tags
  }
}

// Container Apps Environment
module containerAppsEnv 'modules/container-apps-env.bicep' = {
  name: 'container-apps-env'
  params: {
    name: '${baseName}-env-${environment}'
    location: location
    logAnalyticsCustomerId: monitoring.outputs.logAnalyticsCustomerId
    logAnalyticsPrimaryKey: monitoring.outputs.logAnalyticsPrimaryKey
    tags: tags
  }
}

// API Container App (only deploy if image is provided)
module apiApp 'modules/container-app.bicep' = if (!empty(apiImage)) {
  name: 'api-container-app'
  params: {
    name: '${baseName}-api-${environment}'
    location: location
    containerAppsEnvironmentId: containerAppsEnv.outputs.id
    containerImage: apiImage
    registryServer: containerRegistry.outputs.loginServer
    registryUsername: containerRegistry.outputs.name
    registryPassword: registryPassword
    targetPort: 8080
    externalIngress: true
    useManagedIdentity: true
    cpu: '0.5'
    memory: '1Gi'
    minReplicas: 0
    maxReplicas: 3
    tags: tags
    envVars: concat([
      { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
      { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
      { name: 'AZURE_OPENAI_ENDPOINT', value: azureOpenAIEndpoint }
      { name: 'AzureOpenAI__Endpoint', value: azureOpenAIEndpoint }
      { name: 'AzureOpenAI__DeploymentName', value: azureOpenAIDeploymentName }
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: monitoring.outputs.appInsightsConnectionString }
      { name: 'Cors__AllowedOrigins__0', value: !empty(webCustomDomain) ? 'https://${webCustomDomain}' : '' }
    ], !empty(apiKey) ? [{ name: 'API_KEY', secretRef: 'api-key' }] : [])
    additionalSecrets: !empty(apiKey) ? [{ name: 'api-key', value: apiKey }] : []
  }
}

// Web Container App (only deploy if image is provided)
module webApp 'modules/container-app.bicep' = if (!empty(webImage)) {
  name: 'web-container-app'
  params: {
    name: '${baseName}-web-${environment}'
    location: location
    containerAppsEnvironmentId: containerAppsEnv.outputs.id
    containerImage: webImage
    registryServer: containerRegistry.outputs.loginServer
    registryUsername: containerRegistry.outputs.name
    registryPassword: registryPassword
    targetPort: 80
    externalIngress: true
    useManagedIdentity: false
    cpu: '0.25'
    memory: '0.5Gi'
    minReplicas: 0
    maxReplicas: 3
    tags: tags
    envVars: concat([
      { name: 'API_URL', value: !empty(apiImage) ? apiApp.outputs.url : '' }
    ], !empty(apiKey) ? [{ name: 'API_KEY', secretRef: 'api-key' }] : [])
    additionalSecrets: !empty(apiKey) ? [{ name: 'api-key', value: apiKey }] : []
    customDomain: webCustomDomain
    customDomainCertificateId: webCustomDomainCertificateId
  }
}

// Outputs
output containerRegistryLoginServer string = containerRegistry.outputs.loginServer
output containerRegistryName string = containerRegistry.outputs.name
output containerAppsEnvironmentName string = containerAppsEnv.outputs.name
output containerAppsEnvironmentDomain string = containerAppsEnv.outputs.defaultDomain
output apiUrl string = !empty(apiImage) ? apiApp.outputs.url : ''
output webUrl string = !empty(webImage) ? webApp.outputs.url : ''
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString
output apiPrincipalId string = !empty(apiImage) ? apiApp.outputs.principalId : ''

