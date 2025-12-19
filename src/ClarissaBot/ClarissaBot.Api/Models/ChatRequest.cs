namespace ClarissaBot.Api.Models;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Request model for the chat endpoint.
/// </summary>
public record ChatRequest
{
    /// <summary>
    /// The user's message text.
    /// </summary>
    [Required]
    [StringLength(10000, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 10000 characters.")]
    public required string Message { get; init; }

    /// <summary>
    /// Optional conversation ID for multi-turn conversations.
    /// If not provided, a new conversation will be created.
    /// </summary>
    [StringLength(100, ErrorMessage = "ConversationId must not exceed 100 characters.")]
    public string? ConversationId { get; init; }
}

