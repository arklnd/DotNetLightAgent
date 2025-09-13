import { Component } from '@angular/core';
import { JiraApiService } from './services/jira-api.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent {
  title = 'jira-frontend';
  selectedProject: { key: string; name: string } | null = null;

  // Bottom drawer state
  drawerOpen = false;
  selectedBug: any = null;
  steps: string | null = null;
  loadingSteps = false;

  constructor(private jiraApi: JiraApiService) {}

  onProjectSelected(project: { key: string; name: string }) {
    this.selectedProject = project;
    this.drawerOpen = false;
    this.selectedBug = null;
    this.steps = null;
  }

  onBugSelected(bug: any) {
    this.selectedBug = bug;
    this.drawerOpen = true;
    this.steps = null;
    this.loadingSteps = true;
    this.jiraApi.getStepsToReproduce(bug.key).subscribe({
      next: (steps: string) => {
        this.steps = steps;
        this.loadingSteps = false;
      },
      error: () => {
        this.steps = null;
        this.loadingSteps = false;
      }
    });
  }

  onDrawerClosed() {
    this.drawerOpen = false;
    this.selectedBug = null;
    this.steps = null;
  }
}
