using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS to allow frontend applications
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register MCP service
builder.Services.AddSingleton<MCPService>();
builder.Services.AddHostedService<MCPHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Enable Swagger in production for this demo
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseStaticFiles(); // Enable serving static files
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// Serve index.html for root path
app.MapGet("/", () => Results.Redirect("/index.html"));
app.MapFallbackToFile("index.html");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();

// Request/Response models
public record MCPRequest(string Command);
public record MCPResponse(bool Success, string Message, string? Details = null);

// MCP Service for managing server communication
public class MCPService : IDisposable
{
    private Process? _serverProcess;
    private readonly ILogger<MCPService> _logger;
    private bool _initialized = false;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    public MCPService(ILogger<MCPService> logger)
    {
        _logger = logger;
        StartPersistentServer();
    }

    private void StartPersistentServer()
    {
        try
        {
            _logger.LogInformation("🔧 Starting persistent MCP Server...");
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project C:\\Codebase\\MCP\\MCPServer\\MCPServer.csproj",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _serverProcess = new Process { StartInfo = startInfo };
            _serverProcess.Start();
            
            if (_serverProcess != null)
            {
                _logger.LogInformation("✅ MCP Server started persistently with PID: {ProcessId}", _serverProcess.Id);
                
                // Start reading stderr for debugging
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (_serverProcess != null && !_serverProcess.HasExited)
                        {
                            var errorLine = await _serverProcess.StandardError.ReadLineAsync();
                            if (!string.IsNullOrEmpty(errorLine))
                            {
                                _logger.LogDebug("🔴 Server stderr: {ErrorLine}", errorLine);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Error reading server stderr: {Message}", ex.Message);
                    }
                });
            }
            else
            {
                _logger.LogError("Failed to start MCP Server");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting persistent MCP Server");
        }
    }

    public async Task<MCPResponse> ProcessCommandAsync(string command)
    {
        if (_serverProcess == null || _serverProcess.HasExited)
        {
            return new MCPResponse(false, "MCP Server is not running");
        }

        if (IsOBSLaunchCommand(command))
        {
            try
            {
                var result = await CallPersistentMCPServerTool("launch_ob_studio");
                return new MCPResponse(true, "Command executed successfully", result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling MCP server tool");
                return new MCPResponse(false, $"Error: {ex.Message}");
            }
        }
        else
        {
            return new MCPResponse(false, "Command not recognized. Try 'launch obs studio' or similar commands.");
        }
    }

    private static bool IsOBSLaunchCommand(string input)
    {
        var lowerInput = input.ToLower();
        var obsKeywords = new[] { "ob", "ob studio", "onbase", "onbase studio" };
        var actionKeywords = new[] { "launch", "start", "open", "run", "begin" };
        
        bool hasObsKeyword = obsKeywords.Any(keyword => lowerInput.Contains(keyword));
        bool hasActionKeyword = actionKeywords.Any(keyword => lowerInput.Contains(keyword));
        
        return hasObsKeyword && (hasActionKeyword || lowerInput.Trim().Split().Length <= 2);
    }

    private async Task<string> CallPersistentMCPServerTool(string toolName)
    {
        if (_serverProcess?.StandardInput == null || _serverProcess?.StandardOutput == null)
        {
            throw new InvalidOperationException("MCP Server process is not properly initialized");
        }

        await _initializationLock.WaitAsync();
        try
        {
            // Initialize the MCP server if not already done
            if (!_initialized)
            {
                await InitializeMCPServer();
                _initialized = true;
            }

            // Call the tool
            var toolRequest = new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().ToString(),
                method = "tools/call",
                @params = new
                {
                    name = toolName,
                    arguments = new { }
                }
            };

            await SendJsonRequest(toolRequest);
            var response = await ReadJsonResponse();
            
            // Parse the response to extract the result
            return ParseToolResponse(response);
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task InitializeMCPServer()
    {
        _logger.LogInformation("🔧 Initializing MCP Server connection...");
        
        // Send initialization request
        var initRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "MCPWebClient", version = "1.0.0" }
            }
        };

        await SendJsonRequest(initRequest);
        var initResponse = await ReadJsonResponse();
        
        if (initResponse.ValueKind == JsonValueKind.Undefined)
        {
            throw new Exception("Failed to get initialization response from server");
        }
        
        // Send initialized notification
        var initializedNotification = new
        {
            jsonrpc = "2.0",
            method = "notifications/initialized"
        };
        
        await SendJsonRequest(initializedNotification);
        
        // Give server a moment to process the notification
        await Task.Delay(100);
        
        _logger.LogInformation("✅ MCP Server initialized successfully");
    }

    private async Task SendJsonRequest(object request)
    {
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        _logger.LogDebug("📤 Sending to server: {Json}", json);
        
        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        if (_serverProcess?.StandardInput?.BaseStream != null)
        {
            await _serverProcess.StandardInput.BaseStream.WriteAsync(bytes);
            await _serverProcess.StandardInput.BaseStream.FlushAsync();
        }
    }

    private async Task<JsonElement> ReadJsonResponse()
    {
        // Add timeout to prevent hanging
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        
        try
        {
            var line = await _serverProcess!.StandardOutput.ReadLineAsync();
            _logger.LogDebug("📥 Received from server: {Line}", line);
            
            if (string.IsNullOrEmpty(line))
            {
                _logger.LogWarning("⚠️ Received empty response");
                return new JsonElement();
            }
                
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(line);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ JSON parsing error. Raw response: {Line}", line);
                return new JsonElement();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("⏱️ Timeout waiting for server response");
            return new JsonElement();
        }
    }

    private string ParseToolResponse(JsonElement response)
    {
        try
        {
            _logger.LogDebug("🔍 Parsing response: {Response}", response);
            
            if (response.ValueKind == JsonValueKind.Undefined)
            {
                return "No response received from server";
            }
            
            if (response.TryGetProperty("result", out var resultElement))
            {
                _logger.LogDebug("✅ Found result element: {Result}", resultElement);
                
                if (resultElement.TryGetProperty("content", out var contentElement) && 
                    contentElement.ValueKind == JsonValueKind.Array && 
                    contentElement.GetArrayLength() > 0)
                {
                    var firstContent = contentElement[0];
                    if (firstContent.TryGetProperty("text", out var textElement))
                    {
                        return textElement.GetString() ?? "Tool executed successfully";
                    }
                }
                
                // If no content array, try to get the result directly
                return resultElement.ToString();
            }
            
            if (response.TryGetProperty("error", out var errorElement))
            {
                var errorMessage = "Unknown error";
                if (errorElement.TryGetProperty("message", out var messageElement))
                {
                    errorMessage = messageElement.GetString() ?? "Unknown error";
                }
                return $"Error from MCP Server: {errorMessage}";
            }
            
            return $"Unexpected response format: {response}";
        }
        catch (Exception ex)
        {
            return $"Error parsing response: {ex.Message}. Raw response: {response}";
        }
    }

    public void Dispose()
    {
        try
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                _logger.LogInformation("🔧 Shutting down persistent MCP Server...");
                _serverProcess.Kill();
                _serverProcess.Dispose();
            }
            _initializationLock?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing MCP server process");
        }
    }
}

// API Controller
[ApiController]
[Route("api/[controller]")]
public class MCPController : ControllerBase
{
    private readonly MCPService _mcpService;
    private readonly ILogger<MCPController> _logger;

    public MCPController(MCPService mcpService, ILogger<MCPController> logger)
    {
        _mcpService = mcpService;
        _logger = logger;
    }

    [HttpPost("execute")]
    public async Task<ActionResult<MCPResponse>> ExecuteCommand([FromBody] MCPRequest request)
    {
        try
        {
            _logger.LogInformation("Received command: {Command}", request.Command);
            
            var response = await _mcpService.ProcessCommandAsync(request.Command);
            
            if (response.Success)
            {
                return Ok(response);
            }
            else
            {
                return BadRequest(response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command: {Command}", request.Command);
            return StatusCode(500, new MCPResponse(false, "Internal server error", ex.Message));
        }
    }

    [HttpGet("status")]
    public ActionResult<object> GetStatus()
    {
        return Ok(new 
        { 
            status = "running",
            timestamp = DateTime.UtcNow,
            availableCommands = new[] 
            { 
                "launch ob studio",
                "start ob",
                "open onbase studio"
            }
        });
    }
}

// Hosted service to manage MCP service lifecycle
public class MCPHostedService : IHostedService
{
    private readonly MCPService _mcpService;

    public MCPHostedService(MCPService mcpService)
    {
        _mcpService = mcpService;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _mcpService?.Dispose();
        return Task.CompletedTask;
    }
}
