import { Component, Input, Output, EventEmitter } from '@angular/core';
import { HttpClient, HttpEventType } from '@angular/common/http';

@Component({
  selector: 'app-bottom-drawer',
  templateUrl: './bottom-drawer.component.html',
  styleUrls: ['./bottom-drawer.component.scss']
})
export class BottomDrawerComponent {
  @Input() issueKey: string | null = null;
  @Input() steps: string | null = null;
  @Input() loading: boolean = false;
  @Output() closeDrawer = new EventEmitter<void>();

  orchestratorResponse: any[] = [];
  orchestratorLoading: boolean = false;

  constructor(private http: HttpClient) {}

  // Test method to verify rendering
  testOrchestratorResponse() {
    this.orchestratorResponse = [
      {
        id: 1,
        timestamp: new Date().toISOString(),
        type: 'status',
        content: 'Starting browser automation workflow',
        status: 'in_progress',
        sessionId: 'test-session'
      },
      {
        id: 2,
        timestamp: new Date().toISOString(),
        type: 'status',
        content: 'Page loaded successfully, taking snapshot to see form fields',
        status: 'success',
        sessionId: 'test-session'
      },
      {
        id: 3,
        timestamp: new Date().toISOString(),
        type: 'status',
        content: 'Document not found in search results',
        status: 'error',
        sessionId: 'test-session'
      }
    ];
    this.orchestratorLoading = false;
    console.log('Test: orchestratorResponse set to:', this.orchestratorResponse);
  }

  close() {
    this.closeDrawer.emit();
    // Clear orchestrator response when closing
    this.orchestratorResponse = [];
    this.orchestratorLoading = false;
  }

  // TrackBy function for performance optimization
  trackByResponseId(index: number, item: any): any {
    return item.id || index;
  }

  // Get status icon based on status
  getStatusIcon(status: string): string {
    switch (status) {
      case 'success': return 'check_circle';
      case 'error': return 'error';
      case 'in_progress': return 'schedule';
      case 'warning': return 'warning';
      default: return 'info';
    }
  }

  // Get status icon CSS class
  getStatusIconClass(status: string): string {
    switch (status) {
      case 'success': return 'icon-success';
      case 'error': return 'icon-error';
      case 'in_progress': return 'icon-progress';
      case 'warning': return 'icon-warning';
      default: return 'icon-info';
    }
  }

  // Format timestamp for display
  formatTimestamp(timestamp: string): string {
    const date = new Date(timestamp);
    return date.toLocaleTimeString('en-US', {
      hour12: false,
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    });
  }

  showStepsAlert() {
    if (this.steps) {
      // Split steps into lines, remove leading # and whitespace, and format
      const lines = this.steps.split(/\r?\n/)
        .map(line => line.replace(/^\s*#\s*/, '').trim())
        .filter(line => line.length > 0);
      const formatted = lines.map((line, idx) => `Instruction step ${idx + 1}: ${line}`).join('\n');
      // Send to orchestrator API
      this.sendStepsToOrchestrator(formatted);
    }
  }

  sendStepsToOrchestrator(formatted: string) {
    this.orchestratorLoading = true;
    this.orchestratorResponse = [];

    console.log('Debug: orchestratorResponse cleared, orchestratorLoading set to:', this.orchestratorLoading);
    console.log('Sending formatted message:', formatted);

    const apiUrl = 'https://localhost:7000/api/Chat/stream';
    const body = {
      message: formatted,
      sessionId: `orchestrator-${Date.now()}`
    };

    // Use HttpClient with observe: 'events' and responseType: 'text' for streaming
    this.http.post(apiUrl, body, {
      observe: 'events',
      responseType: 'text',
      reportProgress: true
    }).subscribe({
      next: (event: any) => {
        console.log('HTTP Event received:', event.type, event);

        if (event.type === HttpEventType.DownloadProgress) {
          // Parse the partial response for streaming data
          const responseText = event.partialText || event.body || '';
          console.log('Download progress - responseText:', responseText);
          this.parseStreamingResponse(responseText);
        } else if (event.type === HttpEventType.Response) {
          // Final response
          this.orchestratorLoading = false;
          const responseText = event.body || '';
          console.log('Final response - responseText:', responseText);
          this.parseStreamingResponse(responseText);
        }
      },
      error: (error) => {
        console.error('Error sending steps to orchestrator:', error);
        this.orchestratorLoading = false;
        this.orchestratorResponse.push('Error: Failed to connect to orchestrator service');
      }
    });
  }

  private parseStreamingResponse(responseText: string) {
    // Split response by lines and parse each line as potential JSON
    const lines = responseText.split('\n');

    for (const line of lines) {
      const trimmedLine = line.trim();
      if (trimmedLine.startsWith('data: ')) {
        try {
          const jsonData = JSON.parse(trimmedLine.substring(6));
          console.log('Parsed streaming data:', jsonData);

          // Check for both lowercase and uppercase properties to handle different API responses
          const content = jsonData.content || jsonData.Content;
          const isComplete = jsonData.isComplete || jsonData.IsComplete;

          if (content) {
            // Try to parse the nested JSON content
            let parsedContent;
            try {
              parsedContent = JSON.parse(content);
            } catch {
              // If parsing fails, treat content as plain text
              parsedContent = { content: content, type: 'text', status: 'info' };
            }

            // Create a response object with metadata
            const responseItem = {
              id: Date.now() + Math.random(), // Unique ID for tracking
              timestamp: jsonData.Timestamp || new Date().toISOString(),
              type: parsedContent.type || 'text',
              content: parsedContent.content || content,
              status: parsedContent.status || 'info',
              sessionId: jsonData.SessionId
            };

            // Check if this response already exists (avoid duplicates)
            const exists = this.orchestratorResponse.some(existing =>
              existing.content === responseItem.content &&
              existing.timestamp === responseItem.timestamp
            );

            if (!exists) {
              this.orchestratorResponse.push(responseItem);
              console.log('Added response:', responseItem);
            }
          }

          if (isComplete) {
            this.orchestratorLoading = false;
            console.log('Stream completed');
          }
        } catch (error) {
          // Ignore parsing errors for malformed lines
          console.warn('Could not parse streaming data:', trimmedLine, error);
        }
      } else if (trimmedLine.length > 0) {
        // Log any non-SSE lines for debugging
        console.log('Non-SSE line received:', trimmedLine);
      }
    }
  }
}
