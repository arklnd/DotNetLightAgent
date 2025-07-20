// Import packages
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using DotNetLightAgent.Plugins;
using ModelContextProtocol.Client;
using Microsoft.SemanticKernel.Connectors.Ollama;
using DotNetLightAgent.Models;

// Populate values for your Ollama deployment
var modelId = "qwen3"; // or any other model you have installed in Ollama
var endpoint = new Uri("http://172.30.245.214:11434"); // default Ollama endpoint

// Create a kernel with Ollama chat completion
var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddOllamaChatCompletion(modelId, endpoint);

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
    Temperature = 0.6f,
    TopP = (float?)0.95,
    TopK = 40,
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

    // Add user input
    if (!string.IsNullOrWhiteSpace(userInput))
    {
        history.AddUserMessage(userInput);

        // Print the AI response header
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write("[🤖] > ");
        Console.ResetColor();

        // Get the streaming response from the AI
        string fullResponse = "";
        await foreach (var chunk in chatCompletionService.GetStreamingChatMessageContentsAsync(
            history,
            executionSettings: ollamaPromptExecutionSettings,
            kernel: kernel))
        {
            Console.Write(chunk.Content); // Stream response as it arrives
            fullResponse += chunk.Content;
        }
        Console.WriteLine(); // Add newline after streaming is complete

        // Add the complete message from the agent to the chat history
        history.AddAssistantMessage(fullResponse);
    }
} while (userInput is not null);

// Cleanup MCP client if it was created
if (mcpClient != null)
{
    await mcpClient.DisposeAsync();
}