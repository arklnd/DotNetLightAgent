
using System.Net.Http.Json;

namespace PlaywrightOrchestratorServer.Services
{
    public class PromptEngineeringService
    {
        private readonly HttpClient _httpClient;
        private readonly string _hkvcApiUrl = "http://localhost:3000/v1/chat/completions"; 

        public PromptEngineeringService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<string>> ExtractStepsAsync(string instructions)
        {
            // Example: Call HKVC API server for prompt engineering
            // Replace with your actual prompt and parsing logic
            var prompt = $"Extract step-by-step instructions from the following text:\n{instructions}";
            var response = await _httpClient.PostAsJsonAsync(_hkvcApiUrl, new
            {
                prompt = prompt,
                max_tokens = 512
            });
            var result = await response.Content.ReadFromJsonAsync<OpenAiCompletionResponse>();
            // Assume result.choices[0].text contains steps separated by newlines
            var steps = result?.choices?.FirstOrDefault()?.text?.Split('\n')?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();
            return steps;
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

        private class ChatCompletionResponse
        {
            public List<Choice> choices { get; set; } = new();
            public class Choice
            {
                public Message message { get; set; } = new();
            }
            public class Message
            {
                public string content { get; set; } = string.Empty;
            }
        }

        private class OpenAiCompletionResponse
        {
            public List<Choice> choices { get; set; } = new();
            public class Choice { public string text { get; set; } = string.Empty; }
        }
    }
}
