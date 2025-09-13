import { TestBed } from '@angular/core/testing';

import { JiraApiService } from './jira-api.service';

describe('JiraApiService', () => {
  let service: JiraApiService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(JiraApiService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
