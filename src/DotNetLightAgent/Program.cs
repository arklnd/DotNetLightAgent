// Import packages
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using DotNetLightAgent.Plugins;

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

// Enable planning
PromptExecutionSettings promptExecutionSettings = new()
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

// Create a history store the conversation
var history = new ChatHistory();

// Initiate a back-and-forth chat
string? userInput;
do
{
    // Collect user input
    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("User > ");
    Console.ResetColor();
    userInput = Console.ReadLine();

    // Add user input
    if (userInput != null)
    {
        history.AddUserMessage(userInput);

        // Get the response from the AI
        var result = await chatCompletionService.GetChatMessageContentAsync(
            history,
            executionSettings: promptExecutionSettings,
            kernel: kernel);

        // Print the results
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write("Agent > ");
        Console.ResetColor();
        Console.WriteLine(result);

        // Add the message from the agent to the chat history
        history.AddMessage(result.Role, result.Content ?? string.Empty);
    }
} while (userInput is not null);