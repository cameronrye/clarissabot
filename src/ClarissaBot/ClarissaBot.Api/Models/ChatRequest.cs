namespace ClarissaBot.Api.Models;

/// <summary>
/// Request model for the chat endpoint.
/// </summary>
public record ChatRequest
{
    /// <summary>
    /// The user's message text.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Optional conversation ID for multi-turn conversations.
    /// If not provided, a new conversation will be created.
    /// </summary>
    public string? ConversationId { get; init; }
}

