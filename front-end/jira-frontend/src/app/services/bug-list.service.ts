import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class BugListService {
  private bugListSource = new Subject<any[]>();
  bugList$ = this.bugListSource.asObservable();
  setBugs(bugs: any[]) {
    this.bugListSource.next(bugs);
  }
}
