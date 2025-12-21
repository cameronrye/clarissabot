using Azure.AI.OpenAI;
using Azure.Identity;
using ClarissaBot.Api.Middleware;
using ClarissaBot.Api.Models;
using ClarissaBot.Core.Agent;
using ClarissaBot.Core.Extensions;
using ClarissaBot.Core.Models;

var builder = WebApplication.CreateBuilder(args);

// Add optional local configuration (gitignored) for developer-specific settings
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

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

// Configure DefaultAzureCredential with optimized settings for faster auth
// Exclude credential providers that are slow or not used in this environment
var credentialOptions = new DefaultAzureCredentialOptions
{
    // Keep these enabled (fast, commonly used)
    ExcludeEnvironmentCredential = false,           // Fast: checks env vars
    ExcludeManagedIdentityCredential = false,       // Fast in Azure, skipped locally
    ExcludeAzureCliCredential = false,              // Used for local development

    // Exclude these (slow or not typically used for server apps)
    ExcludeInteractiveBrowserCredential = true,     // Not applicable for server
    ExcludeVisualStudioCredential = true,           // Slow, not needed in production
    ExcludeVisualStudioCodeCredential = true,       // Slow, not needed in production
    ExcludeAzurePowerShellCredential = true,        // Slow, rarely used
    ExcludeAzureDeveloperCliCredential = true,      // Slow, rarely used
    ExcludeWorkloadIdentityCredential = false,      // Keep for Kubernetes scenarios
};

var openAIClient = new AzureOpenAIClient(
    new Uri(endpoint),
    new DefaultAzureCredential(credentialOptions));

var chatClient = openAIClient.GetChatClient(deploymentName);
builder.Services.AddClarissaAgent(chatClient);

var app = builder.Build();

// Pre-warm Azure OpenAI connection in background to eliminate first-request delay
_ = Task.Run(async () =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var agent = scope.ServiceProvider.GetRequiredService<IClarissaAgent>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Pre-warming Azure OpenAI connection...");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Send a minimal request to establish connection and warm up the model
        await agent.ChatAsync("ping", "warmup-session");
        agent.ClearConversation("warmup-session");

        sw.Stop();
        logger.LogInformation("Azure OpenAI connection warmed up in {ElapsedMs}ms", sw.ElapsedMilliseconds);
    }
    catch (Exception ex)
    {
        // Log but don't fail startup - warmup is optional optimization
        var logger = app.Services.GetService<ILogger<Program>>();
        logger?.LogWarning(ex, "Failed to pre-warm Azure OpenAI connection");
    }
});

// Add exception handling middleware for production
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages();
app.UseCors("AllowFrontend");

// Rate limiting (before authentication to prevent DoS)
app.UseRateLimiting();

// API Key authentication (skipped if no API_KEY configured in development)
app.UseApiKeyAuthentication();

// Health check endpoint
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    service = "ClarissaBot API"
}))
.WithName("GetHealth");

// Streaming Chat endpoint: Streams agent response via SSE with rich events
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
        httpContext.Response.Headers.Append("X-Accel-Buffering", "no"); // Prevent nginx/proxy buffering

        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();

        await WriteConversationIdEvent(httpContext.Response, conversationId, cancellationToken);

        var startTime = DateTime.UtcNow;

        await foreach (var streamEvent in agent.ChatStreamRichAsync(
            request.Message,
            conversationId,
            cancellationToken))
        {
            await WriteStreamingEvent(httpContext.Response, streamEvent, cancellationToken);
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

static async Task WriteStreamingEvent(HttpResponse response, StreamingEvent streamEvent, CancellationToken ct)
{
    var json = streamEvent switch
    {
        ContentChunkEvent chunk => System.Text.Json.JsonSerializer.Serialize(new { type = "chunk", content = chunk.Content }),
        ToolCallEvent toolCall => System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "toolCall",
            toolName = toolCall.ToolName,
            description = toolCall.Description,
            vehicleInfo = toolCall.VehicleInfo
        }),
        ToolResultEvent toolResult => System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "toolResult",
            toolName = toolResult.ToolName,
            success = toolResult.Success
        }),
        VehicleContextEvent vehicleContext => System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "vehicleContext",
            year = vehicleContext.Year,
            make = vehicleContext.Make,
            model = vehicleContext.Model,
            display = vehicleContext.Display
        }),
        _ => System.Text.Json.JsonSerializer.Serialize(new { type = "unknown" })
    };
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

