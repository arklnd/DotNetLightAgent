using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using DotNetLightAgent.Services;
using DotNetLightAgent.DTOs;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DotNetLightAgent.Controllers;

/// <summary>
/// Controller for handling chat interactions with the AI agent
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IAgentService agentService, ILogger<ChatController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    /// <summary>
    /// Send a message to the agent and get a complete response
    /// </summary>
    /// <param name="request">Chat request containing the message and optional session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Complete chat response</returns>
    [HttpPost("message")]
    public async Task<ActionResult<ChatResponse>> SendMessage(
        [FromBody] ChatRequest request, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest("Message cannot be empty");
            }

            var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
            
            _logger.LogInformation("Processing chat message for session {SessionId}", sessionId);
            
            var response = await _agentService.SendMessageAsync(
                request.Message, 
                sessionId, 
                cancellationToken);

            return Ok(new ChatResponse
            {
                Message = response,
                SessionId = sessionId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, "An error occurred while processing your message");
        }
    }

    /// <summary>
    /// Send a message to the agent and stream the response
    /// </summary>
    /// <param name="request">Chat request containing the message and optional session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Server-sent events stream of chat response chunks</returns>
    [HttpPost("stream")]
    public async Task StreamMessage(
        [FromBody] ChatRequest request, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                Response.StatusCode = 400;
                await Response.WriteAsync("Message cannot be empty", cancellationToken);
                return;
            }

            var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
            
            _logger.LogInformation("Processing streaming chat message for session {SessionId}", sessionId);

            // Set up Server-Sent Events response
            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";
            Response.Headers["Access-Control-Allow-Origin"] = "*";

            var buffer = new StringBuilder();
            var statusMessagePattern = new Regex(@"🙉({.*?})🙊", RegexOptions.Compiled);

            _logger.LogDebug("Starting stream processing for session {SessionId}", sessionId);

            await foreach (var chunk in _agentService.SendMessageStreamAsync(
                request.Message, 
                sessionId, 
                cancellationToken))
            {
                if (string.IsNullOrEmpty(chunk.Content))
                {
                    _logger.LogDebug("Received empty chunk, skipping");
                    continue;
                }

                _logger.LogDebug("Received chunk: {ChunkContent}", chunk.Content);

                // Add chunk content to buffer
                buffer.Append(chunk.Content);
                var bufferContent = buffer.ToString();

                _logger.LogDebug("Current buffer content: {BufferContent}", bufferContent);

                // Process all complete status messages in the buffer
                var matches = statusMessagePattern.Matches(bufferContent);
                
                _logger.LogDebug("Found {MatchCount} status message matches", matches.Count);
                
                if (matches.Count > 0)
                {
                    // Process matches in reverse order to avoid index shifting issues
                    for (int i = matches.Count - 1; i >= 0; i--)
                    {
                        var match = matches[i];
                        try
                        {
                            // Extract and send only the JSON content from status messages
                            var statusJson = match.Groups[1].Value;
                            
                            _logger.LogDebug("Processing status message: {StatusJson}", statusJson);
                            
                            var streamChunk = new ChatStreamChunk
                            {
                                Content = statusJson,
                                Role = AuthorRole.Assistant.ToString(),
                                SessionId = sessionId,
                                IsComplete = false
                            };

                            var json = JsonSerializer.Serialize(streamChunk);
                            var data = $"data: {json}\n\n";
                            
                            _logger.LogDebug("Sending stream chunk: {Data}", data);
                            
                            await Response.WriteAsync(data, cancellationToken);
                            await Response.Body.FlushAsync(cancellationToken);
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse status message: {StatusJson}", match.Groups[1].Value);
                        }
                    }

                    // Remove all processed status messages from buffer
                    var cleanedContent = statusMessagePattern.Replace(bufferContent, "");
                    buffer.Clear();
                    buffer.Append(cleanedContent);
                    
                    _logger.LogDebug("Updated buffer after removing all processed messages: {UpdatedBuffer}", buffer.ToString());
                }
                
                // Only clear non-status content if buffer gets too large (prevent memory issues)
                // But preserve potential partial status messages
                if (buffer.Length > 10000 && !bufferContent.Contains("🙉"))
                {
                    _logger.LogDebug("Buffer too large and no partial status messages, clearing: {BufferContent}", buffer.ToString());
                    buffer.Clear();
                }
            }

            _logger.LogDebug("Stream processing completed, sending completion marker");

            // Send completion marker
            var completionChunk = new ChatStreamChunk
            {
                Content = null,
                Role = null,
                SessionId = sessionId,
                IsComplete = true
            };

            var completionJson = JsonSerializer.Serialize(completionChunk);
            var completionData = $"data: {completionJson}\n\n";
            
            _logger.LogDebug("Sending completion data: {CompletionData}", completionData);
            
            await Response.WriteAsync(completionData, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing streaming chat message");
            
            // Send error event
            var errorContent = JsonSerializer.Serialize(new 
            {
                type = "error",
                content = $"Error: {ex.Message}",
                status = "error"
            });

            var errorChunk = new ChatStreamChunk
            {
                Content = errorContent,
                Role = "error",
                SessionId = request.SessionId ?? "unknown",
                IsComplete = true
            };

            var errorJson = JsonSerializer.Serialize(errorChunk);
            var errorData = $"data: {errorJson}\n\n";
            
            await Response.WriteAsync(errorData, cancellationToken);
        }
    }

    /// <summary>
    /// Clear conversation history for a session
    /// </summary>
    /// <param name="request">Clear history request with optional session ID</param>
    /// <returns>Success message</returns>
    [HttpPost("clear")]
    public IActionResult ClearHistory([FromBody] ClearHistoryRequest request)
    {
        try
        {
            var sessionId = request.SessionId ?? "default";
            _agentService.ClearHistory(sessionId);
            
            _logger.LogInformation("Cleared history for session {SessionId}", sessionId);
            
            return Ok(new { Message = "History cleared successfully", SessionId = sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing chat history");
            return StatusCode(500, "An error occurred while clearing history");
        }
    }

    /// <summary>
    /// Get conversation history for a session
    /// </summary>
    /// <param name="sessionId">Optional session ID (defaults to "default")</param>
    /// <returns>Conversation history</returns>
    [HttpGet("history")]
    public ActionResult<HistoryResponse> GetHistory([FromQuery] string? sessionId = null)
    {
        try
        {
            var effectiveSessionId = sessionId ?? "default";
            var history = _agentService.GetHistory(effectiveSessionId);
            
            var historyMessages = history.Select(msg => new HistoryMessage
            {
                Role = msg.Role.Label ?? "unknown",
                Content = msg.Content ?? "",
                Timestamp = DateTime.UtcNow // Note: We don't have timestamp in ChatMessageContent
            }).ToList();

            return Ok(new HistoryResponse
            {
                SessionId = effectiveSessionId,
                Messages = historyMessages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chat history");
            return StatusCode(500, "An error occurred while retrieving history");
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    /// <returns>Service status</returns>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
    }
}
