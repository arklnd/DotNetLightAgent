
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
        public async Task<IEnumerable<JiraIssueDto>> GetIssues([FromQuery] string jql)
        {
            // Use REST API for reliability
            var url = _config["Jira:Url"];
            var username = _config["Jira:Username"];
            var apiToken = _config["Jira:ApiToken"];
            return await _jiraService.GetIssuesViaRestApiAsync(jql, url, username, apiToken);
        }

        [HttpGet("issue/{key}")]
        public async Task<JiraIssueDto?> GetIssueByKey(string key)
        {
            var issue = await _jiraService.GetIssueByKeyAsync(key);
            if (issue == null) return null;
            return new JiraIssueDto
            {
                Key = issue.Key?.Value,
                Summary = issue.Summary,
                Status = issue.Status?.Name,
                Assignee = issue.Assignee,
                Created = issue.Created,
                Updated = issue.Updated
            };
        }

        [HttpGet("issue/{key}/steps")]
        public async Task<string?> GetStepsToReproduce(string key)
        {
            var issue = await _jiraService.GetIssueByKeyAsync(key);
            if (issue == null) return null;
            return _jiraService.GetStepsToReproduce(issue);
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
