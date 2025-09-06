// Import packages
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using DotNetLightAgent.Plugins;
using ModelContextProtocol.Client;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.AzureAIInference;
using DotNetLightAgent.Models;
using Azure;

// Populate values for your Ollama deployment
var modelId = "copilot-gpt-5"; // or any other model you have installed in Ollama
var endpoint = new Uri("http://localhost:3000/v1/"); // default Ollama endpoint

// Create a kernel with Ollama chat completion
var kernelBuilder = Kernel.CreateBuilder();
// Add timeout configuration and better error handling
var httpClient = new HttpClient()
{
    Timeout = TimeSpan.FromMinutes(5) // Increase timeout
};

// Try OpenAI connector with custom HttpClient
kernelBuilder.AddOpenAIChatCompletion(
    modelId: modelId, 
    endpoint: endpoint, 
    apiKey: "placeholder" // Some services still require a placeholder
    // httpClient: httpClient
);
// Add enterprise components
kernelBuilder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Information));

// Build the kernel
Kernel kernel = kernelBuilder.Build();
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

// Add a plugin (the LightsPlugin class is defined below)
kernel.Plugins.AddFromType<LightsPlugin>("Lights");

IMcpClient? mcpClient = null;
// Stdio based MCP with error handling
foreach (var stdmcp in MCPList.stdioClientTransportOptions)
{
    try
    {
        Console.Write($"[⚪] Attempting to connect to MCP: <{stdmcp.Name}> ..");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        mcpClient = await McpClientFactory.CreateAsync(
            new StdioClientTransport(stdmcp),
            new McpClientOptions(),
            null,
            cts.Token
        );

        // Retrieve available tools and expose as SK functions
        // IList<McpClientTool> tools = await mcpClient.ListToolsAsync();
        var tools = await mcpClient.ListToolsAsync();
        kernel.Plugins.AddFromFunctions(stdmcp.Name, tools.Select(aiFunction => aiFunction.AsKernelFunction()));
        Console.WriteLine($"\r[🟢] Successfully connected to <{stdmcp.Name}> with {tools.Count} tools available.");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\r[🟡] Could not connect to MCP <{stdmcp.Name}> server: {ex.Message}");
        Console.WriteLine($"The application will continue without MCP <{stdmcp.Name}> tools.");
        Console.ResetColor();
        mcpClient = null;
    }
}


// Alternative: HTTP-based MCP server
// var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5249/sse") };
// var serverConfig = new McpServerConfig
// {
//     Id = "SampleServer",
//     Name = "MyCustomMcpServer",
//     Location = httpClient.BaseAddress.ToString(),
//     TransportType = TransportTypes.Sse
// };
// var mcpClient = await McpClientFactory.CreateAsync(serverConfig, new McpClientOptions());

// Enable planning
PromptExecutionSettings promptExecutionSettings = new()
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

OllamaPromptExecutionSettings ollamaPromptExecutionSettings = new()
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
    // Temperature = 0.6f,
    // TopP = (float?)0.95,
    // TopK = 40,
    // MaxToken = 4096, //No such property exists
    ExtensionData = new Dictionary<string, object>
    {
        { "num_predict", 512 },
        { "num_ctx", 4096 }
    }
};
// Create a history store the conversation
var history = new ChatHistory();
history.AddMessage(AuthorRole.System, @"You are a helpful AI assistant named Jira-Sic with access to various tools and capabilities including:
- Ability to call functions and perform actions based on user requests");

// Initiate a back-and-forth chat
string? userInput;
do
{
    // Collect user input
    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("[🙂] > ");
    Console.ResetColor();
    userInput = Console.ReadLine();

    // Clear chat history
    if (string.Equals(userInput?.Trim(), "/clear", StringComparison.OrdinalIgnoreCase))
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("🧹 Chat history cleared. Starting fresh conversation...\n");
        Console.ResetColor();
        
        // Reset history but keep system message
        history.Clear();
        history.AddMessage(AuthorRole.System, @"You are a helpful AI assistant named Jira-Sic with access to various tools and capabilities including:
- Ability to call functions and perform actions based on user requests");
        continue;
    }

    if (string.Equals(userInput?.Trim(), "/history", StringComparison.OrdinalIgnoreCase))
    {
        DisplayConversationHistory(history);
        continue;
    }

    // Add user input
    if (!string.IsNullOrWhiteSpace(userInput))
    {
        history.AddUserMessage(userInput);

        // Print the AI response header
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write("[🤖] > ");
        Console.ResetColor();

        try
        {
            // Get the streaming response from the AI
            string fullResponse = "";
            await foreach (var chunk in chatCompletionService.GetStreamingChatMessageContentsAsync(
                history,
                executionSettings: ollamaPromptExecutionSettings,
                kernel: kernel))
            {
                if (chunk.Role == AuthorRole.Tool)
                {
                    // This is a tool response
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write("[🔧] ");
                    Console.ResetColor();
                    Console.WriteLine(chunk.Content);
                    history.AddMessage(AuthorRole.Tool, chunk.Content ?? "");
                }
                else if (chunk.Role == AuthorRole.Assistant && !string.IsNullOrEmpty(chunk.Content))
                {
                    // Regular assistant response
                    Console.Write(chunk.Content);
                    fullResponse += chunk.Content;
                    history.AddMessage(AuthorRole.Assistant, chunk.Content ?? "");
                }
                else if (!string.IsNullOrEmpty(chunk.Content))
                {
                    // Fallback for any other content
                    Console.Write(chunk.Content);
                    fullResponse += chunk.Content;
                    history.AddMessage(AuthorRole.Assistant, chunk.Content ?? "");
                }
            }
            Console.WriteLine(); // Add newline after streaming is complete

        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error during AI interaction: {ex.Message}");
            Console.ResetColor();
            
            // Add error information to history as a system message for context
            history.AddMessage(AuthorRole.Developer, $"Error occurred during interaction: {ex.Message}");
        }
    }
} while (userInput is not null);

// Cleanup MCP client if it was created
if (mcpClient != null)
{
    await mcpClient.DisposeAsync();
}

// Helper method to display conversation history with role visualization
static void DisplayConversationHistory(ChatHistory history)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("\n📚 Conversation History:");
    Console.WriteLine("════════════════════════");
    Console.ResetColor();
    
    if (history.Count == 0)
    {
        Console.WriteLine("No messages in history yet.");
        Console.WriteLine();
        return;
    }
    
    for (int i = 0; i < history.Count; i++)
    {
        var message = history[i];
        var roleIcon = GetRoleIcon(message.Role);
        var roleColor = GetRoleColor(message.Role);
        
        Console.ForegroundColor = roleColor;
        Console.Write($"{i + 1}. [{roleIcon} {message.Role}] ");
        Console.ResetColor();
        Console.WriteLine(message.Content);
    }
    
    Console.WriteLine($"\nTotal messages: {history.Count}");
    Console.WriteLine();
}

// Helper method to get role-specific icons
static string GetRoleIcon(AuthorRole role)
{
    return role.Label.ToLowerInvariant() switch
    {
        "system" => "⚙️",
        "user" => "👤",
        "assistant" => "🤖",
        "tool" => "🔧",
        "developer" => "👨‍💻",
        _ => "❓"
    };
}

// Helper method to get role-specific colors
static ConsoleColor GetRoleColor(AuthorRole role)
{
    return role.Label.ToLowerInvariant() switch
    {
        "system" => ConsoleColor.DarkCyan,
        "user" => ConsoleColor.Green,
        "assistant" => ConsoleColor.Blue,
        "tool" => ConsoleColor.Magenta,
        "developer" => ConsoleColor.DarkYellow,
        _ => ConsoleColor.Gray
    };
}