import * as vscode from 'vscode';
import { ChatCompletionAPIServer } from './apiServer';

let apiServer: ChatCompletionAPIServer | null = null;

// This method is called when extension is activated
// Extension is activated the very first time the command is executed
export function activate(context: vscode.ExtensionContext) {

	// Use the console to output diagnostic information (console.log) and errors (console.error)
	// This line of code will only be executed once when extension is activated
	console.log('Congratulations, extension "hkvc" is now active!');

	// The command has been defined in the package.json file
	// Now provide the implementation of the command with registerCommand
	// The commandId parameter must match the command field in package.json
	const disposable_hw = vscode.commands.registerCommand('hkvc.helloWorld', () => {
		// The code you place here will be executed every time command is executed
		// Display a message box to the user
		vscode.window.showInformationMessage('Hello World Arijit from HKVC!');
	});

	// Register the completeChat command
	const disposable_completeChat = vscode.commands.registerCommand('hkvc.completeChat', async () => {
		try {
			// Debug: List all available models
			const allAvailableModels = await vscode.lm.selectChatModels();
			console.log('All available models:', allAvailableModels.map(m => ({ vendor: m.vendor, family: m.family, name: m.name })));
			
			if (allAvailableModels.length === 0) {
				vscode.window.showErrorMessage('No language models available at all. Please check VS Code setup and extensions.');
				return;
			}

			// Try to find any Copilot model
			const copilotModels = allAvailableModels.filter(m => m.vendor === 'copilot');
			if (copilotModels.length === 0) {
				vscode.window.showErrorMessage('No GitHub Copilot models available. Please ensure GitHub Copilot is installed and authenticated.');
				return;
			}

			// Use the first available Copilot model
			const model = copilotModels[0];
			console.log('Using model:', { vendor: model.vendor, family: model.family, name: model.name });
			
			// Get user input
			const userPrompt = await vscode.window.showInputBox({
				prompt: 'Enter your message for the chat completion:',
				placeHolder: 'Type your message here...'
			});

			if (!userPrompt) {
				return; // User cancelled
			}

			// Create chat messages
			const messages = [
				vscode.LanguageModelChatMessage.User(userPrompt)
			];

			// Show progress while processing
			await vscode.window.withProgress({
				location: vscode.ProgressLocation.Notification,
				title: 'Getting chat completion...',
				cancellable: true
			}, async (progress, token) => {
				try {
					// Send request to language model
					const chatResponse = await model.sendRequest(
						messages,
						{},
						token
					);

					let fullResponse = '';
					
					// Collect the streaming response
					for await (const fragment of chatResponse.text) {
						if (token.isCancellationRequested) {
							return;
						}
						fullResponse += fragment;
					}

					// Display the response
					const outputChannel = vscode.window.createOutputChannel('HKVC Chat Completion');
					outputChannel.clear();
					outputChannel.appendLine('=== CHAT COMPLETION DEMO ===');
					outputChannel.appendLine('');
					outputChannel.appendLine('User Input:');
					outputChannel.appendLine(userPrompt);
					outputChannel.appendLine('');
					outputChannel.appendLine('AI Response:');
					outputChannel.appendLine(fullResponse);
					outputChannel.appendLine('');
					outputChannel.appendLine('=== END ===');
					outputChannel.show();

					// Also log to debug console
					console.log('Chat Completion - User:', userPrompt);
					console.log('Chat Completion - Response:', fullResponse);

					vscode.window.showInformationMessage('Chat completion displayed in Output panel!');

				} catch (err) {
					if (err instanceof vscode.LanguageModelError) {
						console.error('Language Model Error:', err.message, err.code, err.cause);
						vscode.window.showErrorMessage(`Language Model Error: ${err.message}`);
					} else {
						console.error('Chat completion error:', err);
						vscode.window.showErrorMessage(`Error: ${err instanceof Error ? err.message : 'Unknown error'}`);
					}
				}
			});

		} catch (err) {
			console.error('Failed to initialize chat completion:', err);
			vscode.window.showErrorMessage('Failed to initialize chat completion');
		}
	});

	// Register the start server command
	const disposable_startServer = vscode.commands.registerCommand('hkvc.startServer', async () => {
		try {
			if (apiServer && apiServer.isRunning()) {
				const serverInfo = apiServer.getServerInfo();
				vscode.window.showWarningMessage(`Server is already running on http://${serverInfo.host}:${serverInfo.port}`);
				return;
			}

			// Check if language models are available
			const allAvailableModels = await vscode.lm.selectChatModels();
			if (allAvailableModels.length === 0) {
				vscode.window.showErrorMessage('No language models available. Please check your VS Code setup and extensions before starting the server.');
				return;
			}

			apiServer = new ChatCompletionAPIServer();
			await apiServer.start();
			
			const serverInfo = apiServer.getServerInfo();
			const message = `OpenAI relay API Server started successfully!\n\nEndpoints:\n• Health: http://${serverInfo.host}:${serverInfo.port}/health\n• Models: http://${serverInfo.host}:${serverInfo.port}/v1/models\n• Chat: http://${serverInfo.host}:${serverInfo.port}/v1/chat/completions\n\nThis server is OpenAI API compatible.`;
			
			vscode.window.showInformationMessage(message, 'Copy Base URL').then(selection => {
				if (selection === 'Copy Base URL') {
					vscode.env.clipboard.writeText(`http://${serverInfo.host}:${serverInfo.port}`);
					vscode.window.showInformationMessage('Base URL copied to clipboard!');
				}
			});

		} catch (err) {
			console.error('Failed to start API server:', err);
			vscode.window.showErrorMessage(`Failed to start API server: ${err instanceof Error ? err.message : 'Unknown error'}`);
		}
	});

	// Register the stop server command
	const disposable_stopServer = vscode.commands.registerCommand('hkvc.stopServer', async () => {
		try {
			if (!apiServer || !apiServer.isRunning()) {
				vscode.window.showInformationMessage('Server is not running.');
				return;
			}

			await apiServer.stop();
			apiServer = null;

		} catch (err) {
			console.error('Failed to stop API server:', err);
			vscode.window.showErrorMessage(`Failed to stop API server: ${err instanceof Error ? err.message : 'Unknown error'}`);
		}
	});

	// Register the server status command
	const disposable_serverStatus = vscode.commands.registerCommand('hkvc.serverStatus', async () => {
		try {
			if (!apiServer) {
				vscode.window.showInformationMessage('Server is not initialized.');
				return;
			}

			const serverInfo = apiServer.getServerInfo();
			const status = serverInfo.running ? 'Running' : 'Stopped';
			const message = `Server Status: ${status}\nHost: ${serverInfo.host}\nPort: ${serverInfo.port}`;
			
			if (serverInfo.running) {
				vscode.window.showInformationMessage(message, 'Copy Base URL', 'Open in Browser').then(selection => {
					if (selection === 'Copy Base URL') {
						vscode.env.clipboard.writeText(`http://${serverInfo.host}:${serverInfo.port}`);
						vscode.window.showInformationMessage('Base URL copied to clipboard!');
					} else if (selection === 'Open in Browser') {
						vscode.env.openExternal(vscode.Uri.parse(`http://${serverInfo.host}:${serverInfo.port}`));
					}
				});
			} else {
				vscode.window.showInformationMessage(message);
			}

			// Also check available models
			const allAvailableModels = await vscode.lm.selectChatModels();
			console.log('Available models:', allAvailableModels.map(m => ({ vendor: m.vendor, family: m.family, name: m.name })));

		} catch (err) {
			console.error('Failed to get server status:', err);
			vscode.window.showErrorMessage(`Failed to get server status: ${err instanceof Error ? err.message : 'Unknown error'}`);
		}
	});

	context.subscriptions.push(disposable_hw);
	context.subscriptions.push(disposable_completeChat);
	context.subscriptions.push(disposable_startServer);
	context.subscriptions.push(disposable_stopServer);
	context.subscriptions.push(disposable_serverStatus);
}

// This method is called when extension is deactivated
export async function deactivate() {
	if (apiServer && apiServer.isRunning()) {
		await apiServer.stop();
	}
}
