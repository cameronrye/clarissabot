namespace ClarissaBot.Core.Agent;

using System.ClientModel;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using ClarissaBot.Core.Models;
using ClarissaBot.Core.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

/// <summary>
/// Represents a vehicle context being discussed in a conversation.
/// </summary>
public sealed record VehicleContext(int Year, string Make, string Model)
{
    /// <summary>
    /// Creates a unique key for this vehicle (case-insensitive).
    /// </summary>
    public string Key => $"{Year}|{Make.ToUpperInvariant()}|{Model.ToUpperInvariant()}";

    public override string ToString() => $"{Year} {Make} {Model}";
}

/// <summary>
/// Tracks multiple vehicles discussed in a conversation with access timestamps.
/// Supports follow-up questions referencing previously discussed vehicles.
/// </summary>
public sealed class VehicleContextHistory
{
    private readonly List<(VehicleContext Vehicle, DateTime AccessedUtc)> _vehicles = [];
    private const int MaxVehicles = 10;

    /// <summary>
    /// Gets the most recently discussed vehicle, or null if none.
    /// </summary>
    public VehicleContext? Current => _vehicles.Count > 0 ? _vehicles[^1].Vehicle : null;

    /// <summary>
    /// Gets all vehicles discussed in this conversation, most recent first.
    /// </summary>
    public IReadOnlyList<VehicleContext> All =>
        _vehicles.OrderByDescending(v => v.AccessedUtc).Select(v => v.Vehicle).ToList();

    /// <summary>
    /// Gets the count of vehicles tracked.
    /// </summary>
    public int Count => _vehicles.Count;

    /// <summary>
    /// Adds or updates a vehicle in the history. If the vehicle already exists,
    /// it updates the access time. Returns true if the vehicle was newly added.
    /// </summary>
    public bool AddOrUpdate(VehicleContext vehicle)
    {
        var existingIndex = _vehicles.FindIndex(v => v.Vehicle.Key == vehicle.Key);

        if (existingIndex >= 0)
        {
            // Vehicle exists - update access time and move to end (most recent)
            _vehicles.RemoveAt(existingIndex);
            _vehicles.Add((vehicle, DateTime.UtcNow));
            return false;
        }

        // New vehicle - add to history
        _vehicles.Add((vehicle, DateTime.UtcNow));

        // Trim oldest vehicles if we exceed the limit
        while (_vehicles.Count > MaxVehicles)
        {
            _vehicles.RemoveAt(0);
        }

        return true;
    }

    /// <summary>
    /// Finds a vehicle by make/model (partial match, case-insensitive).
    /// Useful for follow-up questions like "what about the Ford?"
    /// </summary>
    public VehicleContext? FindByMake(string make)
    {
        return _vehicles
            .OrderByDescending(v => v.AccessedUtc)
            .Select(v => v.Vehicle)
            .FirstOrDefault(v => v.Make.Equals(make, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Finds a vehicle by model name (partial match, case-insensitive).
    /// </summary>
    public VehicleContext? FindByModel(string model)
    {
        var normalized = ModelNameNormalizer.Normalize(model);
        return _vehicles
            .OrderByDescending(v => v.AccessedUtc)
            .Select(v => v.Vehicle)
            .FirstOrDefault(v => ModelNameNormalizer.Normalize(v.Model)
                .Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Normalizes vehicle model names to handle variations like "F-150", "F150", "F 150".
/// </summary>
public static class ModelNameNormalizer
{
    /// <summary>
    /// Common model name mappings (normalized form -> variations).
    /// </summary>
    private static readonly Dictionary<string, string[]> ModelMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Ford trucks
        ["F-150"] = ["F150", "F 150", "F-150", "F150 LIGHTNING", "F-150 LIGHTNING"],
        ["F-250"] = ["F250", "F 250", "F-250"],
        ["F-350"] = ["F350", "F 350", "F-350"],
        ["F-450"] = ["F450", "F 450", "F-450"],

        // Chevy trucks
        ["SILVERADO 1500"] = ["SILVERADO1500", "SILVERADO-1500"],
        ["SILVERADO 2500"] = ["SILVERADO2500", "SILVERADO-2500"],
        ["SILVERADO 3500"] = ["SILVERADO3500", "SILVERADO-3500"],

        // RAM trucks
        ["RAM 1500"] = ["RAM1500", "RAM-1500", "1500"],
        ["RAM 2500"] = ["RAM2500", "RAM-2500", "2500"],
        ["RAM 3500"] = ["RAM3500", "RAM-3500", "3500"],

        // Toyota models
        ["RAV4"] = ["RAV 4", "RAV-4"],
        ["4RUNNER"] = ["4-RUNNER", "4 RUNNER", "FOURRUNNER"],
        ["GR86"] = ["GR 86", "GR-86"],

        // BMW series
        ["3 SERIES"] = ["3-SERIES", "3SERIES"],
        ["5 SERIES"] = ["5-SERIES", "5SERIES"],
        ["X3"] = ["X-3"],
        ["X5"] = ["X-5"],

        // Mercedes
        ["C-CLASS"] = ["C CLASS", "CCLASS"],
        ["E-CLASS"] = ["E CLASS", "ECLASS"],
        ["GLE"] = ["GLE-CLASS", "GLE CLASS"],

        // Common electric vehicles
        ["MODEL 3"] = ["MODEL3", "MODEL-3"],
        ["MODEL S"] = ["MODELS", "MODEL-S"],
        ["MODEL X"] = ["MODELX", "MODEL-X"],
        ["MODEL Y"] = ["MODELY", "MODEL-Y"],
        ["MACH-E"] = ["MACH E", "MACHE", "MUSTANG MACH-E", "MUSTANG MACH E"],

        // Honda/Acura
        ["CR-V"] = ["CRV", "CR V"],
        ["HR-V"] = ["HRV", "HR V"],
        ["PILOT"] = [],
        ["ACCORD"] = [],
    };

    private static readonly Dictionary<string, string> ReverseMappings;

    static ModelNameNormalizer()
    {
        // Build reverse lookup for fast normalization
        ReverseMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (canonical, variations) in ModelMappings)
        {
            ReverseMappings[canonical] = canonical;
            foreach (var variation in variations)
            {
                ReverseMappings[variation] = canonical;
            }
        }
    }

    /// <summary>
    /// Normalizes a model name to its canonical form.
    /// Returns the original if no mapping exists.
    /// </summary>
    public static string Normalize(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return model;

        var trimmed = model.Trim().ToUpperInvariant();

        // Check for exact match in reverse mappings
        if (ReverseMappings.TryGetValue(trimmed, out var canonical))
            return canonical;

        // No mapping found - return uppercase version for consistency
        return trimmed;
    }
}

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
    private readonly ConcurrentDictionary<string, DateTime> _evictedConversations = new();
    private readonly List<ChatTool> _tools;
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _maxIdleTime;
    private readonly int _maxConversations;
    private readonly int _maxMessagesPerConversation;
    private bool _disposed;

    private const int MaxRetries = 3;
    private const int MaxToolIterations = 10;
    private const int MaxEvictedTracking = 1000;

    /// <summary>
    /// Tracks a conversation, its last access time, and vehicle context.
    /// </summary>
    private sealed class ConversationEntry
    {
        public List<ChatMessage> Messages { get; }
        public DateTime LastAccessedUtc { get; set; }

        /// <summary>
        /// Tracks all vehicles discussed in this conversation.
        /// </summary>
        public VehicleContextHistory VehicleHistory { get; } = new();

        /// <summary>
        /// Gets the current (most recent) vehicle being discussed.
        /// </summary>
        public VehicleContext? CurrentVehicle => VehicleHistory.Current;

        /// <summary>
        /// The type of the last query (recalls, complaints, safety_rating).
        /// </summary>
        public string? LastQueryType { get; set; }

        /// <summary>
        /// Whether this is a recovered session after eviction.
        /// </summary>
        public bool WasEvicted { get; set; }

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
        _maxMessagesPerConversation = configuration?.GetValue("Conversations:MaxMessagesPerConversation", 100) ?? 100;
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
        var (entry, wasEvicted) = GetOrCreateConversationWithEvictionCheck(conversationId);
        var messages = entry.Messages;

        // Check for eviction and notify user
        string? evictionNotice = null;
        if (wasEvicted)
        {
            evictionNotice = "⚠️ Your previous session has expired. Starting a fresh conversation. ";
            _logger.LogInformation("[Session Expired] Conversation {ConversationId} was evicted", conversationId);
        }

        // Debug logging for conversation context
        _logger.LogInformation(
            "[Context Debug] ConversationId: {ConversationId}, MessageCount: {Count}, CurrentVehicle: {Vehicle}, VehicleCount: {VehicleCount}, LastQuery: {QueryType}",
            conversationId, messages.Count, entry.CurrentVehicle?.ToString() ?? "None",
            entry.VehicleHistory.Count, entry.LastQueryType ?? "None");

        // Trim messages if approaching limit
        TrimMessagesIfNeeded(entry);

        // Inject vehicle context if we have one
        InjectVehicleContext(entry, messages);

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
                await ProcessToolCallsAsync(entry, messages, completion, cancellationToken);
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

                // Prepend eviction notice if session was recovered
                var finalResponse = evictionNotice != null ? evictionNotice + responseText : responseText;

                messages.Add(new AssistantChatMessage(responseText));
                _logger.LogDebug("Assistant: {Response}", TruncateForLog(responseText));
                return finalResponse;
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
        var (entry, wasEvicted) = GetOrCreateConversationWithEvictionCheck(conversationId);
        var messages = entry.Messages;

        // Send eviction notice first if session was recovered
        if (wasEvicted)
        {
            var notice = "⚠️ Your previous session has expired. Starting a fresh conversation.\n\n";
            yield return notice;
            _logger.LogInformation("[Session Expired] Conversation {ConversationId} was evicted", conversationId);
        }

        // Debug logging for conversation context
        _logger.LogInformation(
            "[Context Debug] ConversationId: {ConversationId}, MessageCount: {Count}, CurrentVehicle: {Vehicle}, VehicleCount: {VehicleCount}, LastQuery: {QueryType}",
            conversationId, messages.Count, entry.CurrentVehicle?.ToString() ?? "None",
            entry.VehicleHistory.Count, entry.LastQueryType ?? "None");

        // Trim messages if approaching limit
        TrimMessagesIfNeeded(entry);

        // Inject vehicle context if we have one
        InjectVehicleContext(entry, messages);

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
                    // Extract and store vehicle context from tool call
                    ExtractVehicleContext(entry, toolCall);
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

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamingEvent> ChatStreamRichAsync(
        string userMessage,
        string? conversationId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        conversationId ??= "default";
        var (entry, wasEvicted) = GetOrCreateConversationWithEvictionCheck(conversationId);
        var messages = entry.Messages;

        // Send eviction notice first if session was recovered
        if (wasEvicted)
        {
            yield return new ContentChunkEvent("⚠️ Your previous session has expired. Starting a fresh conversation.\n\n");
            _logger.LogInformation("[Session Expired] Conversation {ConversationId} was evicted", conversationId);
        }

        // Emit current vehicle context if we have one
        if (entry.CurrentVehicle is { } vehicle)
        {
            yield return new VehicleContextEvent(vehicle.Year, vehicle.Make, vehicle.Model);
        }

        // Debug logging
        _logger.LogInformation(
            "[Context Debug] ConversationId: {ConversationId}, MessageCount: {Count}, CurrentVehicle: {Vehicle}, VehicleCount: {VehicleCount}",
            conversationId, messages.Count, entry.CurrentVehicle?.ToString() ?? "None", entry.VehicleHistory.Count);

        // Trim messages if approaching limit
        TrimMessagesIfNeeded(entry);

        // Inject vehicle context if we have one
        InjectVehicleContext(entry, messages);

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
                        yield return new ContentChunkEvent(contentPart.Text);
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
                    // Extract vehicle info from args for display
                    var vehicleInfo = ExtractVehicleInfoForDisplay(toolCall);

                    // Emit tool call event
                    yield return new ToolCallEvent(toolCall.FunctionName, vehicleInfo);

                    _logger.LogDebug("Tool call: {Name}({Args})", toolCall.FunctionName, TruncateForLog(toolCall.FunctionArguments.ToString()));

                    // Extract and store vehicle context from tool call
                    ExtractVehicleContext(entry, toolCall);
                    var result = await ExecuteToolCallAsync(toolCall, cancellationToken);
                    _logger.LogDebug("Tool result: {Result}", TruncateForLog(result));

                    // For VIN decode, extract vehicle context from the result
                    if (toolCall.FunctionName == "decode_vin")
                    {
                        ExtractVehicleContextFromVinResult(entry, result);
                    }

                    // Emit tool result event
                    var success = !result.Contains("\"error\"");
                    yield return new ToolResultEvent(toolCall.FunctionName, success);

                    // Emit updated vehicle context if changed
                    if (entry.CurrentVehicle is { } updatedVehicle)
                    {
                        yield return new VehicleContextEvent(updatedVehicle.Year, updatedVehicle.Make, updatedVehicle.Model);
                    }

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

    /// <summary>
    /// Extracts vehicle info from tool call arguments for display purposes.
    /// </summary>
    private static string? ExtractVehicleInfoForDisplay(ChatToolCall toolCall)
    {
        try
        {
            if (toolCall.FunctionName == "decode_vin")
            {
                using var args = JsonDocument.Parse(toolCall.FunctionArguments);
                if (args.RootElement.TryGetProperty("vin", out var vinProp))
                {
                    return $"VIN: {vinProp.GetString()}";
                }
                return null;
            }

            using var doc = JsonDocument.Parse(toolCall.FunctionArguments);
            var root = doc.RootElement;

            if (root.TryGetProperty("year", out var yearProp) &&
                root.TryGetProperty("make", out var makeProp) &&
                root.TryGetProperty("model", out var modelProp))
            {
                return $"{yearProp.GetInt32()} {makeProp.GetString()} {modelProp.GetString()}";
            }
        }
        catch
        {
            // Ignore parse errors
        }
        return null;
    }

    /// <summary>
    /// Gets or creates a conversation entry, also checking if it was previously evicted.
    /// Returns a tuple of (entry, wasEvicted).
    /// </summary>
    private (ConversationEntry Entry, bool WasEvicted) GetOrCreateConversationWithEvictionCheck(string conversationId)
    {
        var wasEvicted = false;

        // Check if this conversation was recently evicted
        if (_evictedConversations.TryRemove(conversationId, out var evictedTime))
        {
            wasEvicted = true;
            _logger.LogDebug("Conversation {ConversationId} was evicted at {EvictedTime}, creating new session",
                conversationId, evictedTime);
        }

        var entry = _conversations.GetOrAdd(conversationId, _ =>
            new ConversationEntry([new SystemChatMessage(AgentInstructions.GetInstructions())]));

        entry.LastAccessedUtc = DateTime.UtcNow;

        // Evict oldest conversations if we exceed the limit
        if (_conversations.Count > _maxConversations)
        {
            EvictOldestConversations();
        }

        return (entry, wasEvicted);
    }

    /// <summary>
    /// Trims old messages from a conversation to prevent context window overflow.
    /// Keeps the system prompt, vehicle context, and recent messages.
    /// </summary>
    private void TrimMessagesIfNeeded(ConversationEntry entry)
    {
        var messages = entry.Messages;
        if (messages.Count <= _maxMessagesPerConversation)
            return;

        // Calculate how many messages to remove (keep 75% of max)
        var targetCount = (int)(_maxMessagesPerConversation * 0.75);
        var removeCount = messages.Count - targetCount;

        if (removeCount <= 0)
            return;

        // Find first non-system message index
        var firstUserMessageIndex = 0;
        for (var i = 0; i < messages.Count; i++)
        {
            if (messages[i] is not SystemChatMessage)
            {
                firstUserMessageIndex = i;
                break;
            }
        }

        // Remove oldest non-system messages
        var removedCount = 0;
        for (var i = firstUserMessageIndex; i < messages.Count && removedCount < removeCount; )
        {
            // Don't remove tool messages without their corresponding assistant message
            if (messages[i] is ToolChatMessage)
            {
                i++;
                continue;
            }

            messages.RemoveAt(i);
            removedCount++;
        }

        _logger.LogInformation(
            "[Message Trim] Removed {Count} old messages from conversation, {Remaining} remaining",
            removedCount, messages.Count);
    }

    /// <summary>
    /// Removes expired conversations that have been idle too long.
    /// Tracks evicted conversations for user notification.
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
                // Track eviction for user notification
                TrackEvictedConversation(key);
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
    /// Tracks evicted conversations for user notification.
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
            if (_conversations.TryRemove(key, out _))
            {
                TrackEvictedConversation(key);
            }
        }

        _logger.LogInformation("Evicted {Count} oldest conversations due to capacity limit", oldestKeys.Count);
    }

    /// <summary>
    /// Tracks an evicted conversation ID for later notification.
    /// </summary>
    private void TrackEvictedConversation(string conversationId)
    {
        _evictedConversations[conversationId] = DateTime.UtcNow;

        // Cleanup old eviction records to prevent memory leak
        if (_evictedConversations.Count > MaxEvictedTracking)
        {
            var oldestEvictions = _evictedConversations
                .OrderBy(kvp => kvp.Value)
                .Take(_evictedConversations.Count - MaxEvictedTracking + 100)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in oldestEvictions)
            {
                _evictedConversations.TryRemove(key, out _);
            }
        }
    }

    /// <summary>
    /// Disposes resources used by the agent.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _cleanupTimer.Dispose();
        _conversations.Clear();
        _evictedConversations.Clear();
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
        ConversationEntry entry,
        List<ChatMessage> messages,
        ChatCompletion completion,
        CancellationToken cancellationToken)
    {
        messages.Add(new AssistantChatMessage(completion));

        foreach (var toolCall in completion.ToolCalls)
        {
            _logger.LogDebug("Tool call: {Name}({Args})", toolCall.FunctionName, TruncateForLog(toolCall.FunctionArguments.ToString()));
            // Extract and store vehicle context from tool call arguments
            ExtractVehicleContext(entry, toolCall);
            var result = await ExecuteToolCallAsync(toolCall, cancellationToken);
            _logger.LogDebug("Tool result: {Result}", TruncateForLog(result));

            // For VIN decode, extract vehicle context from the result
            if (toolCall.FunctionName == "decode_vin")
            {
                ExtractVehicleContextFromVinResult(entry, result);
            }

            messages.Add(new ToolChatMessage(toolCall.Id, result));
        }
    }

    /// <summary>
    /// Extracts vehicle context from a VIN decode result and adds it to the conversation history.
    /// This enables follow-up queries like "any recalls?" after decoding a VIN.
    /// </summary>
    private void ExtractVehicleContextFromVinResult(ConversationEntry entry, string resultJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;

            // Only process successful decodes
            if (!root.TryGetProperty("found", out var foundProp) || !foundProp.GetBoolean())
                return;

            if (root.TryGetProperty("year", out var yearProp) &&
                root.TryGetProperty("make", out var makeProp) &&
                root.TryGetProperty("model", out var modelProp))
            {
                var year = yearProp.GetInt32();
                var make = makeProp.GetString();
                var model = modelProp.GetString();

                if (year > 0 && !string.IsNullOrEmpty(make) && !string.IsNullOrEmpty(model))
                {
                    var normalizedModel = ModelNameNormalizer.Normalize(model);
                    var vehicle = new VehicleContext(year, make, normalizedModel);

                    var isNew = entry.VehicleHistory.AddOrUpdate(vehicle);

                    _logger.LogInformation(
                        "[VIN Decode Context] {Status} vehicle from VIN: {Vehicle}",
                        isNew ? "Added" : "Updated", vehicle);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to extract vehicle context from VIN decode result");
        }
    }

    /// <summary>
    /// Injects the current vehicle context into the message list if available.
    /// This helps the LLM maintain context for follow-up questions.
    /// Includes all discussed vehicles if multiple have been mentioned.
    /// </summary>
    private void InjectVehicleContext(ConversationEntry entry, List<ChatMessage> messages)
    {
        if (entry.VehicleHistory.Count == 0) return;

        var contextBuilder = new StringBuilder("[CONTEXT: ");

        if (entry.VehicleHistory.Count == 1)
        {
            contextBuilder.Append($"The user is currently discussing the {entry.CurrentVehicle}. ");
            contextBuilder.Append("Use this vehicle for any follow-up questions about recalls, complaints, or safety ratings ");
            contextBuilder.Append("unless the user explicitly mentions a different vehicle.");
        }
        else
        {
            // Multiple vehicles discussed
            contextBuilder.Append($"The user's most recent vehicle is the {entry.CurrentVehicle}. ");
            contextBuilder.Append("Previously discussed vehicles: ");

            var previousVehicles = entry.VehicleHistory.All.Skip(1).Take(5);
            contextBuilder.Append(string.Join(", ", previousVehicles.Select(v => v.ToString())));
            contextBuilder.Append(". ");

            contextBuilder.Append("Use the most recent vehicle for follow-up questions unless the user ");
            contextBuilder.Append("mentions a specific vehicle by name (e.g., 'the Ford' or 'the Camry').");
        }

        contextBuilder.Append(']');
        var contextMessage = contextBuilder.ToString();

        // Add as a system message to reinforce context
        // We insert it after the main system prompt but before user messages
        // Find the last system message index
        var lastSystemIndex = -1;
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i] is SystemChatMessage)
            {
                lastSystemIndex = i;
                break;
            }
        }

        // Remove any previous context injection to avoid duplicates
        messages.RemoveAll(m => m is SystemChatMessage scm &&
            scm.Content.Any(c => c.Text?.StartsWith("[CONTEXT:") == true));

        // Insert the new context after the main system prompt
        var insertIndex = lastSystemIndex >= 0 ? lastSystemIndex + 1 : 0;
        messages.Insert(insertIndex, new SystemChatMessage(contextMessage));

        _logger.LogDebug("[Context Injected] {Context}", contextMessage);
    }

    /// <summary>
    /// Extracts vehicle context from a tool call and stores it in the conversation entry.
    /// Supports extracting from VIN decode results as well as standard queries.
    /// </summary>
    private void ExtractVehicleContext(ConversationEntry entry, ChatToolCall toolCall)
    {
        try
        {
            // Extract from all vehicle-related tools including VIN decode and investigations
            if (toolCall.FunctionName is not ("check_recalls" or "get_complaints" or "get_safety_rating" or "decode_vin" or "check_investigations"))
                return;

            using var args = JsonDocument.Parse(toolCall.FunctionArguments);
            var root = args.RootElement;

            // For VIN decode, we don't have vehicle info in the arguments - it comes from the result
            // The context will be updated when the LLM processes the VIN decode result
            if (toolCall.FunctionName == "decode_vin")
            {
                // Mark that we're in a VIN decode context - the actual vehicle will be extracted
                // from the tool result by the LLM and used in subsequent calls
                entry.LastQueryType = toolCall.FunctionName;
                return;
            }

            if (root.TryGetProperty("year", out var yearProp) &&
                root.TryGetProperty("make", out var makeProp) &&
                root.TryGetProperty("model", out var modelProp))
            {
                var year = yearProp.GetInt32();
                var make = makeProp.GetString();
                var model = modelProp.GetString();

                if (!string.IsNullOrEmpty(make) && !string.IsNullOrEmpty(model))
                {
                    // Normalize the model name for consistent matching
                    var normalizedModel = ModelNameNormalizer.Normalize(model);
                    var newVehicle = new VehicleContext(year, make, normalizedModel);

                    // Add to vehicle history (handles duplicates internally)
                    var isNew = entry.VehicleHistory.AddOrUpdate(newVehicle);

                    if (isNew)
                    {
                        _logger.LogInformation(
                            "[Context Update] New vehicle added: '{NewVehicle}' (Total: {Count})",
                            newVehicle, entry.VehicleHistory.Count);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "[Context Update] Vehicle accessed: '{Vehicle}'",
                            newVehicle);
                    }

                    entry.LastQueryType = toolCall.FunctionName;
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to extract vehicle context from tool call");
        }
    }

    private async Task<string> ExecuteToolCallAsync(ChatToolCall toolCall, CancellationToken cancellationToken)
    {
        try
        {
            using var args = JsonDocument.Parse(toolCall.FunctionArguments);
            var root = args.RootElement;

            // For vehicle-related calls, normalize the model name
            string GetNormalizedModel() => ModelNameNormalizer.Normalize(root.GetProperty("model").GetString()!);

            return toolCall.FunctionName switch
            {
                "check_recalls" => await _nhtsaTools.CheckRecallsAsync(
                    root.GetProperty("make").GetString()!,
                    GetNormalizedModel(),
                    root.GetProperty("year").GetInt32()),
                "get_complaints" => await _nhtsaTools.GetComplaintsAsync(
                    root.GetProperty("make").GetString()!,
                    GetNormalizedModel(),
                    root.GetProperty("year").GetInt32()),
                "get_safety_rating" => await _nhtsaTools.GetSafetyRatingAsync(
                    root.GetProperty("make").GetString()!,
                    GetNormalizedModel(),
                    root.GetProperty("year").GetInt32()),
                "decode_vin" => await _nhtsaTools.DecodeVinAsync(
                    root.GetProperty("vin").GetString()!),
                "check_investigations" => await _nhtsaTools.CheckInvestigationsAsync(
                    root.GetProperty("make").GetString()!,
                    GetNormalizedModel(),
                    root.GetProperty("year").GetInt32()),
                _ => JsonSerializer.Serialize(new { error = $"Unknown function: {toolCall.FunctionName}" })
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error executing tool {Name}", toolCall.FunctionName);
            return JsonSerializer.Serialize(new
            {
                error = "network_error",
                message = $"Unable to connect to NHTSA: {ex.Message}",
                retryable = true,
                suggestion = "The NHTSA service may be temporarily unavailable. Please try again in a moment."
            });
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
        {
            _logger.LogWarning(ex, "Timeout executing tool {Name}", toolCall.FunctionName);
            return JsonSerializer.Serialize(new
            {
                error = "timeout",
                message = "The request to NHTSA timed out.",
                retryable = true,
                suggestion = "The service is slow to respond. Please try again or try a simpler query."
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid arguments for tool {Name}", toolCall.FunctionName);
            return JsonSerializer.Serialize(new
            {
                error = "invalid_arguments",
                message = $"Invalid arguments: {ex.Message}",
                retryable = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {Name}", toolCall.FunctionName);
            return JsonSerializer.Serialize(new
            {
                error = "internal_error",
                message = ex.Message,
                retryable = true,
                suggestion = "An unexpected error occurred. Please try again."
            });
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
                })),
            ChatTool.CreateFunctionTool(
                "decode_vin",
                "Decode a VIN (Vehicle Identification Number) to get vehicle details including year, make, model, and specifications.",
                BinaryData.FromObjectAsJson(new
                {
                    type = "object",
                    properties = new
                    {
                        vin = new { type = "string", description = "17-character Vehicle Identification Number" }
                    },
                    required = new[] { "vin" }
                })),
            ChatTool.CreateFunctionTool(
                "check_investigations",
                "Check for active NHTSA defect investigations on a vehicle. These are ongoing safety investigations that may lead to recalls.",
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

