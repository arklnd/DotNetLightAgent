import { Component } from '@angular/core';

@Component({
  selector: 'app-team-modal',
  templateUrl: './team-modal.component.html',
  styleUrls: ['./team-modal.component.scss']
})
export class TeamModalComponent {
  teamMembers = [
    'Rupsa Roy',
    'Arijit Kundu',
    'Manish Singh',
    'Shreyashi Choudhury',
    'Arka De'
  ];
}
