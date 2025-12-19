#!/bin/bash
set -e

# Configuration
RESOURCE_GROUP="${RESOURCE_GROUP:-clarissabot-rg}"
LOCATION="${LOCATION:-eastus}"
ENVIRONMENT="${ENVIRONMENT:-dev}"
BASE_NAME="${BASE_NAME:-clarissabot}"

# Existing Azure OpenAI configuration
OPENAI_RESOURCE_GROUP="${OPENAI_RESOURCE_GROUP:-cognitive}"
OPENAI_ACCOUNT_NAME="${OPENAI_ACCOUNT_NAME:-clarissa}"
OPENAI_DEPLOYMENT_NAME="${OPENAI_DEPLOYMENT_NAME:-gpt-4.1}"

# API Key for authentication (generate if not provided)
API_KEY="${API_KEY:-}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}🚀 ClarissaBot Container Deployment${NC}"
echo "======================================"

# Check if logged in to Azure
az account show > /dev/null 2>&1 || { echo -e "${RED}Please run 'az login' first${NC}"; exit 1; }

# Get Azure OpenAI configuration
OPENAI_ENDPOINT=$(az cognitiveservices account show \
  --name "$OPENAI_ACCOUNT_NAME" \
  --resource-group "$OPENAI_RESOURCE_GROUP" \
  --query "properties.endpoint" -o tsv)

# Get ACR info from existing deployment
ACR_NAME=$(az deployment group show \
  --resource-group "$RESOURCE_GROUP" \
  --name "container-registry" \
  --query "properties.outputs.name.value" -o tsv 2>/dev/null || echo "")

if [ -z "$ACR_NAME" ]; then
  # Try to find ACR in resource group
  ACR_NAME=$(az acr list --resource-group "$RESOURCE_GROUP" --query "[0].name" -o tsv)
fi

if [ -z "$ACR_NAME" ]; then
  echo -e "${RED}No container registry found. Run deploy.sh first.${NC}"
  exit 1
fi

ACR_LOGIN_SERVER=$(az acr show --name "$ACR_NAME" --query "loginServer" -o tsv)
ACR_PASSWORD=$(az acr credential show --name "$ACR_NAME" --query "passwords[0].value" -o tsv)

# Image names
API_IMAGE="${ACR_LOGIN_SERVER}/clarissabot-api:latest"
WEB_IMAGE="${ACR_LOGIN_SERVER}/clarissabot-web:latest"

# Check if images exist
echo -e "${YELLOW}Checking for container images...${NC}"
API_EXISTS=$(az acr repository show --name "$ACR_NAME" --image "clarissabot-api:latest" 2>/dev/null && echo "yes" || echo "no")
WEB_EXISTS=$(az acr repository show --name "$ACR_NAME" --image "clarissabot-web:latest" 2>/dev/null && echo "yes" || echo "no")

if [ "$API_EXISTS" == "no" ]; then
  echo -e "${RED}API image not found. Build and push first:${NC}"
  echo "  az acr login --name $ACR_NAME"
  echo "  docker build -t $API_IMAGE -f src/ClarissaBot/ClarissaBot.Api/Dockerfile src/ClarissaBot"
  echo "  docker push $API_IMAGE"
  exit 1
fi

echo -e "${GREEN}✓ API image found${NC}"

# Generate API key if not provided
if [ -z "$API_KEY" ]; then
  echo -e "${YELLOW}Generating API key...${NC}"
  API_KEY=$(openssl rand -base64 32 | tr -d '/+=' | head -c 32)
  echo -e "${GREEN}✓ API key generated${NC}"
fi

# Deploy with container apps
echo -e "${YELLOW}Deploying container apps...${NC}"
DEPLOYMENT_OUTPUT=$(az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file main.bicep \
  --parameters \
    baseName="$BASE_NAME" \
    location="$LOCATION" \
    environment="$ENVIRONMENT" \
    azureOpenAIEndpoint="$OPENAI_ENDPOINT" \
    azureOpenAIDeploymentName="$OPENAI_DEPLOYMENT_NAME" \
    apiImage="$API_IMAGE" \
    $([ "$WEB_EXISTS" == "yes" ] && echo "webImage=$WEB_IMAGE") \
    registryPassword="$ACR_PASSWORD" \
    apiKey="$API_KEY" \
  --query "properties.outputs" -o json)

echo -e "${GREEN}✓ Container apps deployed${NC}"

# Extract outputs
API_URL=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.apiUrl.value')
WEB_URL=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.webUrl.value')
API_PRINCIPAL_ID=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.apiPrincipalId.value')

# Assign Cognitive Services User role to API managed identity
if [ -n "$API_PRINCIPAL_ID" ] && [ "$API_PRINCIPAL_ID" != "" ]; then
  echo -e "${YELLOW}Assigning Cognitive Services User role...${NC}"

  OPENAI_RESOURCE_ID=$(az cognitiveservices account show \
    --name "$OPENAI_ACCOUNT_NAME" \
    --resource-group "$OPENAI_RESOURCE_GROUP" \
    --query "id" -o tsv)

  az role assignment create \
    --assignee-object-id "$API_PRINCIPAL_ID" \
    --assignee-principal-type ServicePrincipal \
    --role "Cognitive Services User" \
    --scope "$OPENAI_RESOURCE_ID" \
    --output none 2>/dev/null || echo "Role already assigned or error assigning role"

  echo -e "${GREEN}✓ Role assignment complete${NC}"
fi

echo ""
echo -e "${GREEN}🎉 Deployment Complete!${NC}"
echo "========================"
echo "API URL: $API_URL"
[ -n "$WEB_URL" ] && [ "$WEB_URL" != "" ] && echo "Web URL: $WEB_URL"
echo ""
echo -e "${YELLOW}🔐 API Key (save this securely):${NC}"
echo "  $API_KEY"
echo ""
echo "Test the API:"
echo "  curl $API_URL/api/health"
echo ""
echo "Test authenticated endpoint:"
echo "  curl -X POST $API_URL/api/chat -H 'Content-Type: application/json' -H 'X-API-Key: $API_KEY' -d '{\"message\": \"Hello\"}'"
echo ""
echo "For frontend build, set environment variable:"
echo "  VITE_API_KEY=$API_KEY npm run build"

