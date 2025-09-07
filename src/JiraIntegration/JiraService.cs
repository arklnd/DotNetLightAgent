using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using JiraIntegration.DTOs;
using Atlassian.Jira;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JiraIntegration
{
    public class JiraService
    {
        private string? _url;
        private string? _username;
        private string? _apiToken;
        private Jira? _jiraClient;

        public async Task<IEnumerable<JiraIssueDto>> GetIssuesViaRestApiAsync(string jql, string url, string username, string apiToken)
        {
            var apiUrl = $"{url.TrimEnd('/')}/rest/api/2/search?jql={Uri.EscapeDataString(jql)}";
            using var client = new HttpClient();
            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{apiToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var response = await client.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var issues = new List<JiraIssueDto>();
            foreach (var issue in doc.RootElement.GetProperty("issues").EnumerateArray())
            {
                var fields = issue.GetProperty("fields");
                issues.Add(new JiraIssueDto
                {
                    Key = issue.GetProperty("key").GetString(),
                    Summary = fields.GetProperty("summary").GetString(),
                    Status = fields.GetProperty("status").GetProperty("name").GetString(),
                    IssueType = fields.TryGetProperty("issuetype", out var issuetype) && issuetype.ValueKind != JsonValueKind.Null ? issuetype.GetProperty("name").GetString() : null,
                    Priority = fields.TryGetProperty("priority", out var priority) && priority.ValueKind != JsonValueKind.Null ? priority.GetProperty("name").GetString() : null,
                    Assignee = fields.TryGetProperty("assignee", out var assignee) && assignee.ValueKind != JsonValueKind.Null ? assignee.GetProperty("displayName").GetString() : null,
                    Reporter = fields.TryGetProperty("reporter", out var reporter) && reporter.ValueKind != JsonValueKind.Null ? reporter.GetProperty("displayName").GetString() : null,
                    Description = fields.TryGetProperty("description", out var desc) && desc.ValueKind != JsonValueKind.Null ? desc.GetString() : null,
                    Project = fields.TryGetProperty("project", out var project) && project.ValueKind != JsonValueKind.Null ? project.GetProperty("name").GetString() : null,
                    Labels = fields.TryGetProperty("labels", out var labels) && labels.ValueKind == JsonValueKind.Array ? labels.EnumerateArray().Select(l => l.GetString()).Where(l => l != null).Cast<string>().ToArray() : null,
                    Resolution = fields.TryGetProperty("resolution", out var resolution) && resolution.ValueKind != JsonValueKind.Null ? resolution.GetProperty("name").GetString() : null,
                    Created = fields.TryGetProperty("created", out var created) && DateTime.TryParse(created.GetString(), out var createdDt) ? createdDt : (DateTime?)null,
                    Updated = fields.TryGetProperty("updated", out var updated) && DateTime.TryParse(updated.GetString(), out var updatedDt) ? updatedDt : (DateTime?)null,
                    DueDate = fields.TryGetProperty("duedate", out var duedate) && DateTime.TryParse(duedate.GetString(), out var dueDt) ? dueDt : (DateTime?)null,
                    RawFields = fields.EnumerateObject().ToDictionary(f => f.Name, f => (object?)f.Value.ToString() ?? "")
                });
            }
            return issues;
        }

        public async Task<JiraIssueDto?> GetIssueByKeyViaRestApiAsync(string issueKey, string url, string username, string apiToken)
        {
            var apiUrl = $"{url.TrimEnd('/')}/rest/api/2/issue/{issueKey}";
            using var client = new HttpClient();
            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{apiToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var response = await client.GetAsync(apiUrl);
            if (!response.IsSuccessStatusCode)
                return null;
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var issue = doc.RootElement;
            var fields = issue.GetProperty("fields");
            return new JiraIssueDto
            {
                Key = issue.GetProperty("key").GetString(),
                Summary = fields.GetProperty("summary").GetString(),
                Status = fields.GetProperty("status").GetProperty("name").GetString(),
                IssueType = fields.TryGetProperty("issuetype", out var issuetype) && issuetype.ValueKind != JsonValueKind.Null ? issuetype.GetProperty("name").GetString() : null,
                Priority = fields.TryGetProperty("priority", out var priority) && priority.ValueKind != JsonValueKind.Null ? priority.GetProperty("name").GetString() : null,
                Assignee = fields.TryGetProperty("assignee", out var assignee) && assignee.ValueKind != JsonValueKind.Null ? assignee.GetProperty("displayName").GetString() : null,
                Reporter = fields.TryGetProperty("reporter", out var reporter) && reporter.ValueKind != JsonValueKind.Null ? reporter.GetProperty("displayName").GetString() : null,
                Description = fields.TryGetProperty("description", out var desc) && desc.ValueKind != JsonValueKind.Null ? desc.GetString() : null,
                Project = fields.TryGetProperty("project", out var project) && project.ValueKind != JsonValueKind.Null ? project.GetProperty("name").GetString() : null,
                Labels = fields.TryGetProperty("labels", out var labels) && labels.ValueKind == JsonValueKind.Array ? labels.EnumerateArray().Select(l => l.GetString()).Where(l => l != null).Cast<string>().ToArray() : null,
                Resolution = fields.TryGetProperty("resolution", out var resolution) && resolution.ValueKind != JsonValueKind.Null ? resolution.GetProperty("name").GetString() : null,
                Created = fields.TryGetProperty("created", out var created) && DateTime.TryParse(created.GetString(), out var createdDt) ? createdDt : (DateTime?)null,
                Updated = fields.TryGetProperty("updated", out var updated) && DateTime.TryParse(updated.GetString(), out var updatedDt) ? updatedDt : (DateTime?)null,
                DueDate = fields.TryGetProperty("duedate", out var duedate) && DateTime.TryParse(duedate.GetString(), out var dueDt) ? dueDt : (DateTime?)null,
                RawFields = fields.EnumerateObject().ToDictionary(f => f.Name, f => (object?)f.Value.ToString() ?? "")
            };
        }

        public void Authenticate(string url, string username, string apiToken)
        {
            _jiraClient = Jira.CreateRestClient(url, username, apiToken);
            _url = url;
            _username = username;
            _apiToken = apiToken;
        }

        public async Task<List<JiraProjectDto>> GetAllProjectsAsync()
        {
            if (string.IsNullOrEmpty(_url) || string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_apiToken))
                throw new InvalidOperationException("Jira client not authenticated. Call Authenticate first.");

            using var client = new HttpClient();
            client.BaseAddress = new Uri(_url);
            var authToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{_username}:{_apiToken}"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);

            var response = await client.GetAsync("/rest/api/2/project");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var projects = System.Text.Json.JsonSerializer.Deserialize<List<JiraProjectDto>>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return projects ?? new List<JiraProjectDto>();
        }
    }
}
