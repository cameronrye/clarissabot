<p align="center">
  <img src="src/ClarissaBot.Web/public/favicon.svg" alt="Clarissa Logo" width="120" height="120">
</p>

# ClarissaBot

ClarissaBot is an AI-powered vehicle safety assistant that provides information from the National Highway Traffic Safety Administration (NHTSA). It helps users check vehicle recalls, safety ratings, and consumer complaints using natural language queries.

## Features

- **Recall Lookup** - Check for safety recalls on any vehicle by year, make, and model
- **Safety Ratings** - Get NCAP crash test ratings (overall, frontal, side, rollover)
- **Complaint Search** - View consumer complaints and common issues for vehicles
- **Vehicle Comparison** - Compare safety data between two vehicles
- **Streaming Responses** - Real-time streaming chat with Server-Sent Events (SSE)

## Architecture

```text
src/
├── ClarissaBot/
│   ├── ClarissaBot.Api/       # ASP.NET Core minimal API
│   ├── ClarissaBot.Core/      # Core agent logic and NHTSA tools
│   ├── ClarissaBot.Console/   # Interactive console application
│   └── ClarissaBot.Tests/     # Unit tests
└── ClarissaBot.Web/           # React + TypeScript frontend

data/
├── graders/                   # RFT grader for model fine-tuning
├── rft_training*.jsonl        # Training data (502 samples)
├── rft_validation*.jsonl      # Validation data (73 samples)
└── schema.json                # Structured output schema
```

## Prerequisites

- .NET 8 or 9 SDK
- Node.js 18+
- Azure subscription with Azure OpenAI access
- Azure CLI authenticated (`az login`)

## Configuration

### Backend

Create or update `src/ClarissaBot/ClarissaBot.Api/appsettings.json`:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "DeploymentName": "gpt-4o-mini"
  }
}
```

Or set environment variables:

- `AZURE_OPENAI_ENDPOINT` - Your Azure OpenAI endpoint URL

The API uses `DefaultAzureCredential` for authentication, so ensure you're logged in via Azure CLI.

## Running Locally

### Start the Backend API

```bash
cd src/ClarissaBot/ClarissaBot.Api
dotnet run
```

The API runs at `http://localhost:5080`

### Start the Frontend

```bash
cd src/ClarissaBot.Web
npm install
npm run dev
```

The frontend runs at `http://localhost:5173`

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/chat/stream` | Streaming chat via SSE |
| POST | `/api/chat` | Non-streaming chat |
| DELETE | `/api/chat/{conversationId}` | Clear a conversation |
| GET | `/api/health` | Health check |

### Chat Request

```json
{
  "message": "Are there any recalls on the 2020 Tesla Model 3?",
  "conversationId": "optional-conversation-id"
}
```

## NHTSA Tools

The agent uses Azure OpenAI function calling with the following tools:

- `check_recalls` - Get recalls by vehicle year/make/model
- `get_complaints` - Get consumer complaints
- `get_safety_rating` - Get NCAP safety ratings

## Running Tests

```bash
cd src/ClarissaBot
dotnet test
```

## Fine-Tuning (RFT)

The `data/` directory contains training data and graders for Reinforcement Fine-Tuning on Azure OpenAI.

## License

MIT

---

Made with ❤️ by [Cameron Rye](https://rye.dev)