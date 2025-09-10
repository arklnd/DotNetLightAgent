using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Client;

namespace DotNetLightAgent.Models
{
    public class MCPList
    {
        public static List<StdioClientTransportOptions> stdioClientTransportOptions = [
            // new StdioClientTransportOptions
            // {
            //     Name = "FileSystem",
            //     Command = "npx",
            //     Arguments = ["-y", "@modelcontextprotocol/server-filesystem", "C:\\ARIJIT"],
            //     ShutdownTimeout = TimeSpan.FromSeconds(90)
            // },
            // new StdioClientTransportOptions
            // {
            //     Name = "Sequential_Thinker",
            //     Command = "npx",
            //     Arguments = ["-y", "@modelcontextprotocol/server-sequential-thinking"],
            //     ShutdownTimeout = TimeSpan.FromSeconds(90)
            // },
            // new StdioClientTransportOptions
            // {
            //     Name = "fetch_web",
            //     Command = "python",
            //     Arguments = ["-m", "mcp_server_fetch"],
            //     ShutdownTimeout = TimeSpan.FromSeconds(90)
            // },
            // new StdioClientTransportOptions
            // {
            //     Name = "git",
            //     Command = "python",
            //     Arguments = ["-m", "mcp_server_git"],
            //     ShutdownTimeout = TimeSpan.FromSeconds(90)
            // },
            // new StdioClientTransportOptions
            // {
            //     Name = "time",
            //     Command = "python",
            //     Arguments = ["-m", "mcp_server_time"],
            //     ShutdownTimeout = TimeSpan.FromSeconds(90)
            // },
            new StdioClientTransportOptions
            {
                Name = "playwright",
                Command = "npx",
                Arguments = ["@playwright/mcp@latest", "--headless"],
                ShutdownTimeout = TimeSpan.FromSeconds(90)
            },
        ];
    }
}