using System.Diagnostics;
using System.Text;
using System.Text.Json;

Console.WriteLine("=== MCP Client for OBS Studio Launcher ===");
Console.WriteLine("Type natural language commands to launch OBS Studio");
Console.WriteLine("Examples: 'launch obs', 'start obs studio', 'open obs'");
Console.WriteLine("Type 'quit' to exit\n");

// Start persistent MCP server
Process? serverProcess = null;
try
{
    Console.WriteLine("🔧 Starting persistent MCP Server...");
    serverProcess = StartPersistentServer();
    await Task.Delay(2000); // Give server time to start
    Console.WriteLine("✅ MCP Server is running persistently\n");

    // Main interaction loop
    while (true)
    {
        Console.Write("Enter command: ");
        var input = Console.ReadLine();
        
        if (string.IsNullOrWhiteSpace(input))
            continue;
            
        if (input.ToLower() == "quit")
        {
            Console.WriteLine("Goodbye!");
            break;
        }
        
        // Process natural language input
        if (IsOBSLaunchCommand(input))
        {
            Console.WriteLine("🚀 Launching OBS Studio via persistent MCP Server...");
            
            try
            {
                var result = await CallPersistentMCPServerTool("launch_ob_studio", serverProcess);
                Console.WriteLine($"✅ {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("❓ I didn't understand that command. Try something like:");
            Console.WriteLine("  - 'launch obs'");
            Console.WriteLine("  - 'start obs studio'");
            Console.WriteLine("  - 'open obs'");
        }
        
        Console.WriteLine();
    }
}
finally
{
    // Clean up server process
    if (serverProcess != null && !serverProcess.HasExited)
    {
        Console.WriteLine("🔧 Shutting down MCP Server...");
        try
        {
            serverProcess.Kill();
            await serverProcess.WaitForExitAsync();
        }
        catch { }
        serverProcess.Dispose();
    }
}

// Simple natural language processing for OBS launch commands
static bool IsOBSLaunchCommand(string input)
{
    var lowerInput = input.ToLower();
    var obsKeywords = new[] { "obs", "obs studio", "obstudio" };
    var actionKeywords = new[] { "launch", "start", "open", "run", "begin" };
    
    bool hasObsKeyword = obsKeywords.Any(keyword => lowerInput.Contains(keyword));
    bool hasActionKeyword = actionKeywords.Any(keyword => lowerInput.Contains(keyword));
    
    return hasObsKeyword && (hasActionKeyword || lowerInput.Trim().Split().Length <= 2);
}

// Start persistent MCP server that stays alive
static Process StartPersistentServer()
{
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

    var process = new Process { StartInfo = startInfo };
    process.Start();
    
    // Start reading stderr for debugging
    _ = Task.Run(async () =>
    {
        while (!process.HasExited)
        {
            var errorLine = await process.StandardError.ReadLineAsync();
            if (!string.IsNullOrEmpty(errorLine))
            {
                Console.WriteLine($"🔴 Server stderr: {errorLine}");
            }
        }
    });

    return process;
}

// Call MCP Server tool via persistent connection
static async Task<string> CallPersistentMCPServerTool(string toolName, Process serverProcess)
{
    if (serverProcess == null || serverProcess.HasExited)
    {
        throw new Exception("MCP Server is not running");
    }

    try
    {
        // Initialize the MCP server (only needed once, but server handles multiple initializations)
        var initRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "MCPClient", version = "1.0.0" }
            }
        };

        await SendJsonRequest(serverProcess, initRequest);
        var initResponse = await ReadJsonResponse(serverProcess);
        
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
        
        await SendJsonRequest(serverProcess, initializedNotification);
        
        // Give server a moment to process the notification
        await Task.Delay(100);

        // Call the tool
        var toolRequest = new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/call",
            @params = new
            {
                name = toolName,
                arguments = new { }
            }
        };

        await SendJsonRequest(serverProcess, toolRequest);
        var response = await ReadJsonResponse(serverProcess);
        
        // Parse the response to extract the result
        return ParseToolResponse(response);
    }
    catch (Exception ex)
    {
        throw new Exception($"Error communicating with persistent MCP server: {ex.Message}");
    }
}

static async Task SendJsonRequest(Process process, object request)
{
    var json = JsonSerializer.Serialize(request, new JsonSerializerOptions 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase  // Actually, the test showed camelCase works fine
    });
    Console.WriteLine($"📤 Sending to server: {json}"); // Debug output
    
    var bytes = Encoding.UTF8.GetBytes(json + "\n");
    await process.StandardInput.BaseStream.WriteAsync(bytes);
    await process.StandardInput.BaseStream.FlushAsync();
}

static async Task<JsonElement> ReadJsonResponse(Process process)
{
    // Add timeout to prevent hanging
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    
    try
    {
        var line = await process.StandardOutput.ReadLineAsync();
        Console.WriteLine($"📥 Received from server: {line}"); // Debug output
        
        if (string.IsNullOrEmpty(line))
        {
            Console.WriteLine("⚠️ Received empty response");
            return new JsonElement();
        }
            
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(line);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ JSON parsing error: {ex.Message}");
            Console.WriteLine($"📄 Raw response: {line}");
            return new JsonElement();
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("⏱️ Timeout waiting for server response");
        return new JsonElement();
    }
}

static string ParseToolResponse(JsonElement response)
{
    try
    {
        Console.WriteLine($"🔍 Parsing response: {response}"); // Debug output
        
        if (response.ValueKind == JsonValueKind.Undefined)
        {
            return "No response received from server";
        }
        
        if (response.TryGetProperty("result", out var resultElement))
        {
            Console.WriteLine($"✅ Found result element: {resultElement}");
            
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
