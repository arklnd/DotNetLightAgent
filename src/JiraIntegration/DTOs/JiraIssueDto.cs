namespace JiraIntegration.DTOs
{
    public class JiraIssueDto
    {
        public string? Key { get; set; }
        public string? Summary { get; set; }
        public string? Status { get; set; }
        public string? IssueType { get; set; }
        public string? Priority { get; set; }
        public string? Assignee { get; set; }
        public string? Reporter { get; set; }
        public string? Description { get; set; }
        public string? Project { get; set; }
        public string[]? Labels { get; set; }
        public string? Resolution { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Updated { get; set; }
        public DateTime? DueDate { get; set; }
        public Dictionary<string, object>? RawFields { get; set; } // For extensibility
    }
}
