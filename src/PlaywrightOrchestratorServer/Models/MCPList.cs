using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
// using ModelContextProtocol.Client; // Uncomment if you add MCP client support

namespace PlaywrightOrchestratorServer.Models
{
    public class MCPList
    {
        public static List<StdioClientTransportOptions> stdioClientTransportOptions = [
            new StdioClientTransportOptions
            {
                Name = "playwright",
                Command = "npx",
                Arguments = ["@playwright/mcp@latest"],
                ShutdownTimeout = TimeSpan.FromSeconds(90)
            },
        ];
    }

    // Define this class if not available from a shared library
    public class StdioClientTransportOptions
    {
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public List<string> Arguments { get; set; } = new();
        public TimeSpan ShutdownTimeout { get; set; }
    }
}
