import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from './core/auth/auth.service';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbarModule, MatButtonModule],
  styles: `
    // En-tête : fond de carte et filet, sur toute la largeur ; son contenu suit la largeur de la
    // page.
    mat-toolbar {
      padding: 0;
      border-block-end: 1px solid var(--mat-sys-outline-variant);
      background: var(--card-background);
      color: var(--mat-sys-on-surface);
    }
    .bar {
      display: flex;
      align-items: center;
      gap: 1.5rem;
      width: 100%;
      max-width: 68rem;
      margin-inline: auto;
      padding-inline: 1.5rem;
      box-sizing: border-box;
    }
    .brand {
      display: inline-flex;
      align-items: center;
      gap: 0.625rem;
      font: var(--mat-sys-title-medium);
      font-weight: 600;
    }
    .brand-mark {
      width: 1.5rem;
      height: 1.5rem;
      border-radius: 6px;
      background: var(--mat-sys-primary);
      box-shadow: inset -0.5rem -0.5rem 0 0 var(--mat-sys-primary-container);
    }
    nav {
      display: flex;
      gap: 0.25rem;
    }
    nav a {
      color: var(--mat-sys-on-surface-variant);
    }
    // Lien de la page courante : souligné et en couleur d'accent, en plus de aria-current pour les
    // lecteurs d'écran.
    nav a.active {
      color: var(--mat-sys-primary);
      text-decoration: underline;
      text-underline-offset: 0.3em;
    }
    .spacer {
      flex: 1 1 auto;
    }
    // Écran étroit, où la barre ne tient plus sur une ligne (une fois connecté, il lui faut environ
    // 34rem) : le nom et « Déconnexion » sur une ligne, la navigation en dessous. La barre garde la
    // hauteur de Material sur mobile, 3.5rem, au moins.
    @media (width <= 35rem) {
      mat-toolbar {
        height: auto;
        min-height: 3.5rem;
      }
      .bar {
        flex-wrap: wrap;
        gap: 0 0.5rem;
        padding: 0.5rem 1rem;
      }
      nav {
        order: 3;
        flex-basis: 100%;
      }
    }
  `,
  template: `
    <mat-toolbar>
      <div class="bar">
        <span class="brand">
          <span class="brand-mark" aria-hidden="true"></span>
          SaaS Starter
        </span>
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
        <span class="spacer"></span>
        @if (auth.isAuthenticated()) {
          <button mat-button (click)="logout()">Déconnexion</button>
        }
      </div>
    </mat-toolbar>
    <main class="page"><router-outlet /></main>
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
