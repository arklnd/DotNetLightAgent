import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-loader',
  template: `
    <div class="loader-overlay" *ngIf="show">
      <mat-progress-spinner mode="indeterminate" color="primary"></mat-progress-spinner>
    </div>
  `,
  styleUrls: ['./loader.component.scss']
})
export class LoaderComponent {
  @Input() show = false;
}
