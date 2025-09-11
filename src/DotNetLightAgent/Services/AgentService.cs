using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using DotNetLightAgent.Plugins;
using ModelContextProtocol.Client;
using DotNetLightAgent.Models;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace DotNetLightAgent.Services;

/// <summary>
/// Agent service implementation that handles AI chat interactions with Semantic Kernel
/// </summary>
public class AgentService : IAgentService, IDisposable
{
    private readonly ILogger<AgentService> _logger;
    private readonly ConcurrentDictionary<string, ChatHistory> _sessionHistories;
    private Kernel? _kernel;
    private IChatCompletionService? _chatCompletionService;
    private IMcpClient? _mcpClient;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initializationLock;

    public AgentService(ILogger<AgentService> logger)
    {
        _logger = logger;
        _sessionHistories = new ConcurrentDictionary<string, ChatHistory>();
        _initializationLock = new SemaphoreSlim(1, 1);
    }

    public async Task InitializeAsync()
    {
        await _initializationLock.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            _logger.LogInformation("Initializing Agent Service...");

            // Create kernel configuration
            var modelId = "copilot-claude-sonnet-4"; // or any other model you have installed in Ollama
            var endpoint = new Uri("http://localhost:3000/v1/"); // default Ollama endpoint

            var kernelBuilder = Kernel.CreateBuilder();
            
            // Configure HTTP client with timeout
            var httpClient = new HttpClient()
            {
                Timeout = TimeSpan.FromMinutes(5)
            };

            // Add OpenAI chat completion
            kernelBuilder.AddOpenAIChatCompletion(
                modelId: modelId,
                endpoint: endpoint,
                apiKey: "placeholder" // Some services still require a placeholder
            );

            // Build the kernel
            _kernel = kernelBuilder.Build();
            _chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

            // Add plugins
            _kernel.Plugins.AddFromType<LightsPlugin>("Lights");

            // Initialize MCP clients
            await InitializeMcpClientsAsync();

            _isInitialized = true;
            _logger.LogInformation("Agent Service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Agent Service");
            throw;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task InitializeMcpClientsAsync()
    {
        // Initialize MCP clients with error handling
        foreach (var stdmcp in MCPList.stdioClientTransportOptions)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                _mcpClient = await McpClientFactory.CreateAsync(
                    new StdioClientTransport(stdmcp),
                    new McpClientOptions(),
                    null,
                    cts.Token
                );

                // Retrieve available tools and expose as SK functions
                var tools = await _mcpClient.ListToolsAsync();
                _kernel!.Plugins.AddFromFunctions(stdmcp.Name, tools.Select(aiFunction => aiFunction.AsKernelFunction()));
                _logger.LogInformation("Successfully connected to MCP '{McpName}' with {ToolCount} tools available", 
                    stdmcp.Name, tools.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not connect to MCP '{McpName}' server. The application will continue without these tools", 
                    stdmcp.Name);
                _mcpClient = null;
            }
        }
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> SendMessageStreamAsync(
        string message, 
        string? sessionId = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
            await InitializeAsync();

        if (_kernel == null || _chatCompletionService == null)
            throw new InvalidOperationException("Agent service is not properly initialized");

        var effectiveSessionId = sessionId ?? "default";
        var history = GetOrCreateHistory(effectiveSessionId);

        // Add user message to history
        history.AddUserMessage(message);

        // Configure execution settings
        var executionSettings = new OllamaPromptExecutionSettings()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            ExtensionData = new Dictionary<string, object>
            {
                { "num_predict", 512 },
                { "num_ctx", 4096 }
            }
        };

        IAsyncEnumerable<StreamingChatMessageContent> streamingResponse;
        
        try
        {
            // Get the streaming response
            streamingResponse = _chatCompletionService.GetStreamingChatMessageContentsAsync(
                history,
                executionSettings: executionSettings,
                kernel: _kernel,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during streaming chat completion for session {SessionId}", effectiveSessionId);
            
            // Add error to history for context
            history.AddMessage(AuthorRole.System, $"Error occurred during interaction: {ex.Message}");
            throw;
        }

        // Stream the response
        await foreach (var chunk in streamingResponse.WithCancellation(cancellationToken))
        {
            // Add chunks to history for context
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                history.AddMessage(chunk.Role ?? AuthorRole.Assistant, chunk.Content);
            }

            yield return chunk;
        }
    }

    public async Task<string> SendMessageAsync(
        string message, 
        string? sessionId = null, 
        CancellationToken cancellationToken = default)
    {
        var fullResponse = "";
        
        await foreach (var chunk in SendMessageStreamAsync(message, sessionId, cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                fullResponse += chunk.Content;
            }
        }

        return fullResponse;
    }

    public void ClearHistory(string? sessionId = null)
    {
        var effectiveSessionId = sessionId ?? "default";
        
        if (_sessionHistories.TryRemove(effectiveSessionId, out _))
        {
            _logger.LogInformation("Cleared history for session {SessionId}", effectiveSessionId);
        }
    }

    public IReadOnlyList<ChatMessageContent> GetHistory(string? sessionId = null)
    {
        var effectiveSessionId = sessionId ?? "default";
        var history = GetOrCreateHistory(effectiveSessionId);
        return history.ToList().AsReadOnly();
    }

    private ChatHistory GetOrCreateHistory(string sessionId)
    {
        return _sessionHistories.GetOrAdd(sessionId, _ =>
        {
            var history = new ChatHistory();
            history.AddMessage(AuthorRole.System, @"You are a helpful AI assistant named Jira-Sic with access to various tools and capabilities including:
- Ability to call functions and perform actions based on user requests

IMPORTANT: When performing tasks, you must provide structured status updates to help users track your progress. Use the following format:

🙉{""type"":""status"",""content"":""Brief description of current action"",""status"":""in_progress""}🙊

Status Types:
- ""status"": For progress updates during task execution
- ""result"": For final results or completed actions
- ""error"": For error conditions or failures

Status Values:
- ""in_progress"": Currently working on a step
- ""success"": Step completed successfully  
- ""error"": Step failed or encountered an error
- ""complete"": Entire task finished

Examples:
🙉{""type"":""status"",""content"":""Connecting to Jira API"",""status"":""in_progress""}🙊
🙉{""type"":""status"",""content"":""Retrieved 5 issues from project"",""status"":""success""}🙊
🙉{""type"":""result"",""content"":""Task completed: Created issue PROJ-123"",""status"":""complete""}🙊
🙉{""type"":""error"",""content"":""Authentication failed"",""status"":""error""}🙊

Rules:
1. Always emit status messages when starting, during, and completing actions
2. Keep content concise but informative (max 100 characters)
3. Use exact JSON format between 🙉 and 🙊 emojis
4. These messages should be separate from your regular conversational responses
5. Emit status updates before and after tool calls or significant processing steps");
            return history;
        });
    }

    public void Dispose()
    {
        _mcpClient?.DisposeAsync().AsTask().Wait();
        _initializationLock?.Dispose();
    }
}
