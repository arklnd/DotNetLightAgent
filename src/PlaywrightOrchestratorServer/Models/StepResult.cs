namespace PlaywrightOrchestratorServer.Models
{
    public class StepResult
    {
        public string Step { get; set; } = string.Empty;
        public bool Done { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
