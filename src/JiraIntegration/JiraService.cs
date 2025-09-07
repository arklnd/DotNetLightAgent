using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using JiraIntegration.DTOs;
using Atlassian.Jira;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JiraIntegration;

public class JiraService
{
    private string? _url;
    private string? _username;
    private string? _apiToken;
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
                IssueType = fields.GetProperty("issuetype").GetProperty("name").GetString(),
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
    private Jira? _jiraClient;

    /// <summary>
    /// Authenticates and creates a Jira client instance.
    /// </summary>
    /// <param name="url">Jira instance URL</param>
    /// <param name="username">Jira username/email</param>
    /// <param name="apiToken">Jira API token</param>
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

    /// <summary>
    /// Fetches issues from Jira using a JQL query.
    /// </summary>
    /// <param name="jql">Jira Query Language string</param>
    /// <returns>List of Jira issues</returns>
    public async Task<IEnumerable<Issue>> GetIssuesAsync(string jql)
    {
        if (_jiraClient == null)
            throw new System.InvalidOperationException("Jira client not authenticated. Call Authenticate first.");

        // Use GetIssuesFromJqlAsync to fetch issues by JQL
        var issues = await _jiraClient.Issues.GetIssuesFromJqlAsync(jql);
        return issues;
    }

    /// <summary>
    /// Fetches a Jira issue by its key.
    /// </summary>
    /// <param name="issueKey">Jira issue key (e.g., PROJ-123)</param>
    /// <returns>The Jira issue</returns>
    public async Task<Issue?> GetIssueByKeyAsync(string issueKey)
    {
        if (_jiraClient == null)
            throw new System.InvalidOperationException("Jira client not authenticated. Call Authenticate first.");

        return await _jiraClient.Issues.GetIssueAsync(issueKey);
    }

    /// <summary>
    /// Gets the steps to reproduce from a Jira issue.
    /// </summary>
    /// <param name="issue">The Jira issue</param>
    /// <returns>Steps to reproduce as a string, or null if not found</returns>
    public string? GetStepsToReproduce(Issue issue)
    {
        // Try to get from a custom field (commonly used for steps)
        var stepsField = issue["Steps to Reproduce"]?.ToString();
        if (!string.IsNullOrWhiteSpace(stepsField))
            return stepsField;

        // Fallback: try to extract from description
        if (!string.IsNullOrWhiteSpace(issue.Description))
        {
            // Simple heuristic: look for a section starting with "Steps to Reproduce"
            var desc = issue.Description;
            var marker = "Steps to Reproduce";
            var idx = desc.IndexOf(marker, System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var steps = desc.Substring(idx + marker.Length).Trim();
                // Optionally, stop at next section header or limit length
                return steps;
            }
        }
        return null;
    }
}
