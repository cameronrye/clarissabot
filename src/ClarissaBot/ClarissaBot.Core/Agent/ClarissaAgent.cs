namespace ClarissaBot.Core.Agent;

using System.ClientModel;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using ClarissaBot.Core.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

/// <summary>
/// Clarissa AI agent implementation using Azure OpenAI with function calling.
/// Includes automatic conversation cleanup to prevent memory leaks.
/// </summary>
public class ClarissaAgent : IClarissaAgent, IDisposable
{
    private readonly ChatClient _chatClient;
    private readonly NhtsaTools _nhtsaTools;
    private readonly ILogger<ClarissaAgent> _logger;
    private readonly ConcurrentDictionary<string, ConversationEntry> _conversations = new();
    private readonly List<ChatTool> _tools;
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _maxIdleTime;
    private readonly int _maxConversations;
    private bool _disposed;

    private const int MaxRetries = 3;
    private const int MaxToolIterations = 10;

    /// <summary>
    /// Tracks a conversation and its last access time.
    /// </summary>
    private sealed class ConversationEntry
    {
        public List<ChatMessage> Messages { get; }
        public DateTime LastAccessedUtc { get; set; }

        public ConversationEntry(List<ChatMessage> messages)
        {
            Messages = messages;
            LastAccessedUtc = DateTime.UtcNow;
        }
    }

    public ClarissaAgent(
        ChatClient chatClient,
        NhtsaTools nhtsaTools,
        ILogger<ClarissaAgent> logger,
        IConfiguration? configuration = null)
    {
        _chatClient = chatClient;
        _nhtsaTools = nhtsaTools;
        _logger = logger;
        _tools = CreateToolDefinitions();

        // Configure conversation cleanup from settings
        var maxIdleMinutes = configuration?.GetValue("Conversations:MaxIdleMinutes", 60) ?? 60;
        var cleanupIntervalMinutes = configuration?.GetValue("Conversations:CleanupIntervalMinutes", 5) ?? 5;
        _maxConversations = configuration?.GetValue("Conversations:MaxConversations", 10000) ?? 10000;
        _maxIdleTime = TimeSpan.FromMinutes(maxIdleMinutes);

        // Start cleanup timer
        _cleanupTimer = new Timer(
            CleanupExpiredConversations,
            null,
            TimeSpan.FromMinutes(cleanupIntervalMinutes),
            TimeSpan.FromMinutes(cleanupIntervalMinutes));
    }

    /// <inheritdoc />
    public async Task<string> ChatAsync(string userMessage, string? conversationId = null, CancellationToken cancellationToken = default)
    {
        conversationId ??= "default";
        var entry = GetOrCreateConversation(conversationId);
        var messages = entry.Messages;

        messages.Add(new UserChatMessage(userMessage));
        _logger.LogDebug("User: {Message}", userMessage);

        var options = CreateChatOptions();
        var iterations = 0;

        while (iterations++ < MaxToolIterations)
        {
            var completion = await ExecuteWithRetryAsync(
                () => _chatClient.CompleteChatAsync(messages, options, cancellationToken),
                cancellationToken);

            if (completion.FinishReason == ChatFinishReason.ToolCalls)
            {
                await ProcessToolCallsAsync(messages, completion, cancellationToken);
            }
            else
            {
                // Safely access response content with null check
                var responseText = completion.Content is { Count: > 0 }
                    ? completion.Content[0].Text ?? string.Empty
                    : string.Empty;

                if (string.IsNullOrEmpty(responseText))
                {
                    _logger.LogWarning("Received empty response from OpenAI");
                    responseText = "I apologize, but I was unable to generate a response. Please try again.";
                }

                messages.Add(new AssistantChatMessage(responseText));
                _logger.LogDebug("Assistant: {Response}", TruncateForLog(responseText));
                return responseText;
            }
        }

        throw new InvalidOperationException($"Agent exceeded maximum tool iterations ({MaxToolIterations})");
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ChatStreamAsync(
        string userMessage,
        string? conversationId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        conversationId ??= "default";
        var entry = GetOrCreateConversation(conversationId);
        var messages = entry.Messages;

        messages.Add(new UserChatMessage(userMessage));
        _logger.LogDebug("User: {Message}", userMessage);

        var options = CreateChatOptions();
        var iterations = 0;
        var responseBuilder = new StringBuilder();

        while (iterations++ < MaxToolIterations)
        {
            var streamingUpdates = _chatClient.CompleteChatStreamingAsync(messages, options, cancellationToken);

            var toolCallsById = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();
            ChatFinishReason? finishReason = null;

            await foreach (var update in streamingUpdates.WithCancellation(cancellationToken))
            {
                // Collect tool call information
                foreach (var toolCallUpdate in update.ToolCallUpdates)
                {
                    if (!toolCallsById.TryGetValue(toolCallUpdate.Index, out var existing))
                    {
                        toolCallsById[toolCallUpdate.Index] = (toolCallUpdate.ToolCallId, toolCallUpdate.FunctionName, new StringBuilder());
                        existing = toolCallsById[toolCallUpdate.Index];
                    }
                    existing.Args.Append(toolCallUpdate.FunctionArgumentsUpdate);
                }

                // Stream content tokens to caller
                foreach (var contentPart in update.ContentUpdate)
                {
                    if (!string.IsNullOrEmpty(contentPart.Text))
                    {
                        responseBuilder.Append(contentPart.Text);
                        yield return contentPart.Text;
                    }
                }

                if (update.FinishReason.HasValue)
                {
                    finishReason = update.FinishReason;
                }
            }

            if (finishReason == ChatFinishReason.ToolCalls)
            {
                // Build tool calls for processing
                var toolCalls = toolCallsById.Values
                    .Select(tc => ChatToolCall.CreateFunctionToolCall(tc.Id, tc.Name, BinaryData.FromString(tc.Args.ToString())))
                    .ToList();

                var assistantMessage = new AssistantChatMessage(toolCalls);
                messages.Add(assistantMessage);

                foreach (var toolCall in toolCalls)
                {
                    _logger.LogDebug("Tool call: {Name}({Args})", toolCall.FunctionName, TruncateForLog(toolCall.FunctionArguments.ToString()));
                    var result = await ExecuteToolCallAsync(toolCall, cancellationToken);
                    _logger.LogDebug("Tool result: {Result}", TruncateForLog(result));
                    messages.Add(new ToolChatMessage(toolCall.Id, result));
                }

                responseBuilder.Clear();
            }
            else
            {
                // Final response complete
                messages.Add(new AssistantChatMessage(responseBuilder.ToString()));
                yield break;
            }
        }

        throw new InvalidOperationException($"Agent exceeded maximum tool iterations ({MaxToolIterations})");
    }

    /// <inheritdoc />
    public void ClearConversation(string? conversationId = null)
    {
        conversationId ??= "default";
        _conversations.TryRemove(conversationId, out _);
    }

    /// <summary>
    /// Gets or creates a conversation entry, updating the last access time.
    /// </summary>
    private ConversationEntry GetOrCreateConversation(string conversationId)
    {
        var entry = _conversations.GetOrAdd(conversationId, _ =>
            new ConversationEntry([new SystemChatMessage(AgentInstructions.GetInstructions())]));

        entry.LastAccessedUtc = DateTime.UtcNow;

        // Evict oldest conversations if we exceed the limit
        if (_conversations.Count > _maxConversations)
        {
            EvictOldestConversations();
        }

        return entry;
    }

    /// <summary>
    /// Removes expired conversations that have been idle too long.
    /// </summary>
    private void CleanupExpiredConversations(object? state)
    {
        var cutoff = DateTime.UtcNow - _maxIdleTime;
        var expiredKeys = _conversations
            .Where(kvp => kvp.Value.LastAccessedUtc < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            if (_conversations.TryRemove(key, out _))
            {
                _logger.LogDebug("Removed expired conversation: {ConversationId}", key);
            }
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired conversations", expiredKeys.Count);
        }
    }

    /// <summary>
    /// Evicts the oldest conversations when the limit is exceeded.
    /// </summary>
    private void EvictOldestConversations()
    {
        var excess = _conversations.Count - _maxConversations + 100; // Remove 100 extra to avoid frequent evictions
        if (excess <= 0) return;

        var oldestKeys = _conversations
            .OrderBy(kvp => kvp.Value.LastAccessedUtc)
            .Take(excess)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in oldestKeys)
        {
            _conversations.TryRemove(key, out _);
        }

        _logger.LogInformation("Evicted {Count} oldest conversations due to capacity limit", oldestKeys.Count);
    }

    /// <summary>
    /// Disposes resources used by the agent.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _cleanupTimer.Dispose();
        _conversations.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private ChatCompletionOptions CreateChatOptions()
    {
        var options = new ChatCompletionOptions();
        foreach (var tool in _tools)
        {
            options.Tools.Add(tool);
        }
        return options;
    }

    private async Task<ChatCompletion> ExecuteWithRetryAsync(
        Func<Task<ClientResult<ChatCompletion>>> action,
        CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(1);

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var result = await action();
                return result.Value;
            }
            catch (ClientResultException ex) when (IsTransientError(ex) && attempt < MaxRetries)
            {
                _logger.LogWarning(ex, "Transient error on attempt {Attempt}/{MaxRetries}, retrying in {Delay}s",
                    attempt, MaxRetries, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
                delay *= 2; // Exponential backoff
            }
        }

        throw new InvalidOperationException($"Failed after {MaxRetries} retry attempts");
    }

    private static bool IsTransientError(ClientResultException ex)
    {
        // Retry on rate limiting (429) or server errors (5xx)
        return ex.Status == 429 || (ex.Status >= 500 && ex.Status < 600);
    }

    private async Task ProcessToolCallsAsync(
        List<ChatMessage> messages,
        ChatCompletion completion,
        CancellationToken cancellationToken)
    {
        messages.Add(new AssistantChatMessage(completion));

        foreach (var toolCall in completion.ToolCalls)
        {
            _logger.LogDebug("Tool call: {Name}({Args})", toolCall.FunctionName, TruncateForLog(toolCall.FunctionArguments.ToString()));
            var result = await ExecuteToolCallAsync(toolCall, cancellationToken);
            _logger.LogDebug("Tool result: {Result}", TruncateForLog(result));
            messages.Add(new ToolChatMessage(toolCall.Id, result));
        }
    }

    private async Task<string> ExecuteToolCallAsync(ChatToolCall toolCall, CancellationToken cancellationToken)
    {
        try
        {
            using var args = JsonDocument.Parse(toolCall.FunctionArguments);
            var root = args.RootElement;

            return toolCall.FunctionName switch
            {
                "check_recalls" => await _nhtsaTools.CheckRecallsAsync(
                    root.GetProperty("make").GetString()!,
                    root.GetProperty("model").GetString()!,
                    root.GetProperty("year").GetInt32()),
                "get_complaints" => await _nhtsaTools.GetComplaintsAsync(
                    root.GetProperty("make").GetString()!,
                    root.GetProperty("model").GetString()!,
                    root.GetProperty("year").GetInt32()),
                "get_safety_rating" => await _nhtsaTools.GetSafetyRatingAsync(
                    root.GetProperty("make").GetString()!,
                    root.GetProperty("model").GetString()!,
                    root.GetProperty("year").GetInt32()),
                _ => JsonSerializer.Serialize(new { error = $"Unknown function: {toolCall.FunctionName}" })
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error executing tool {Name}", toolCall.FunctionName);
            return JsonSerializer.Serialize(new { error = $"Network error: {ex.Message}" });
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
        {
            _logger.LogWarning(ex, "Timeout executing tool {Name}", toolCall.FunctionName);
            return JsonSerializer.Serialize(new { error = "Request timed out" });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid arguments for tool {Name}", toolCall.FunctionName);
            return JsonSerializer.Serialize(new { error = $"Invalid arguments: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {Name}", toolCall.FunctionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private static string TruncateForLog(string value, int maxLength = 200)
    {
        return value.Length > maxLength ? value[..maxLength] + "..." : value;
    }

    private static List<ChatTool> CreateToolDefinitions()
    {
        return
        [
            ChatTool.CreateFunctionTool(
                "check_recalls",
                "Check for vehicle recalls from NHTSA. Returns recall campaigns, affected components, and remedies.",
                BinaryData.FromObjectAsJson(new
                {
                    type = "object",
                    properties = new
                    {
                        make = new { type = "string", description = "Vehicle manufacturer (e.g., Toyota, Ford, Tesla)" },
                        model = new { type = "string", description = "Vehicle model name (e.g., Camry, F-150, Model 3)" },
                        year = new { type = "integer", description = "Model year (e.g., 2020, 2024)" }
                    },
                    required = new[] { "make", "model", "year" }
                })),
            ChatTool.CreateFunctionTool(
                "get_complaints",
                "Get consumer complaints filed with NHTSA for a vehicle. Shows reported problems, crashes, and fires.",
                BinaryData.FromObjectAsJson(new
                {
                    type = "object",
                    properties = new
                    {
                        make = new { type = "string", description = "Vehicle manufacturer" },
                        model = new { type = "string", description = "Vehicle model name" },
                        year = new { type = "integer", description = "Model year" }
                    },
                    required = new[] { "make", "model", "year" }
                })),
            ChatTool.CreateFunctionTool(
                "get_safety_rating",
                "Get NCAP safety ratings from NHTSA crash tests. Returns overall rating and individual test scores.",
                BinaryData.FromObjectAsJson(new
                {
                    type = "object",
                    properties = new
                    {
                        make = new { type = "string", description = "Vehicle manufacturer" },
                        model = new { type = "string", description = "Vehicle model name" },
                        year = new { type = "integer", description = "Model year" }
                    },
                    required = new[] { "make", "model", "year" }
                }))
        ];
    }
}

