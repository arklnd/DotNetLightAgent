import { Component, Input, Output, EventEmitter } from '@angular/core';
import { HttpClient } from '@angular/common/http';

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

  constructor(private http: HttpClient) {}

  close() {
    this.closeDrawer.emit();
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
    const apiUrl = 'http://localhost:5230/api/automation/steps';
    const body = { instructions: formatted };
    this.http.post(apiUrl, body).subscribe({
      next: (result) => {
        console.log('Orchestrator response:', result);
        alert('Steps sent to orchestrator. Check console for response.');
      },
      error: (err) => {
        console.error('Error sending steps to orchestrator:', err);
        alert('Failed to send steps to orchestrator.');
      }
    });
  }
}
