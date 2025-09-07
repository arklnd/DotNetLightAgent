import { Component, Input, Output, EventEmitter } from '@angular/core';

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
      alert(formatted);
    }
  }
}
