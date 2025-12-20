using ClarissaBot.Core.Models;

namespace ClarissaBot.Core.Agent;

/// <summary>
/// Interface for the Clarissa AI agent.
/// </summary>
public interface IClarissaAgent
{
    /// <summary>
    /// Process a user message and return the agent's response.
    /// </summary>
    /// <param name="userMessage">The user's input message</param>
    /// <param name="conversationId">Optional conversation ID for context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The agent's response message</returns>
    Task<string> ChatAsync(string userMessage, string? conversationId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Process a user message and stream the response token by token.
    /// </summary>
    /// <param name="userMessage">The user's input message</param>
    /// <param name="conversationId">Optional conversation ID for context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An async enumerable of response tokens</returns>
    IAsyncEnumerable<string> ChatStreamAsync(string userMessage, string? conversationId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Process a user message and stream rich events including tool calls and vehicle context.
    /// </summary>
    /// <param name="userMessage">The user's input message</param>
    /// <param name="conversationId">Optional conversation ID for context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An async enumerable of streaming events</returns>
    IAsyncEnumerable<StreamingEvent> ChatStreamRichAsync(string userMessage, string? conversationId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear conversation history for a given conversation ID.
    /// </summary>
    /// <param name="conversationId">The conversation ID to clear</param>
    void ClearConversation(string? conversationId = null);
}

