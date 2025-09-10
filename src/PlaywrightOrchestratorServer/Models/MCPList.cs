using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Client;

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
}
