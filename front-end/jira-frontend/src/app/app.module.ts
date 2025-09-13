
import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { FormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';

// Angular Material Modules
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialogModule } from '@angular/material/dialog';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatCardModule } from '@angular/material/card';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { FooterComponent } from './layout/footer/footer.component';
import { TeamModalComponent } from './modals/team-modal/team-modal.component';
import { HeaderComponent } from './layout/header/header.component';
import { PresentorComponent } from './layout/presentor/presentor.component';
import { MasterSidebarComponent } from './layout/presentor/master-sidebar/master-sidebar.component';
import { DetailContentComponent } from './layout/presentor/detail-content/detail-content.component';
import { LoaderComponent } from './shared/loader/loader.component';
import { BottomDrawerComponent } from './layout/presentor/bottom-drawer/bottom-drawer.component';

@NgModule({
  declarations: [
  AppComponent,
  FooterComponent,
  TeamModalComponent,
  HeaderComponent,
  PresentorComponent,
  MasterSidebarComponent,
  DetailContentComponent,
  LoaderComponent,
  BottomDrawerComponent
  ],
  imports: [
    BrowserModule,
    BrowserAnimationsModule,
    FormsModule,
    HttpClientModule,
    AppRoutingModule,
    MatToolbarModule,
    MatIconModule,
    MatListModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    MatSidenavModule,
    MatCardModule
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
