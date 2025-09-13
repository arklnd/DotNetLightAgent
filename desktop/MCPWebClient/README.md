# MCP Web Client

A web-based client for the Model Context Protocol (MCP) server that provides an HTTP API and web interface for launching OB Studio through natural language commands.

## Features

- **REST API**: HTTP endpoints for executing MCP commands
- **Web Interface**: User-friendly HTML interface for sending commands
- **Natural Language Processing**: Accepts natural language commands like "launch obs studio"
- **Real-time Status**: Check server health and availability
- **CORS Enabled**: Can be integrated with frontend applications

## API Endpoints

### POST /api/mcp/execute
Execute a natural language command.

**Request Body:**
```json
{
    "command": "launch obs studio"
}
```

**Response:**
```json
{
    "success": true,
    "message": "Command executed successfully",
    "details": "Successfully launched and detached OBS Studio..."
}
```

### GET /api/mcp/status
Get server status and available commands.

**Response:**
```json
{
    "status": "running",
    "timestamp": "2025-09-11T10:30:00Z",
    "availableCommands": [
        "launch obs studio",
        "start obs",
        "open onbase studio"
    ]
}
```

### GET /health
Basic health check endpoint.

## Running the Web Client

1. **Build the project:**
   ```powershell
   cd MCPWebClient
   dotnet build
   ```

2. **Run the web server:**
   ```powershell
   dotnet run
   ```

3. **Access the web interface:**
   - Open your browser and navigate to: `https://localhost:5001` or `http://localhost:5000`
   - Or access the Swagger UI at: `https://localhost:5001/swagger`

## Natural Language Commands

The web client recognizes various natural language patterns for launching OB Studio:

- "launch obs studio"
- "start obs"
- "open onbase studio"
- "run obs studio"
- "execute obs"

## Architecture

The web client consists of:

1. **MCPService**: Manages communication with the MCP server process
2. **MCPController**: Provides REST API endpoints
3. **Static Web Interface**: HTML/JavaScript frontend for user interaction

## Dependencies

- ASP.NET Core 9.0
- System.Text.Json
- Swashbuckle.AspNetCore (for Swagger documentation)

## Integration Examples

### JavaScript/Fetch API
```javascript
const response = await fetch('/api/mcp/execute', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ command: 'launch obs studio' })
});
const result = await response.json();
```

### PowerShell
```powershell
$body = @{ command = "launch obs studio" } | ConvertTo-Json
Invoke-RestMethod -Uri "https://localhost:5001/api/mcp/execute" -Method POST -Body $body -ContentType "application/json"
```

### cURL
```bash
curl -X POST https://localhost:5001/api/mcp/execute \
  -H "Content-Type: application/json" \
  -d '{"command": "launch obs studio"}'
```

## Troubleshooting

1. **MCP Server not found**: Ensure the MCPServer project is built and the executable exists
2. **Permission errors**: Make sure the web client has permissions to start the MCP server process
3. **Port conflicts**: Change the ports in `appsettings.json` if needed
4. **CORS issues**: The default configuration allows all origins for development

## Security Considerations

- In production, configure CORS policy to restrict allowed origins
- Consider implementing authentication/authorization for the API endpoints
- Validate and sanitize input commands
- Run with appropriate user permissions
