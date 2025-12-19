# CI/CD Setup Guide

This document explains how to configure the GitHub Actions CI/CD pipelines for ClarissaBot.

## Workflows

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `ci.yml` | PR, push to main/develop | Build, test, security scan |
| `deploy-dev.yml` | Push to main, manual | Deploy to dev environment |
| `deploy-prod.yml` | Manual only | Deploy to production (requires approval) |

## Required Secrets

Configure these secrets in your GitHub repository settings:

### Azure Credentials

1. **Create a Service Principal** for dev:
   ```bash
   az ad sp create-for-rbac --name "clarissabot-github-dev" \
     --role contributor \
     --scopes /subscriptions/{subscription-id}/resourceGroups/clarissabot-rg \
     --json-auth
   ```

2. **Create a Service Principal** for prod:
   ```bash
   az ad sp create-for-rbac --name "clarissabot-github-prod" \
     --role contributor \
     --scopes /subscriptions/{subscription-id}/resourceGroups/clarissabot-prod-rg \
     --json-auth
   ```

3. **Add the JSON output** as repository secrets:
   - `AZURE_CREDENTIALS` - Dev service principal JSON
   - `AZURE_CREDENTIALS_PROD` - Prod service principal JSON

### Container Registry

```bash
ACR_NAME=$(az acr list --resource-group clarissabot-rg --query "[0].name" -o tsv)
az acr credential show --name $ACR_NAME
```

- `ACR_USERNAME` - Registry username
- `ACR_PASSWORD` - Registry password

### Azure OpenAI

- `OPENAI_ACCOUNT_NAME` - Name of your Azure OpenAI resource
- `OPENAI_RESOURCE_GROUP` - Resource group containing OpenAI
- `OPENAI_DEPLOYMENT_NAME` - Model deployment name (e.g., `gpt-4.1`)

### API Keys

Generate secure API keys:
```bash
openssl rand -base64 32 | tr -d '/+=' | head -c 32
```

- `API_KEY` - API key for dev environment
- `API_KEY_PROD` - API key for production
- `DEV_API_URL` - Dev API URL for web frontend build

## GitHub Environments

The setup script creates two environments:

### `dev` Environment
- Auto-deploys on push to main
- No protection rules

### `production` Environment
- Manual trigger only (workflow_dispatch)
- Requires typing "DEPLOY" to confirm
- Note: Required reviewers need GitHub Team/Enterprise plan

## Initial Setup Steps

1. **Deploy infrastructure first**:
   ```bash
   cd infra
   ./deploy.sh
   ```

2. **Get ACR credentials** and add to GitHub secrets

3. **Add all required secrets** to GitHub

4. **Create GitHub environments** (`dev` and `production`)

5. **Push to main** to trigger first deployment

## Manual Deployment

### Deploy to Dev
```bash
gh workflow run deploy-dev.yml
```

### Deploy to Production
```bash
gh workflow run deploy-prod.yml -f image_tag=latest -f confirm=DEPLOY
```

## Rollback

To rollback, deploy a previous image tag:
```bash
gh workflow run deploy-prod.yml -f image_tag=abc1234 -f confirm=DEPLOY
```

Find available tags:
```bash
az acr repository show-tags --name $ACR_NAME --repository clarissabot-api
```

