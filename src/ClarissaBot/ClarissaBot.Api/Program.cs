using Azure.AI.OpenAI;
using Azure.Identity;
using ClarissaBot.Api.Models;
using ClarissaBot.Core.Agent;
using ClarissaBot.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add ProblemDetails service for standardized RFC 7807 error responses
builder.Services.AddProblemDetails();

// Configure CORS for local development
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin =>
            {
                if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return uri.Host is "localhost" or "127.0.0.1";
                }
                return false;
            })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

// Register ClarissaBot services
builder.Services.AddClarissaBotCore();

// Configure Azure OpenAI
var endpoint = builder.Configuration["AzureOpenAI:Endpoint"]
    ?? builder.Configuration["AZURE_OPENAI_ENDPOINT"];
var deploymentName = builder.Configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4o-mini";

if (string.IsNullOrEmpty(endpoint))
{
    throw new InvalidOperationException(
        "Azure OpenAI endpoint is not configured. " +
        "Please set 'AzureOpenAI:Endpoint' in appsettings.json or " +
        "set the 'AZURE_OPENAI_ENDPOINT' environment variable.");
}

var openAIClient = new AzureOpenAIClient(
    new Uri(endpoint),
    new DefaultAzureCredential());

var chatClient = openAIClient.GetChatClient(deploymentName);
builder.Services.AddClarissaAgent(chatClient);

var app = builder.Build();

// Add exception handling middleware for production
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages();
app.UseCors("AllowFrontend");

// Health check endpoint
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    service = "ClarissaBot API"
}))
.WithName("GetHealth");

// Streaming Chat endpoint: Streams agent response via SSE
app.MapPost("/api/chat/stream", async (
    ChatRequest request,
    IClarissaAgent agent,
    HttpContext httpContext,
    IHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    try
    {
        httpContext.Response.Headers.Append("Content-Type", "text/event-stream");
        httpContext.Response.Headers.Append("Cache-Control", "no-cache");
        httpContext.Response.Headers.Append("Connection", "keep-alive");

        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();

        await WriteConversationIdEvent(httpContext.Response, conversationId, cancellationToken);

        var startTime = DateTime.UtcNow;

        await foreach (var chunk in agent.ChatStreamAsync(
            request.Message,
            conversationId,
            cancellationToken))
        {
            await WriteChunkEvent(httpContext.Response, chunk, cancellationToken);
        }

        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        await WriteUsageEvent(httpContext.Response, duration, cancellationToken);
        await WriteDoneEvent(httpContext.Response, cancellationToken);
    }
    catch (Exception ex)
    {
        var errorResponse = ErrorResponseFactory.CreateFromException(
            ex,
            500,
            environment.IsDevelopment());

        await WriteErrorEvent(
            httpContext.Response,
            errorResponse.Detail ?? errorResponse.Title,
            cancellationToken);
    }
})
.WithName("StreamChatMessage");

// Non-streaming Chat endpoint for simpler clients
app.MapPost("/api/chat", async (
    ChatRequest request,
    IClarissaAgent agent,
    IHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    try
    {
        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();
        var response = await agent.ChatAsync(request.Message, conversationId, cancellationToken);

        return Results.Ok(new ChatResponse
        {
            Message = response,
            ConversationId = conversationId
        });
    }
    catch (Exception ex)
    {
        var errorResponse = ErrorResponseFactory.CreateFromException(
            ex,
            500,
            environment.IsDevelopment());

        return Results.Problem(
            title: errorResponse.Title,
            detail: errorResponse.Detail,
            statusCode: errorResponse.Status);
    }
})
.WithName("ChatMessage");

// Clear conversation endpoint
app.MapDelete("/api/chat/{conversationId}", (
    string conversationId,
    IClarissaAgent agent) =>
{
    agent.ClearConversation(conversationId);
    return Results.NoContent();
})
.WithName("ClearConversation");

app.Run();

// SSE Helper Methods
static async Task WriteConversationIdEvent(HttpResponse response, string conversationId, CancellationToken ct)
{
    await response.WriteAsync(
        $"data: {{\"type\":\"conversationId\",\"conversationId\":\"{conversationId}\"}}\n\n",
        ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteChunkEvent(HttpResponse response, string content, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(new { type = "chunk", content });
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteUsageEvent(HttpResponse response, double duration, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(new { type = "usage", duration });
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteDoneEvent(HttpResponse response, CancellationToken ct)
{
    await response.WriteAsync("data: {\"type\":\"done\"}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteErrorEvent(HttpResponse response, string message, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(new { type = "error", message });
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

