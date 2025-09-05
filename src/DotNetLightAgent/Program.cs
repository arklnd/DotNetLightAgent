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
var modelId = "openai/gpt-5-chat"; // or any other model you have installed in Ollama
var endpoint = new Uri("https://models.github.ai/inference");
var apiKey = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? throw new InvalidOperationException("Please set the GITHUB_TOKEN environment variable.");

// Create a kernel with Ollama chat completion
var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddAzureAIInferenceChatCompletion(
    modelId: modelId,
    endpoint: endpoint,
    apiKey: apiKey // or credential, depending on exact Semantic Kernel API version
    // serviceId: "optional-custom-id" // If you want to distinguish between services
);
// Add enterprise components
kernelBuilder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Information));

// Build the kernel
Kernel kernel = kernelBuilder.Build();
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

// Add a plugin (the LightsPlugin class is defined below)
// kernel.Plugins.AddFromType<LightsPlugin>("Lights");

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
    // ExtensionData = new Dictionary<string, object>
    // {
    //     { "num_predict", 512 },
    //     { "num_ctx", 4096 }
    // }
};
// Create a history store the conversation
var history = new ChatHistory();

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
        history.AddMessage(AuthorRole.System, @"You are a helpful AI assistant with access to various tools and capabilities including:
- Ability to call functions and perform actions based on user requests");
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
                }
                else if (!string.IsNullOrEmpty(chunk.Content))
                {
                    // Fallback for any other content
                    Console.Write(chunk.Content);
                    fullResponse += chunk.Content;
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