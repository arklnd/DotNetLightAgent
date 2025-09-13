namespace JiraIntegration.DTOs
{
    public class JiraProjectDto
    {
        public string Id { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public string ProjectTypeKey { get; set; }
        public string Description { get; set; }
        public string Lead { get; set; }
    }
}
