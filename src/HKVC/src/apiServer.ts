import express from 'express';
import cors from 'cors';
import * as vscode from 'vscode';
import * as http from 'http';

// OpenAI API compatible interfaces
interface Tool {
    type: 'function';
    function: {
        name: string;
        description?: string;
        parameters?: {
            type: 'object';
            properties: Record<string, any>;
            required?: string[];
        };
    };
}

interface ToolCall {
    id: string;
    type: 'function';
    function: {
        name: string;
        arguments: string;
    };
}

interface ChatCompletionMessage {
    role: 'system' | 'user' | 'assistant' | 'tool';
    content: string | null;
    name?: string;
    tool_calls?: ToolCall[];
    tool_call_id?: string;
}

interface ChatCompletionRequest {
    model: string;
    messages: ChatCompletionMessage[];
    temperature?: number;
    max_tokens?: number;
    stream?: boolean;
    top_p?: number;
    frequency_penalty?: number;
    presence_penalty?: number;
    tools?: Tool[];
    tool_choice?: 'auto' | 'none' | { type: 'function'; function: { name: string } };
}

interface ChatCompletionResponse {
    id: string;
    object: string;
    created: number;
    model: string;
    choices: Array<{
        index: number;
        message: ChatCompletionMessage;
        finish_reason: string;
    }>;
    usage: {
        prompt_tokens: number;
        completion_tokens: number;
        total_tokens: number;
    };
}

interface ChatCompletionStreamChunk {
    id: string;
    object: string;
    created: number;
    model: string;
    choices: Array<{
        index: number;
        delta: Partial<ChatCompletionMessage>;
        finish_reason: string | null;
    }>;
}

export class ChatCompletionAPIServer {
    private app: express.Application;
    private server: http.Server | null = null;
    private port: number;
    private host: string;
    private outputChannel: vscode.OutputChannel;

    constructor() {
        this.app = express();
        const config = vscode.workspace.getConfiguration('hkvc');
        this.port = config.get('server.port', 3000);
        this.host = config.get('server.host', 'localhost');
        this.outputChannel = vscode.window.createOutputChannel('HKVC API Server');
        this.setupMiddleware();
        this.setupRoutes();
    }

    private setupMiddleware(): void {
        this.app.use(cors());
        this.app.use(express.json({ limit: '10mb' }));
        
        // Request logging middleware
        this.app.use((req: express.Request, res: express.Response, next: express.NextFunction) => {
            this.outputChannel.appendLine(`${new Date().toISOString()} - ${req.method} ${req.path}`);
            next();
        });
    }

    private setupRoutes(): void {
        // Health check endpoint
        this.app.get('/health', (req: express.Request, res: express.Response) => {
            res.json({ status: 'ok', timestamp: new Date().toISOString() });
        });

        // Models endpoint (OpenAI compatible)
        this.app.get('/v1/models', async (req: express.Request, res: express.Response) => {
            try {
                const allAvailableModels = await vscode.lm.selectChatModels();
                const models = allAvailableModels.map(model => ({
                    id: `${model.vendor}-${model.family}`,
                    object: 'model',
                    created: Math.floor(Date.now() / 1000),
                    owned_by: model.vendor,
                    permission: [],
                    root: `${model.vendor}-${model.family}`,
                    parent: null
                }));

                res.json({
                    object: 'list',
                    data: models
                });
            } catch (error) {
                this.outputChannel.appendLine(`Error listing models: ${error}`);
                res.status(500).json({ error: 'Failed to list models' });
            }
        });

        // Chat completions endpoint (OpenAI compatible)
        this.app.post('/v1/chat/completions', async (req: express.Request, res: express.Response) => {
            try {
                await this.handleChatCompletion(req, res);
            } catch (error) {
                this.outputChannel.appendLine(`Error in chat completion: ${error}`);
                res.status(500).json({
                    error: {
                        message: 'Internal server error',
                        type: 'internal_error',
                        code: 'internal_error'
                    }
                });
            }
        });

        // Root endpoint
        this.app.get('/', (req: express.Request, res: express.Response) => {
            res.json({
                message: 'HKVC OpenAI relay API Server',
                version: '1.0.0',
                endpoints: {
                    health: '/health',
                    models: '/v1/models',
                    chat_completions: '/v1/chat/completions'
                }
            });
        });
    }

    private async handleChatCompletion(req: express.Request, res: express.Response): Promise<void> {
        const body: ChatCompletionRequest = req.body;

        // Validate request
        if (!body.messages || !Array.isArray(body.messages) || body.messages.length === 0) {
            res.status(400).json({
                error: {
                    message: 'Messages array is required and must not be empty',
                    type: 'invalid_request_error',
                    code: 'invalid_request'
                }
            });
            return;
        }

        try {
            // Get available models
            const allAvailableModels = await vscode.lm.selectChatModels();
            
            if (allAvailableModels.length === 0) {
                res.status(503).json({
                    error: {
                        message: 'No language models available',
                        type: 'service_unavailable',
                        code: 'no_models_available'
                    }
                });
                return;
            }

            // Find suitable model
            let selectedModel = allAvailableModels[0]; // Default to first available
            
            if (body.model) {
                const requestedModel = allAvailableModels.find(m => 
                    `${m.vendor}-${m.family}` === body.model ||
                    m.name === body.model ||
                    m.family === body.model
                );
                if (requestedModel) {
                    selectedModel = requestedModel;
                }
            }

            // Convert messages to VS Code format and handle tools
            const { vscodeMessages, hasTools } = this.convertMessagesToVSCode(body.messages, body.tools);

            const requestId = this.generateId();
            const created = Math.floor(Date.now() / 1000);
            const modelId = `${selectedModel.vendor}-${selectedModel.family}`;

            this.outputChannel.appendLine(`Processing chat completion with model: ${modelId}${hasTools ? ' (with tools)' : ''}`);
            this.outputChannel.appendLine(`Request contains ${vscodeMessages.length} messages`);

            if (body.stream) {
                // Handle streaming response
                res.setHeader('Content-Type', 'text/event-stream');
                res.setHeader('Cache-Control', 'no-cache');
                res.setHeader('Connection', 'keep-alive');

                try {
                    this.outputChannel.appendLine(`Sending request to model ${modelId} with ${vscodeMessages.length} messages`);
                    const chatResponse = await selectedModel.sendRequest(vscodeMessages, {});
                    this.outputChannel.appendLine(`Successfully received response from model ${modelId}`);
                    
                    let isFirstChunk = true;
                    let accumulatedContent = '';
                    
                    for await (const fragment of chatResponse.text) {
                        if (fragment) {
                            accumulatedContent += fragment;
                            const chunk: ChatCompletionStreamChunk = {
                                id: requestId,
                                object: 'chat.completion.chunk',
                                created,
                                model: modelId,
                                choices: [{
                                    index: 0,
                                    delta: isFirstChunk ? { role: 'assistant', content: fragment } : { content: fragment },
                                    finish_reason: null
                                }]
                            };

                            res.write(`data: ${JSON.stringify(chunk)}\n\n`);
                            isFirstChunk = false;
                        }
                    }

                    // Check for tool calls in the final response
                    const toolCalls = hasTools ? this.extractToolCalls(accumulatedContent) : [];
                    
                    if (toolCalls.length > 0) {
                        // Send tool calls chunk
                        const toolChunk: ChatCompletionStreamChunk = {
                            id: requestId,
                            object: 'chat.completion.chunk',
                            created,
                            model: modelId,
                            choices: [{
                                index: 0,
                                delta: { tool_calls: toolCalls },
                                finish_reason: null
                            }]
                        };
                        res.write(`data: ${JSON.stringify(toolChunk)}\n\n`);
                    }

                    // Send final chunk
                    const finalChunk: ChatCompletionStreamChunk = {
                        id: requestId,
                        object: 'chat.completion.chunk',
                        created,
                        model: modelId,
                        choices: [{
                            index: 0,
                            delta: {},
                            finish_reason: toolCalls.length > 0 ? 'tool_calls' : 'stop'
                        }]
                    };

                    res.write(`data: ${JSON.stringify(finalChunk)}\n\n`);
                    res.write('data: [DONE]\n\n');
                    res.end();

                } catch (error) {
                    this.outputChannel.appendLine(`Streaming error: ${error}`);
                    res.write(`data: ${JSON.stringify({ error: 'Stream error' })}\n\n`);
                    res.end();
                }

            } else {
                // Handle non-streaming response
                try {
                    this.outputChannel.appendLine(`Sending non-streaming request to model ${modelId} with ${vscodeMessages.length} messages`);
                    const chatResponse = await selectedModel.sendRequest(vscodeMessages, {});
                    this.outputChannel.appendLine(`Successfully received non-streaming response from model ${modelId}`);
                
                    let fullResponse = '';
                    for await (const fragment of chatResponse.text) {
                        fullResponse += fragment;
                    }

                    // Check for tool calls in the response
                    const toolCalls = hasTools ? this.extractToolCalls(fullResponse) : [];
                    const finishReason = toolCalls.length > 0 ? 'tool_calls' : 'stop';

                    const responseMessage: ChatCompletionMessage = {
                        role: 'assistant',
                        content: toolCalls.length > 0 ? null : fullResponse
                    };

                    if (toolCalls.length > 0) {
                        responseMessage.tool_calls = toolCalls;
                    }

                    const response: ChatCompletionResponse = {
                        id: requestId,
                        object: 'chat.completion',
                        created,
                        model: modelId,
                        choices: [{
                            index: 0,
                            message: responseMessage,
                            finish_reason: finishReason
                        }],
                        usage: {
                            prompt_tokens: this.estimateTokens(body.messages.map(m => m.content || '').join(' ')),
                            completion_tokens: this.estimateTokens(fullResponse),
                            total_tokens: 0 // Will be calculated
                        }
                    };

                    response.usage.total_tokens = response.usage.prompt_tokens + response.usage.completion_tokens;

                    res.json(response);
                } catch (error) {
                    this.outputChannel.appendLine(`Non-streaming error: ${error}`);
                    res.status(500).json({
                        error: {
                            message: 'Internal server error during non-streaming response',
                            type: 'internal_error'
                        }
                    });
                }
            }

        } catch (error) {
            this.outputChannel.appendLine(`Model request error: ${error}`);
            
            if (error instanceof vscode.LanguageModelError) {
                res.status(400).json({
                    error: {
                        message: error.message,
                        type: 'model_error',
                        code: error.code || 'model_request_failed'
                    }
                });
            } else {
                res.status(500).json({
                    error: {
                        message: 'Failed to process chat completion',
                        type: 'internal_error',
                        code: 'completion_failed'
                    }
                });
            }
        }
    }

    private generateId(): string {
        return 'chatcmpl-' + Math.random().toString(36).substring(2, 15) + Math.random().toString(36).substring(2, 15);
    }

    private estimateTokens(text: string): number {
        // Simple token estimation (roughly 4 characters per token)
        return Math.ceil(text.length / 4);
    }

    private convertMessagesToVSCode(messages: ChatCompletionMessage[], tools?: Tool[]): { vscodeMessages: vscode.LanguageModelChatMessage[], hasTools: boolean } {
        const hasTools = Boolean(tools && tools.length > 0);
        
        // If we have tools, add them as a system message at the beginning
        const vscodeMessages: vscode.LanguageModelChatMessage[] = [];
        
        if (hasTools) {
            const toolsPrompt = this.createToolsSystemPrompt(tools!);
            vscodeMessages.push(new vscode.LanguageModelChatMessage(vscode.LanguageModelChatMessageRole.User, toolsPrompt));
        }

        // Convert each message
        for (const msg of messages) {
            // Ensure content is a valid string
            const content = this.sanitizeContent(msg.content);
            
            switch (msg.role) {
                case 'user':
                    vscodeMessages.push(new vscode.LanguageModelChatMessage(vscode.LanguageModelChatMessageRole.User, content));
                    break;
                case 'assistant':
                    if (msg.tool_calls && msg.tool_calls.length > 0) {
                        // Convert tool calls to text format
                        const toolCallsText = this.formatToolCallsAsText(msg.tool_calls);
                        vscodeMessages.push(new vscode.LanguageModelChatMessage(vscode.LanguageModelChatMessageRole.Assistant, toolCallsText));
                    } else {
                        vscodeMessages.push(new vscode.LanguageModelChatMessage(vscode.LanguageModelChatMessageRole.Assistant, content));
                    }
                    break;
                case 'tool':
                    // Convert tool result to user message with special formatting
                    const toolResult = `Tool "${msg.name}" (ID: ${msg.tool_call_id}) returned: ${content}`;
                    vscodeMessages.push(new vscode.LanguageModelChatMessage(vscode.LanguageModelChatMessageRole.User, toolResult));
                    break;
                case 'system':
                    vscodeMessages.push(new vscode.LanguageModelChatMessage(vscode.LanguageModelChatMessageRole.User, `System: ${content}`));
                    break;
                default:
                    vscodeMessages.push(new vscode.LanguageModelChatMessage(vscode.LanguageModelChatMessageRole.User, content));
            }
        }

        return { vscodeMessages, hasTools };
    }

    private sanitizeContent(content: string | null | undefined): string {
        if (content === null || content === undefined) {
            return '';
        }
        
        if (typeof content !== 'string') {
            // If content is not a string, try to convert it
            try {
                return String(content);
            } catch (error) {
                this.outputChannel.appendLine(`Warning: Could not convert content to string: ${error}`);
                return '';
            }
        }
        
        // Ensure content is valid UTF-8 and doesn't contain problematic characters
        return content.replace(/[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]/g, '');
    }

    private createToolsSystemPrompt(tools: Tool[]): string {
        const toolDescriptions = tools.map(tool => {
            const params = tool.function.parameters ? JSON.stringify(tool.function.parameters, null, 2) : '{}';
            return `- ${tool.function.name}: ${tool.function.description || 'No description'}\n  Parameters: ${params}`;
        }).join('\n');

        return `You have access to the following tools/functions. When you need to call a function, respond with a JSON object in this exact format:

{
  "tool_calls": [
    {
      "id": "call_<unique_id>",
      "type": "function",
      "function": {
        "name": "<function_name>",
        "arguments": "<json_string_of_arguments>"
      }
    }
  ]
}

Available tools:
${toolDescriptions}

IMPORTANT: 
- Only call functions when explicitly requested or when you need specific information
- Provide the function arguments as a JSON string
- Use unique IDs for each tool call (e.g., call_1, call_2, etc.)
- If you don't need to call any functions, respond normally without the tool_calls JSON`;
    }

    private formatToolCallsAsText(toolCalls: ToolCall[]): string {
        const formattedCalls = toolCalls.map(call => 
            `Function call: ${call.function.name}(${call.function.arguments})`
        ).join('\n');
        
        return `I need to call the following functions:\n${formattedCalls}`;
    }

    private extractToolCalls(content: string): ToolCall[] {
        try {
            // Add debug logging
            this.outputChannel.appendLine(`Attempting to extract tool calls from content (length: ${content.length})`);
            
            // Look for JSON objects containing tool_calls
            // Use a more precise approach to extract valid JSON
            const lines = content.split('\n');
            let jsonStart = -1;
            let braceCount = 0;
            let jsonEnd = -1;
            
            // Find the start of a JSON object that might contain tool_calls
            for (let i = 0; i < lines.length; i++) {
                const line = lines[i].trim();
                if (line === '{' || line.startsWith('{')) {
                    jsonStart = i;
                    braceCount = (line.match(/\{/g) || []).length - (line.match(/\}/g) || []).length;
                    
                    // Check if this could be a tool_calls JSON by looking ahead
                    const nextFewLines = lines.slice(i, Math.min(i + 10, lines.length)).join('');
                    if (nextFewLines.includes('tool_calls')) {
                        this.outputChannel.appendLine(`Found potential tool_calls JSON starting at line ${i}`);
                        break;
                    }
                }
            }
            
            if (jsonStart === -1) {
                this.outputChannel.appendLine('No tool_calls JSON found');
                return [];
            }
            
            // Find the end of the JSON object by counting braces
            for (let i = jsonStart + 1; i < lines.length && braceCount > 0; i++) {
                const line = lines[i];
                braceCount += (line.match(/\{/g) || []).length - (line.match(/\}/g) || []).length;
                if (braceCount === 0) {
                    jsonEnd = i;
                    break;
                }
            }
            
            if (jsonEnd === -1) {
                this.outputChannel.appendLine('Could not find end of JSON object');
                return [];
            }
            
            // Extract and parse the JSON
            const jsonText = lines.slice(jsonStart, jsonEnd + 1).join('\n');
            this.outputChannel.appendLine(`Extracted JSON: ${jsonText}`);
            
            const parsed = JSON.parse(jsonText);
            
            if (parsed.tool_calls && Array.isArray(parsed.tool_calls)) {
                this.outputChannel.appendLine(`Successfully parsed ${parsed.tool_calls.length} tool calls`);
                return parsed.tool_calls.map((call: any, index: number) => ({
                    id: call.id || `call_${Date.now()}_${index}`,
                    type: 'function',
                    function: {
                        name: call.function.name,
                        arguments: typeof call.function.arguments === 'string' 
                            ? call.function.arguments 
                            : JSON.stringify(call.function.arguments)
                    }
                }));
            }
        } catch (error) {
            this.outputChannel.appendLine(`Error parsing tool calls: ${error}`);
            this.outputChannel.appendLine(`Content that failed to parse: ${content}`);
            // Fallback to the old regex method but with better error handling
            try {
                const jsonMatch = content.match(/\{[^{}]*"tool_calls"[^{}]*\}/);
                if (jsonMatch) {
                    this.outputChannel.appendLine(`Trying fallback regex with match: ${jsonMatch[0]}`);
                    const parsed = JSON.parse(jsonMatch[0]);
                    if (parsed.tool_calls && Array.isArray(parsed.tool_calls)) {
                        return parsed.tool_calls.map((call: any, index: number) => ({
                            id: call.id || `call_${Date.now()}_${index}`,
                            type: 'function',
                            function: {
                                name: call.function.name,
                                arguments: typeof call.function.arguments === 'string' 
                                    ? call.function.arguments 
                                    : JSON.stringify(call.function.arguments)
                            }
                        }));
                    }
                }
            } catch (fallbackError) {
                this.outputChannel.appendLine(`Fallback parsing also failed: ${fallbackError}`);
            }
        }

        return [];
    }

    public async start(): Promise<void> {
        return new Promise((resolve, reject) => {
            try {
                this.server = this.app.listen(this.port, this.host, () => {
                    const message = `HKVC API Server started on http://${this.host}:${this.port}`;
                    this.outputChannel.appendLine(message);
                    this.outputChannel.show();
                    vscode.window.showInformationMessage(message);
                    resolve();
                });

                if (this.server) {
                    this.server.on('error', (error: any) => {
                        if (error.code === 'EADDRINUSE') {
                            const errorMsg = `Port ${this.port} is already in use`;
                            this.outputChannel.appendLine(errorMsg);
                            vscode.window.showErrorMessage(errorMsg);
                        } else {
                            this.outputChannel.appendLine(`Server error: ${error.message}`);
                            vscode.window.showErrorMessage(`Server error: ${error.message}`);
                        }
                        reject(error);
                    });
                }

            } catch (error) {
                reject(error);
            }
        });
    }

    public async stop(): Promise<void> {
        return new Promise((resolve) => {
            if (this.server) {
                this.server.close(() => {
                    const message = 'HKVC API Server stopped';
                    this.outputChannel.appendLine(message);
                    vscode.window.showInformationMessage(message);
                    this.server = null;
                    resolve();
                });
            } else {
                resolve();
            }
        });
    }

    public isRunning(): boolean {
        return this.server !== null && this.server.listening;
    }

    public getServerInfo(): { host: string; port: number; running: boolean } {
        return {
            host: this.host,
            port: this.port,
            running: this.isRunning()
        };
    }
}
