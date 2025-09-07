
        using JiraIntegration;
        using JiraIntegration.DTOs;
        using Microsoft.AspNetCore.Mvc;
        using Atlassian.Jira;
        using Microsoft.Extensions.Configuration;

namespace JiraAutomateServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JiraController : ControllerBase
    {
        private static JiraService _jiraService = new JiraService();
        private readonly IConfiguration _config;

        public JiraController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost("authenticate")]
        public IActionResult Authenticate([FromBody] JiraAuthRequest request)
        {
            // Use config defaults if not provided in request
            var url = string.IsNullOrWhiteSpace(request.Url) ? _config["Jira:Url"] : request.Url;
            var username = string.IsNullOrWhiteSpace(request.Username) ? _config["Jira:Username"] : request.Username;
            var apiToken = string.IsNullOrWhiteSpace(request.ApiToken) ? _config["Jira:ApiToken"] : request.ApiToken;
            _jiraService.Authenticate(url, username, apiToken);
            return Ok("Authenticated");
        }


        [HttpGet("issues")]
        public async Task<ActionResult<IEnumerable<JiraIssueDto>>> GetIssues([FromQuery] string jql)
        {
            var url = _config["Jira:Url"];
            var username = _config["Jira:Username"];
            var apiToken = _config["Jira:ApiToken"];
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(apiToken))
                return BadRequest("Jira configuration is missing.");
            var issues = await _jiraService.GetIssuesViaRestApiAsync(jql, url, username, apiToken);
            return Ok(issues);
        }


        [HttpGet("issue/{key}")]
        public async Task<ActionResult<JiraIssueDto?>> GetIssueByKey(string key)
        {
            var url = _config["Jira:Url"];
            var username = _config["Jira:Username"];
            var apiToken = _config["Jira:ApiToken"];
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(apiToken))
                return BadRequest("Jira configuration is missing.");
            var issue = await _jiraService.GetIssueByKeyViaRestApiAsync(key, url, username, apiToken);
            if (issue == null) return NotFound();
            return Ok(issue);
        }


        [HttpGet("issue/{key}/steps")]
        public async Task<ActionResult<string?>> GetStepsToReproduce(string key)
        {
            var url = _config["Jira:Url"];
            var username = _config["Jira:Username"];
            var apiToken = _config["Jira:ApiToken"];
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(apiToken))
                return BadRequest("Jira configuration is missing.");
            var issue = await _jiraService.GetIssueByKeyViaRestApiAsync(key, url, username, apiToken);
            if (issue == null) return NotFound();
            // Try to get steps from a custom field or from the description
            string? steps = null;
            if (issue.RawFields != null && issue.RawFields.TryGetValue("Steps to Reproduce", out var stepsObj))
            {
                steps = stepsObj?.ToString();
            }
            if (string.IsNullOrWhiteSpace(steps) && !string.IsNullOrWhiteSpace(issue.Description))
            {
                var desc = issue.Description;
                var marker = "Steps to Reproduce";
                var idx = desc.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    steps = desc.Substring(idx + marker.Length).Trim();
                }
            }
            return Ok(steps);
        }

        [HttpGet("projects")]
        public async Task<ActionResult<IEnumerable<JiraProjectDto>>> GetAllProjects()
        {
            try
            {
                var projects = await _jiraService.GetAllProjectsAsync();
                return Ok(projects);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }

    public class JiraAuthRequest
    {
        public string Url { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string ApiToken { get; set; } = string.Empty;
    }
}
