# HKVC(Hack vscode) - OpenAI relay API Server

A VS Code extension that creates an OpenAI-compatible OpenAI relay API server using VS Code's built-in language models.

## Features

- 🚀 **OpenAI-compatible API**: Full compatibility with OpenAI's API
- 📡 **Streaming support**: Real-time streaming responses
- 🔗 **Multiple models**: Works with GitHub Copilot and other VS Code language models  
- ⚙️ **Easy management**: Start/stop server directly from VS Code
- 🔧 **Configurable**: Customize host and port settings

## Quick Start

1. Install the extension
2. Make sure GitHub Copilot (or other language models) are installed and authenticated
3. Open Command Palette (`Ctrl+Shift+P`) and run `HKVC: Start OpenAI relay API Server`
4. Server starts at `http://localhost:3000` by default

## API Usage

### Basic OpenAI relay
```bash
curl -X POST http://localhost:3000/v1/chat/completions 
  -H "Content-Type: application/json" 
  -d '{
    "model": "copilot-gpt-4o",
    "messages": [{"role": "user", "content": "Hello!"}]
  }'
```

### With Python OpenAI SDK
```python
from openai import OpenAI

client = OpenAI(base_url="http://localhost:3000/v1", api_key="not-needed")
response = client.chat.completions.create(
    model="copilot-gpt-4o",
    messages=[{"role": "user", "content": "Hello!"}]
)
print(response.choices[0].message.content)
```

## Commands

- `HKVC: Start OpenAI relay API Server` - Start the API server
- `HKVC: Stop OpenAI relay API Server` - Stop the API server
- `HKVC: Check Server Status` - View server status and endpoints
- `HKVC: Complete Chat` - Single OpenAI relay

## Configuration

```json
{
  "hkvc.server.port": 3000,
  "hkvc.server.host": "localhost"
}
```

## API Endpoints

- `GET /health` - Health check
- `GET /v1/models` - List available models
- `POST /v1/chat/completions` - OpenAI relays (OpenAI compatible)


## Requirements

- VS Code 1.103.0 or higher
- GitHub Copilot or other language model extensions

## Extension Settings

This extension contributes the following settings:

- `hkvc.server.port`: Port for the API server (default: 3000)
- `hkvc.server.host`: Host for the API server (default: localhost)

## Known Issues

- Server must be manually started/stopped via VS Code commands
- Model availability depends on installed VS Code language model extensions

## Release Notes

### 0.0.1

Initial release with OpenAI-compatible OpenAI relay API server.
