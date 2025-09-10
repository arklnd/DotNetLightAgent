using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using DotNetLightAgent.Services;
using DotNetLightAgent.DTOs;
using System.Text.Json;
using System.Text;

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

            await foreach (var chunk in _agentService.SendMessageStreamAsync(
                request.Message, 
                sessionId, 
                cancellationToken))
            {
                var streamChunk = new ChatStreamChunk
                {
                    Content = chunk.Content,
                    Role = chunk.Role?.Label ?? "unknown",
                    SessionId = sessionId,
                    IsComplete = false
                };

                var json = JsonSerializer.Serialize(streamChunk);
                var data = $"data: {json}\n\n";
                
                await Response.WriteAsync(data, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

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
            
            await Response.WriteAsync(completionData, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing streaming chat message");
            
            // Send error event
            var errorChunk = new ChatStreamChunk
            {
                Content = $"Error: {ex.Message}",
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
