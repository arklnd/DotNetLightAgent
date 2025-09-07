
import { Component, OnInit } from '@angular/core';
import { JiraApiService } from '../../../services/jira-api.service';

@Component({
  selector: 'app-master-sidebar',
  templateUrl: './master-sidebar.component.html',
  styleUrls: ['./master-sidebar.component.scss']
})
export class MasterSidebarComponent implements OnInit {
  loading = false;
  projects: any[] = [];
  searchTerm = '';

  constructor(private jiraApi: JiraApiService) {}

  ngOnInit() {
    this.fetchProjects();
  }

  fetchProjects() {
    this.loading = true;
    this.jiraApi.getAllProjects().subscribe({
      next: (result) => {
        console.log('Projects response:', result);
        this.projects = Array.isArray(result) ? result : [];
        this.loading = false;
      },
      error: (err) => {
        console.error('Projects error:', err);
        this.projects = [];
        this.loading = false;
      }
    });
  }

  filteredProjects() {
    if (!this.searchTerm) return this.projects;
    return this.projects.filter(p => p.name?.toLowerCase().includes(this.searchTerm.toLowerCase()));
  }
}
