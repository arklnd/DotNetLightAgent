using PlaywrightOrchestratorServer.Models;

namespace PlaywrightOrchestratorServer.Services
{
    public class PlaywrightMcpService
    {
        private readonly HttpClient _httpClient;
        private readonly string _mcpApiUrl = "http://localhost:5001/api/mcp/run-step"; // Adjust as needed

        public PlaywrightMcpService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<StepResult>> ExecuteStepsAsync(List<string> steps)
        {
            var results = new List<StepResult>();
            foreach (var step in steps)
            {
                // Example: Call Playwright MCP server for each step
                var response = await _httpClient.PostAsJsonAsync(_mcpApiUrl, new { step });
                var result = await response.Content.ReadFromJsonAsync<StepResult>();
                results.Add(result ?? new StepResult { Step = step, Done = false, Message = "No response" });
            }
            return results;
        }
    }
}
