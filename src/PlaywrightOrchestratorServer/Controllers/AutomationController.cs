
using Microsoft.AspNetCore.Mvc;
using PlaywrightOrchestratorServer.Models;
using PlaywrightOrchestratorServer.Services;

namespace PlaywrightOrchestratorServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AutomationController : ControllerBase
    {
        private readonly PromptEngineeringService _promptService;
        private readonly PlaywrightMcpService _mcpService;

        public AutomationController(PromptEngineeringService promptService, PlaywrightMcpService mcpService)
        {
            _promptService = promptService;
            _mcpService = mcpService;
        }

        [HttpPost("steps")]
        public async Task<ActionResult<List<StepResult>>> RunSteps([FromBody] AutomationRequest request)
        {
            // 1. Use prompt engineering to extract/format steps
            var steps = await _promptService.ExtractStepsAsync(request.Instructions);
            // 2. Call Playwright MCP for each step
            var results = await _mcpService.ExecuteStepsAsync(steps);
            return Ok(results);
        }
        [HttpPost("completion")]
        public async Task<ActionResult<string>> GetCompletion([FromBody] PromptRequest request)
        {
            var result = await _promptService.GetCompletionAsync(request.Prompt);
            return Ok(result);
        }
    }
}
