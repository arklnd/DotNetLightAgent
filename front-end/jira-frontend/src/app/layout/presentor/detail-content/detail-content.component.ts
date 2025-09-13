import { Component, OnInit, Input } from '@angular/core';
import { BugListService } from '../../../services/bug-list.service';
import { JiraApiService } from '../../../services/jira-api.service';

@Component({
  selector: 'app-detail-content',
  templateUrl: './detail-content.component.html',
  styleUrls: ['./detail-content.component.scss']
})
export class DetailContentComponent implements OnInit {
  @Input() loading: boolean = false;
  bugs: any[] = [];
  bugSearchTerm: string = '';
  drawerOpen = false;
  selectedIssueKey: string | null = null;
  stepsToReproduce: string | null = null;
  stepsLoading = false;

  get filteredBugs() {
    if (!this.bugs || !this.bugSearchTerm?.trim()) return this.bugs;
    const term = this.bugSearchTerm.trim().toLowerCase();
    return this.bugs.filter(bug =>
      bug.key?.toLowerCase().includes(term) ||
      (bug.fields?.summary || bug.summary)?.toLowerCase().includes(term)
    );
  }

  constructor(private bugListService: BugListService, private jiraApi: JiraApiService) {}

  ngOnInit(): void {
    this.bugListService.bugList$.subscribe((bugs: any[]) => {
      this.bugs = bugs;
    });
  }

  openDrawer(issueKey: string) {
    this.selectedIssueKey = issueKey;
    this.drawerOpen = true;
    this.stepsToReproduce = null;
    this.stepsLoading = true;
    this.jiraApi.getStepsToReproduce(issueKey).subscribe({
      next: (steps: string) => {
        this.stepsToReproduce = steps;
        this.stepsLoading = false;
      },
      error: () => {
        this.stepsToReproduce = 'Could not fetch steps to reproduce.';
        this.stepsLoading = false;
      }
    });
  }

  closeDrawer() {
    this.drawerOpen = false;
    this.selectedIssueKey = null;
    this.stepsToReproduce = null;
    this.stepsLoading = false;
  }
}
