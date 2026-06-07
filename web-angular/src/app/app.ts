import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from './core/auth/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, MatToolbarModule, MatButtonModule],
  template: `
    <mat-toolbar color="primary">
      <span>Saas</span>
      <span style="flex: 1 1 auto"></span>
      @if (auth.isAuthenticated()) {
        <button mat-button (click)="logout()">Déconnexion</button>
      }
    </mat-toolbar>
    <main style="padding: 1rem"><router-outlet /></main>
  `,
})
export class App {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  logout() { this.auth.logout(); void this.router.navigateByUrl('/login'); }
}
