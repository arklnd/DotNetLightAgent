
using System.Net.Http.Json;

namespace PlaywrightOrchestratorServer.Services
{
    public class PromptEngineeringService
    {
        private readonly HttpClient _httpClient;
        private readonly string _hkvcApiUrl = "http://localhost:3000/v1/chat/completions";
        private readonly string _systemPrompt;

        public PromptEngineeringService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _systemPrompt = configuration["PromptEngineering:SystemPrompt"] ?? "";
        }

        public async Task<List<string>> ExtractStepsAsync(string instructions)
        {
            // Split instructions into lines
            var lines = instructions.Split('\n')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            var transformedSteps = new List<string>();
            foreach (var line in lines)
            {
                var transformed = await TransformInstructionForPlaywrightAsync(line);
                transformedSteps.Add(transformed);
            }
            return transformedSteps;
        }

        // Transforms a single instruction into a Playwright MCP-friendly command using LLM
        public async Task<string> TransformInstructionForPlaywrightAsync(string instruction)
        {
            var requestBody = new
            {
                model = "copilot-gpt-4o",
                messages = new[]
                {
                    new { role = "system", content = _systemPrompt },
                    new { role = "user", content = instruction }
                }
            };

            var response = await _httpClient.PostAsJsonAsync(_hkvcApiUrl, requestBody);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>();
            var content = result?.choices?.FirstOrDefault()?.message?.content ?? string.Empty;
            return content;
        }

        public async Task<string> GetCompletionAsync(string prompt)
        {
            var requestBody = new
            {
                model = "copilot-gpt-4o",
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var response = await _httpClient.PostAsJsonAsync(_hkvcApiUrl, requestBody);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>();
            return result?.choices?.FirstOrDefault()?.message?.content ?? string.Empty;
        }

    }
}

public class ChatCompletionResponse
{
    public List<ChatCompletionResponse.Choice> choices { get; set; } = new();
    public class Choice
    {
        public Message message { get; set; } = new();
    }
    public class Message
    {
        public string content { get; set; } = string.Empty;
    }
}

public class OpenAiCompletionResponse
{
    public List<OpenAiCompletionResponse.Choice> choices { get; set; } = new();
    public class Choice { public string text { get; set; } = string.Empty; }
}
