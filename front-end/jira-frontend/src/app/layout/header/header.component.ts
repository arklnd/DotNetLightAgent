import { Component } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { TeamModalComponent } from '../../modals/team-modal/team-modal.component';

@Component({
  selector: 'app-header',
  templateUrl: './header.component.html',
  styleUrls: ['./header.component.scss']
})
export class HeaderComponent {
  constructor(private dialog: MatDialog) {}

  openTeamModal() {
    this.dialog.open(TeamModalComponent, {
      width: '400px',
      panelClass: 'team-modal-panel',
      disableClose: false
    });
  }
}
