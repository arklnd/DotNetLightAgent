import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class JiraApiService {
  private apiUrl = 'http://localhost:5212/api/jira';

  // Hardcoded credentials (replace with your own for now)
  private jiraAuth = {
    Url: 'https://hyland.atlassian.net',
    Username: 'arka.de@hyland.com',
    ApiToken: 'ATATT3xFfGF00BmQ8OeSHMH-Odde5bBTy9oh4ciF148yv2ermTypWyhy6aQOXmiQAcqOjPCPW3j_XazrfOnYT2607DsUmqg_tMnDvcotlNEoAEyNKRQljizCucrF-tARhfKDqq2USSngcRNc_PF6whRIIkJzrrmZBO4xSqKx5DGUBtEiajYCgtk=DB2D604F'
  };

  constructor(private http: HttpClient) {}

  authenticate(auth: { Url: string; Username: string; ApiToken: string }): Observable<any> {
    return this.http.post(`${this.apiUrl}/authenticate`, auth);
  }

  getIssues(jql: string): Observable<any> {
    const params = new HttpParams().set('jql', jql);
    return this.http.get(`${this.apiUrl}/issues`, { params });
  }

  getIssueByKey(key: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/issue/${key}`);
  }

  getStepsToReproduce(key: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/issue/${key}/steps`, { responseType: 'text' });
  }

  getAllProjects(): Observable<any> {
    return this.http.post(`${this.apiUrl}/projects`, this.jiraAuth);
  }
}
