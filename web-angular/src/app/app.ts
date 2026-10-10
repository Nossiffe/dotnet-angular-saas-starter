import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from './core/auth/auth.service';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbarModule, MatButtonModule],
  // Lien de la page courante : souligné, en plus de aria-current pour les lecteurs d'écran.
  styles: `
    nav a.active {
      text-decoration: underline;
      text-underline-offset: 0.3em;
    }
  `,
  template: `
    <mat-toolbar color="primary">
      <span>Saas</span>
      @if (auth.isAuthenticated()) {
        <nav aria-label="Navigation principale">
          <a
            mat-button
            routerLink="/products"
            routerLinkActive="active"
            ariaCurrentWhenActive="page"
            >Produits</a
          >
          @if (auth.canManageOrganization()) {
            <a
              mat-button
              routerLink="/organization"
              routerLinkActive="active"
              ariaCurrentWhenActive="page"
              >Organisation</a
            >
          }
        </nav>
      }
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
  logout(): void {
    this.auth.logout().subscribe(() => {
      void this.router.navigateByUrl('/login');
    });
  }
}
