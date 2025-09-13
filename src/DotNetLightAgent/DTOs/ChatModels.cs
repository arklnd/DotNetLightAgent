namespace DotNetLightAgent.DTOs;

/// <summary>
/// Request model for chat messages
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// The user message to send to the agent
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Optional session ID for conversation history
    /// </summary>
    public string? SessionId { get; set; }
}

/// <summary>
/// Response model for chat messages
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// The complete response from the agent
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Session ID used for this conversation
    /// </summary>
    public required string SessionId { get; set; }

    /// <summary>
    /// Timestamp of the response
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Streaming chunk response model
/// </summary>
public class ChatStreamChunk
{
    /// <summary>
    /// The content chunk from the streaming response
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// The role of the message sender
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Session ID for this conversation
    /// </summary>
    public required string SessionId { get; set; }

    /// <summary>
    /// Indicates if this is the final chunk
    /// </summary>
    public bool IsComplete { get; set; } = false;

    /// <summary>
    /// Timestamp of this chunk
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Request to clear conversation history
/// </summary>
public class ClearHistoryRequest
{
    /// <summary>
    /// Session ID to clear (optional, clears default if not provided)
    /// </summary>
    public string? SessionId { get; set; }
}

/// <summary>
/// Response for history operations
/// </summary>
public class HistoryResponse
{
    /// <summary>
    /// Session ID
    /// </summary>
    public required string SessionId { get; set; }

    /// <summary>
    /// List of conversation messages
    /// </summary>
    public required List<HistoryMessage> Messages { get; set; }
}

/// <summary>
/// Individual message in conversation history
/// </summary>
public class HistoryMessage
{
    /// <summary>
    /// Role of the message sender
    /// </summary>
    public required string Role { get; set; }

    /// <summary>
    /// Message content
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
