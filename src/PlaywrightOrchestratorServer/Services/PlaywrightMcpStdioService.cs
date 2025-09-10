using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlaywrightOrchestratorServer.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ModelContextProtocol.Client;
using Microsoft.SemanticKernel.Connectors.Ollama;


namespace PlaywrightOrchestratorServer.Services
{
	public class PlaywrightMcpStdioService
	{
		// Simulated tool list for development/testing
		private static readonly List<string> SimulatedTools = new() { "click", "type", "navigate", "screenshot" };

		// Called from [HttpPost("steps")]
		public async Task<string> ExecuteStepAsync(string step)
		{
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
            // ...existing code...
            // Add enterprise components
            kernelBuilder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Information));

            // Build the kernel
            Kernel kernel = kernelBuilder.Build();
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();


            IMcpClient? mcpClient = null;
            // Stdio based MCP with error handling
            foreach (var stdmcp in MCPList.stdioClientTransportOptions)
            {
                try
                {
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

            // Simulate a response
            return $"[Simulated] Step received: {step}";
		}
	}
}
