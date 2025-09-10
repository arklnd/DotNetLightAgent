# DotNet Light Agent REST API

This project has been refactored from a console application to a REST API with proper dependency injection and service architecture.

## Architecture Overview

### Services
- **IAgentService**: Interface defining agent operations including chat functionality
- **AgentService**: Implementation that handles AI chat interactions with Semantic Kernel
- **ChatController**: REST API controller exposing chat endpoints

### Key Features
- ✅ **Dependency Injection**: Proper service registration and DI container setup
- ✅ **Session Management**: Multiple conversation sessions with separate histories
- ✅ **Message Streaming**: Real-time streaming responses via Server-Sent Events
- ✅ **Function Calling**: Integration with Semantic Kernel plugins and MCP tools
- ✅ **Error Handling**: Comprehensive error handling and logging
- ✅ **CORS Support**: Configured for web frontend integration
- ✅ **Swagger Documentation**: Auto-generated API documentation

## API Endpoints

### Chat Operations

#### `POST /api/chat/message`
Send a message and get a complete response.

**Request Body:**
```json
{
  "message": "Your message here",
  "sessionId": "optional-session-id"
}
```

**Response:**
```json
{
  "message": "Agent response",
  "sessionId": "session-id-used",
  "timestamp": "2025-09-10T10:30:00Z"
}
```

#### `POST /api/chat/stream`
Send a message and stream the response in real-time using Server-Sent Events.

**Request Body:**
```json
{
  "message": "Your message here",
  "sessionId": "optional-session-id"
}
```

**Response:** Server-Sent Events stream with chunks:
```
data: {"content":"Hello","role":"assistant","sessionId":"abc123","isComplete":false}

data: {"content":" there!","role":"assistant","sessionId":"abc123","isComplete":false}

data: {"content":null,"role":null,"sessionId":"abc123","isComplete":true}
```

#### `GET /api/chat/history`
Get conversation history for a session.

**Query Parameters:**
- `sessionId` (optional): Session ID to retrieve history for

**Response:**
```json
{
  "sessionId": "session-id",
  "messages": [
    {
      "role": "user",
      "content": "Hello",
      "timestamp": "2025-09-10T10:30:00Z"
    },
    {
      "role": "assistant", 
      "content": "Hi there!",
      "timestamp": "2025-09-10T10:30:01Z"
    }
  ]
}
```

#### `POST /api/chat/clear`
Clear conversation history for a session.

**Request Body:**
```json
{
  "sessionId": "optional-session-id"
}
```

#### `GET /api/chat/health`
Health check endpoint.

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- Ollama or compatible LLM endpoint running on `http://localhost:3000/v1/`

### Running the Application

1. **Restore packages:**
   ```bash
   dotnet restore
   ```

2. **Run the application:**
   ```bash
   dotnet run --project src/DotNetLightAgent/DotNetLightAgent.csproj
   ```

3. **Access the API:**
   - **Base URL:** `https://localhost:7000` or `http://localhost:5000`
   - **Swagger UI:** `https://localhost:7000/swagger`

### Configuration

The agent service is configured with:
- **Model ID:** `copilot-gpt-5` (configurable)
- **Endpoint:** `http://localhost:3000/v1/` (Ollama default)
- **Timeout:** 5 minutes for long-running operations
- **Context Size:** 4096 tokens
- **Max Tokens:** 512 per response

### Session Management

- Sessions are identified by `sessionId` parameter
- If no `sessionId` is provided, a new GUID is generated
- Each session maintains separate conversation history
- Default session ID is "default" when not specified
- Sessions are stored in memory (consider Redis for production)

### Streaming

The streaming endpoint uses Server-Sent Events (SSE) for real-time response delivery:
- Content-Type: `text/event-stream`
- Each chunk contains partial response content
- Final chunk has `isComplete: true`
- Supports cancellation via client disconnect

### Testing

Use the provided `test-api.http` file with REST Client extension in VS Code or any HTTP client:

```http
POST https://localhost:7000/api/chat/stream
Content-Type: application/json

{
  "message": "Explain dependency injection in .NET",
  "sessionId": "demo-session"
}
```

### Integration with Frontend

The API is CORS-enabled and can be consumed by web frontends:

```javascript
// Example: Streaming chat
async function streamChat(message, sessionId) {
  const response = await fetch('/api/chat/stream', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ message, sessionId })
  });

  const reader = response.body.getReader();
  const decoder = new TextDecoder();

  while (true) {
    const { done, value } = await reader.read();
    if (done) break;

    const chunk = decoder.decode(value);
    const lines = chunk.split('\n');
    
    for (const line of lines) {
      if (line.startsWith('data: ')) {
        const data = JSON.parse(line.slice(6));
        if (data.isComplete) return;
        console.log(data.content); // Process chunk
      }
    }
  }
}
```

## Migration from Console App

The refactoring moved:

1. **Agent Logic**: From `Program.cs` → `AgentService.cs`
2. **Configuration**: From inline setup → DI container registration
3. **Interaction**: From console I/O → HTTP endpoints
4. **State Management**: From single conversation → multi-session support
5. **Output**: From console streaming → HTTP streaming responses

The core AI functionality remains unchanged, but is now accessible via HTTP APIs with proper separation of concerns.
