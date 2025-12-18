namespace ClarissaBot.Api.Models;

/// <summary>
/// Response model for non-streaming chat responses.
/// </summary>
public record ChatResponse
{
    /// <summary>
    /// The assistant's response message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The conversation ID for subsequent messages.
    /// </summary>
    public required string ConversationId { get; init; }
}

