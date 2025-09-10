using PlaywrightOrchestratorServer.Models;
using System.Text.Json;
using System.Text;
using System;

namespace PlaywrightOrchestratorServer.Services
{
    public class PlaywrightMcpService
    {
        private readonly HttpClient _httpClient;
    private readonly string _mcpApiUrl = "http://localhost:5001/mcp"; 

        public PlaywrightMcpService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<StepResult> ExecuteStepAsync(string step)
        {
            // 1. Construct the JSON payload for the "browser_navigate" tool.
            // The structure is based on the Playwright MCP API specification.
            var payload = new
            {
                tool_name = "browser_navigate",
                tool_input = new
                {
                    url = "www.google.com"
                }
            };

            // 2. Serialize the C# object into a JSON string.
            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            Console.WriteLine($"Sending request to http://localhost:5000 with payload:\n{jsonPayload}\n");

            try
            {
                // 3. Send the POST request to the server.
                // The Playwright MCP server endpoint for tools is typically at the root URI.
                var response = await _httpClient.PostAsync("http://localhost:5000/mcp", content);

                // 4. Ensure the request was successful and handle the response.
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Request successful!");
                Console.WriteLine($"Server response: {responseBody}");
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"\nHTTP request failed: {e.Message}");
                Console.WriteLine("Please ensure the Playwright MCP server is running at the specified URI.");
            }
            // The step is a JSON string from the LLM, send it directly to the MCP server
            var content1 = new StringContent(step, System.Text.Encoding.UTF8, "application/json");

            var response1 = await _httpClient.PostAsync(_mcpApiUrl, content1);

            if (response1.IsSuccessStatusCode)
            {
                var responseBody = await response1.Content.ReadAsStringAsync();
                return new StepResult { Step = step, Done = true, Message = responseBody };
            }
            else
            {
                return new StepResult { Step = step, Done = false, Message = $"Automation failed. Status code: {response1.StatusCode}" };
            }
        }
    }
}
