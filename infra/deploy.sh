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

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}🚀 ClarissaBot Azure Deployment${NC}"
echo "=================================="
echo "Resource Group: $RESOURCE_GROUP"
echo "Location: $LOCATION"
echo "Environment: $ENVIRONMENT"
echo ""

# Check if logged in to Azure
echo -e "${YELLOW}Checking Azure CLI authentication...${NC}"
az account show > /dev/null 2>&1 || { echo -e "${RED}Please run 'az login' first${NC}"; exit 1; }
echo -e "${GREEN}✓ Authenticated${NC}"

# Get Azure OpenAI endpoint
echo -e "${YELLOW}Getting Azure OpenAI configuration...${NC}"
OPENAI_ENDPOINT=$(az cognitiveservices account show \
  --name "$OPENAI_ACCOUNT_NAME" \
  --resource-group "$OPENAI_RESOURCE_GROUP" \
  --query "properties.endpoint" -o tsv)

echo "OpenAI Endpoint: $OPENAI_ENDPOINT"
echo "OpenAI Deployment: $OPENAI_DEPLOYMENT_NAME"

# Create resource group if it doesn't exist
echo -e "${YELLOW}Creating resource group if needed...${NC}"
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none
echo -e "${GREEN}✓ Resource group ready${NC}"

# Deploy infrastructure (without container apps initially)
echo -e "${YELLOW}Deploying infrastructure...${NC}"
DEPLOYMENT_OUTPUT=$(az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file main.bicep \
  --parameters \
    baseName="$BASE_NAME" \
    location="$LOCATION" \
    environment="$ENVIRONMENT" \
    azureOpenAIEndpoint="$OPENAI_ENDPOINT" \
    azureOpenAIDeploymentName="$OPENAI_DEPLOYMENT_NAME" \
  --query "properties.outputs" -o json)

echo -e "${GREEN}✓ Infrastructure deployed${NC}"

# Extract outputs
ACR_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.containerRegistryName.value')
ACR_LOGIN_SERVER=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.containerRegistryLoginServer.value')

echo ""
echo -e "${GREEN}📦 Deployment Complete!${NC}"
echo "========================"
echo "Container Registry: $ACR_LOGIN_SERVER"
echo ""
echo "Next steps:"
echo "  1. Build and push Docker images:"
echo "     az acr login --name $ACR_NAME"
echo "     docker build -t $ACR_LOGIN_SERVER/clarissabot-api:latest -f src/ClarissaBot/ClarissaBot.Api/Dockerfile ."
echo "     docker push $ACR_LOGIN_SERVER/clarissabot-api:latest"
echo ""
echo "  2. Deploy containers (run this script again with images):"
echo "     ./deploy-apps.sh"

