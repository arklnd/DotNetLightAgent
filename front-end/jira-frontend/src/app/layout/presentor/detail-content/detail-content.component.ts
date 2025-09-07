import { Component, OnInit, Input } from '@angular/core';
import { BugListService } from '../../../services/bug-list.service';

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

  get filteredBugs() {
    if (!this.bugs || !this.bugSearchTerm?.trim()) return this.bugs;
    const term = this.bugSearchTerm.trim().toLowerCase();
    return this.bugs.filter(bug =>
      bug.key?.toLowerCase().includes(term) ||
      (bug.fields?.summary || bug.summary)?.toLowerCase().includes(term)
    );
  }

  constructor(private bugListService: BugListService) {}

  ngOnInit(): void {
    this.bugListService.bugList$.subscribe((bugs: any[]) => {
      this.bugs = bugs;
    });
  }

  openDrawer(issueKey: string) {
    this.selectedIssueKey = issueKey;
    this.drawerOpen = true;
  }

  closeDrawer() {
    this.drawerOpen = false;
    this.selectedIssueKey = null;
  }
}
