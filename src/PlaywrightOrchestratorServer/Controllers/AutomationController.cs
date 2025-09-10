
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
        private readonly PlaywrightMcpStdioService _mcpService;

        public AutomationController(PromptEngineeringService promptService, PlaywrightMcpStdioService mcpService)
        {
            _promptService = promptService;
            _mcpService = mcpService;
        }

        [HttpPost("steps")]
        public async Task<ActionResult<string>> RunSteps([FromBody] AutomationRequest request)
        {
            // Step-by-step orchestration: transform and execute each instruction sequentially
            var instructionLines = request.Instructions.Split('\n')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            var results = new List<StepResult>();
            foreach (var instruction in instructionLines)
            {
                // 1. Transform instruction to Playwright command using LLM
                var transformed = await _promptService.TransformInstructionForPlaywrightAsync(instruction); // Use same model as GetCompletionAsync

                // 2. Execute the transformed step via Playwright MCP
                var stepResult = await _mcpService.ExecuteStepAsync(transformed);
                //results.Add(stepResult);

                //// Early exit if a step fails
                //if (!stepResult.Done)
                //{
                //    break;
                //}
            }
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
