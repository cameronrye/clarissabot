# NHTSA RFT (Reinforcement Fine-Tuning) Setup Guide

This guide documents how to set up and run Reinforcement Fine-Tuning for the NHTSA automotive safety chatbot using Azure OpenAI.

## Overview

RFT uses a custom grader to evaluate model responses against live NHTSA API data, ensuring the fine-tuned model produces factually accurate answers about vehicle recalls, complaints, and safety ratings.

## Prerequisites

1. **Azure Subscription** with AI Foundry access (preview)
2. **Azure OpenAI Resource** with access to o4-mini model
3. **Azure CLI** authenticated (`az login`)
4. **Python 3.10+** installed

## Files Required

| File | Description |
|------|-------------|
| `rft_training_expanded.jsonl` | Training examples (502 samples) |
| `rft_validation_expanded.jsonl` | Validation examples (73 samples) |
| `graders/nhtsa_grader.py` | Python grader that validates against live API |
| `graders/python_grader.json` | Grader configuration |
| `schema.json` | Structured output schema |

## Step 1: Prepare Azure Resources

```bash
# Login to Azure
az login

# Set subscription
az account set --subscription "<subscription-id>"

# Create resource group (if needed)
az group create --name rg-nhtsa-rft --location eastus2

# Create Azure OpenAI resource
az cognitiveservices account create \
  --name aoai-nhtsa-rft \
  --resource-group rg-nhtsa-rft \
  --kind OpenAI \
  --sku S0 \
  --location eastus2

# Get endpoint and key
az cognitiveservices account show \
  --name aoai-nhtsa-rft \
  --resource-group rg-nhtsa-rft \
  --query "properties.endpoint" -o tsv

az cognitiveservices account keys list \
  --name aoai-nhtsa-rft \
  --resource-group rg-nhtsa-rft \
  --query "key1" -o tsv
```

## Step 2: Upload Training Files

```bash
# Set environment variables
export AZURE_OPENAI_ENDPOINT="https://aoai-nhtsa-rft.openai.azure.com/"
export AZURE_OPENAI_KEY="<your-key>"

# Upload training file
curl -X POST "$AZURE_OPENAI_ENDPOINT/openai/files?api-version=2024-10-01-preview" \
  -H "api-key: $AZURE_OPENAI_KEY" \
  -H "Content-Type: multipart/form-data" \
  -F "purpose=fine-tune" \
  -F "file=@rft_training_expanded.jsonl"

# Upload validation file
curl -X POST "$AZURE_OPENAI_ENDPOINT/openai/files?api-version=2024-10-01-preview" \
  -H "api-key: $AZURE_OPENAI_KEY" \
  -H "Content-Type: multipart/form-data" \
  -F "purpose=fine-tune" \
  -F "file=@rft_validation_expanded.jsonl"
```

## Step 3: Configure RFT Job

Create `rft_job_config.json`:

```json
{
  "model": "o4-mini",
  "training_file": "<training-file-id>",
  "validation_file": "<validation-file-id>",
  "method": "reinforcement",
  "reinforcement": {
    "grader": {
      "type": "python",
      "source": "nhtsa_grader.py",
      "pass_threshold": 0.7
    }
  },
  "hyperparameters": {
    "n_epochs": 3,
    "batch_size": 8,
    "learning_rate_multiplier": 1.0
  },
  "suffix": "nhtsa-safety-v1"
}
```

## Step 4: Submit RFT Job

```bash
# Upload grader script
curl -X POST "$AZURE_OPENAI_ENDPOINT/openai/files?api-version=2024-10-01-preview" \
  -H "api-key: $AZURE_OPENAI_KEY" \
  -H "Content-Type: multipart/form-data" \
  -F "purpose=fine-tune" \
  -F "file=@graders/nhtsa_grader.py"

# Create fine-tuning job
curl -X POST "$AZURE_OPENAI_ENDPOINT/openai/fine_tuning/jobs?api-version=2024-10-01-preview" \
  -H "api-key: $AZURE_OPENAI_KEY" \
  -H "Content-Type: application/json" \
  -d @rft_job_config.json
```

## Step 5: Monitor Training

```bash
# Check job status
curl "$AZURE_OPENAI_ENDPOINT/openai/fine_tuning/jobs/<job-id>?api-version=2024-10-01-preview" \
  -H "api-key: $AZURE_OPENAI_KEY"

# List events
curl "$AZURE_OPENAI_ENDPOINT/openai/fine_tuning/jobs/<job-id>/events?api-version=2024-10-01-preview" \
  -H "api-key: $AZURE_OPENAI_KEY"
```

## Step 6: Deploy Fine-tuned Model

```bash
# Create deployment
az cognitiveservices account deployment create \
  --name aoai-nhtsa-rft \
  --resource-group rg-nhtsa-rft \
  --deployment-name nhtsa-safety-v1 \
  --model-name <fine-tuned-model-id> \
  --model-version "1" \
  --model-format OpenAI \
  --sku-capacity 10 \
  --sku-name Standard
```

## Grader Details

The Python grader (`nhtsa_grader.py`) validates responses by:

1. **Recalls**: Checks if response correctly identifies recall existence and campaign numbers
2. **Complaints**: Validates complaint acknowledgment against actual complaint count
3. **Safety Ratings**: Verifies star ratings match NHTSA data
4. **Safety Features**: Confirms feature availability (standard/optional)
5. **Comparisons**: Checks if both vehicles' data is correctly referenced

### Scoring Thresholds

| Score | Meaning |
|-------|---------|
| 0.7-1.0 | Good response (passes) |
| 0.3-0.7 | Neutral/partial |
| 0.0-0.3 | Bad response (fails) |

## Training Data Distribution

| Query Type | Training | Validation |
|------------|----------|------------|
| complaints | 107 | 14 |
| safety_rating | 84 | 12 |
| recalls | 82 | 10 |
| multi_turn | 52 | 8 |
| safety_features | 40 | 4 |
| comparison | 35 | 5 |
| edge_cases | 50 | 10 |

**Total: 502 training / 73 validation examples**

## Next Steps

After training completes:
1. Run evaluation script against validation set
2. Compare to base model performance
3. Deploy to Azure AI Foundry agent
