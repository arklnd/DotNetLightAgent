using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
await builder.Build().RunAsync();

[McpServerToolType]
public static class AppLauncher
{
    [McpServerTool(Name = "launch_ob_studio"), Description("Launches OB Studio application")]
    public static async Task<string> LaunchOBSStudio()
    {
        try
        {
            // Use stderr for debugging to avoid interfering with JSON-RPC on stdout
            Console.Error.WriteLine("Launching OBS Studio...");
            var obsPath = @"C:\Codebase\TFS\OnBase\DEV\Core\OnBase.NET\Applications\Hyland.Applications.OnBase.Studio\bin\x64\Debug\obstudio.exe";
            
            // Check if the file exists first
            if (!File.Exists(obsPath))
            {
                return $"Error: OBS Studio executable not found at path: {obsPath}";
            }
            
            // Use cmd.exe with start command to create a truly detached process
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start \"OBS Studio\" \"{obsPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(obsPath)
            };
            
            var process = Process.Start(startInfo);
            
            if (process != null)
            {
                // Wait for cmd to finish (it should exit quickly after starting the detached process)
                await process.WaitForExitAsync();
                
                // Give the actual application time to start
                await Task.Delay(1000);
                
                // Try to find the OBS Studio process
                var obsProcesses = Process.GetProcessesByName("obstudio");
                if (obsProcesses.Length > 0)
                {
                    var obsProcess = obsProcesses[0];
                    return $"Successfully launched and detached OBS Studio from: {obsPath}. Process ID: {obsProcess.Id}, Process Name: {obsProcess.ProcessName}";
                }
                else
                {
                    return $"Started detached process but could not verify OBS Studio is running. Command executed: cmd /c start \"OBS Studio\" \"{obsPath}\"";
                }
            }
            else
            {
                return $"Failed to start process from: {obsPath}";
            }
        }
        catch (Exception ex)
        {
            return $"Error launching OBS Studio: {ex.Message}";
        }
    }
}
