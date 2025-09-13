using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Runtime.CompilerServices;

namespace DotNetLightAgent.Services;

/// <summary>
/// Interface for the AI agent service that handles chat interactions
/// </summary>
public interface IAgentService
{
    /// <summary>
    /// Initializes the agent service asynchronously
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Sends a message to the agent and streams the response
    /// </summary>
    /// <param name="message">The user message</param>
    /// <param name="sessionId">Optional session identifier for conversation history</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of streaming chat message contents</returns>
    IAsyncEnumerable<StreamingChatMessageContent> SendMessageStreamAsync(
        string message, 
        string? sessionId = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to the agent and returns the complete response
    /// </summary>
    /// <param name="message">The user message</param>
    /// <param name="sessionId">Optional session identifier for conversation history</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The complete chat response</returns>
    Task<string> SendMessageAsync(
        string message, 
        string? sessionId = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the conversation history for a session
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    void ClearHistory(string? sessionId = null);

    /// <summary>
    /// Gets the conversation history for a session
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <returns>List of chat history messages</returns>
    IReadOnlyList<ChatMessageContent> GetHistory(string? sessionId = null);
}
