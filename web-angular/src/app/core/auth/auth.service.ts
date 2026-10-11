import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable, catchError, map, of, tap } from 'rxjs';
import type {
  AuthResponse,
  LoginRequest,
  RegisterRequest,
  PostApiV1AuthLoginData,
  PostApiV1AuthRegisterData,
  PostApiV1AuthForgotPasswordData,
  PostApiV1AuthResetPasswordData,
  PostApiV1AuthConfirmEmailData,
} from '../../api/generated';
import { SKIP_ERROR_TOAST } from '../error/http-error.interceptor';
import { withCrossTabLock } from './cross-tab-lock';

/**
 * Verrou Web Locks des appels qui présentent le cookie `rt` puis le remplacent ou le suppriment
 * (refresh et logout), partagé par les onglets de l'origine. Voir refresh().
 */
export const SESSION_COOKIE_LOCK = 'auth-session-cookie';

// Réponse jeton = DTO généré `AuthResponse` (Hey API).
export type TokenResponse = AuthResponse;
// Identifiants de connexion = DTO généré `LoginRequest` (tenantId facultatif).
export type Credentials = LoginRequest;
export interface CurrentUser {
  email: string;
}

/** Rôle dans l'organisation du jeton (claim `tenant_role`). */
export type TenantRole = 'Owner' | 'Admin' | 'Member';

/**
 * Claims du jeton d'accès utiles à l'affichage : l'utilisateur, son organisation et son rôle.
 * Lus sans vérifier la signature : ils ne servent qu'à adapter l'interface, l'API contrôle chaque
 * appel (adhésion et rôle relus en base).
 */
export interface SessionClaims {
  userId: string;
  tenantId: string;
  tenantRole: TenantRole | null;
}

interface AccessTokenPayload {
  sub?: unknown;
  tenant?: unknown;
  tenant_role?: unknown;
}

function decodeClaims(token: string | null): SessionClaims | null {
  const payload = token?.split('.')[1];
  if (!payload) return null;
  try {
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    const claims = JSON.parse(atob(padded)) as AccessTokenPayload;
    const role = claims.tenant_role;
    return {
      userId: typeof claims.sub === 'string' ? claims.sub : '',
      tenantId: typeof claims.tenant === 'string' ? claims.tenant : '',
      tenantRole: role === 'Owner' || role === 'Admin' || role === 'Member' ? role : null,
    };
  } catch {
    return null;
  }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly accessToken = signal<string | null>(null);
  private readonly user = signal<CurrentUser | null>(null);
  private readonly endedByLogout = signal(false);

  readonly currentUser = this.user.asReadonly();
  readonly isAuthenticated = computed(() => this.accessToken() !== null);
  /**
   * La dernière session s'est terminée par « Déconnexion », et non par un refresh refusé : une page
   * n'annonce alors pas une session expirée.
   */
  readonly loggedOut = this.endedByLogout.asReadonly();
  /** Claims du jeton courant ; mis à jour à chaque refresh, donc après un changement de rôle. */
  readonly session = computed(() => decodeClaims(this.accessToken()));
  /** Owner ou Admin : accès à l'écran Organisation. */
  readonly canManageOrganization = computed(() => {
    const role = this.session()?.tenantRole;
    return role === 'Owner' || role === 'Admin';
  });

  token(): string | null {
    return this.accessToken();
  }

  register(body: RegisterRequest): Observable<unknown> {
    const reqBody: PostApiV1AuthRegisterData['body'] = body;
    return this.http.post('/api/v1/auth/register', reqBody, {
      context: new HttpContext().set(SKIP_ERROR_TOAST, true),
    });
  }

  login(body: Credentials): Observable<TokenResponse> {
    const reqBody: PostApiV1AuthLoginData['body'] = { tenantId: null, ...body };
    return this.http
      .post<TokenResponse>('/api/v1/auth/login', reqBody)
      .pipe(tap((res) => this.applyTokens(res, body.email)));
  }

  /**
   * Renouvelle le jeton d'accès. Le refresh token n'est jamais lisible en JS : il vit dans le
   * cookie HttpOnly `rt` (Path=/api/v1/auth), que le navigateur envoie seul (même origine, via le
   * proxy de dev).
   *
   * Un refresh à la fois entre les onglets. Ils partagent le cookie, et l'API fait tourner le
   * refresh token à chaque refresh : un second refresh parti avant la réponse du premier présente
   * le même token. Traité en même temps que le premier, il est refusé par le contrôle de
   * concurrence de la rotation ; arrivé après elle, l'API y voit un vol et révoque toutes les
   * sessions de l'utilisateur. Dans les deux cas, le refus supprime le cookie partagé. Avec le
   * verrou, le second part après la réponse du premier, avec le cookie qu'elle a posé.
   */
  refresh(): Observable<TokenResponse> {
    return withCrossTabLock(
      SESSION_COOKIE_LOCK,
      this.http.post<TokenResponse>('/api/v1/auth/refresh', {}),
    ).pipe(tap((res) => this.applyTokens(res, this.user()?.email)));
  }

  /**
   * Restauration silencieuse de la session au démarrage (voir `app.config.ts`) par le cookie
   * HttpOnly. Un 401 veut seulement dire « non connecté » et ne remonte pas comme une erreur.
   */
  restoreSession(): Observable<boolean> {
    return this.refresh().pipe(
      map(() => true),
      catchError(() => of(false)),
    );
  }

  /**
   * Efface l'état d'authentification local, sans appel HTTP : les signaux seulement, le cookie
   * du refresh token étant invisible en JS et supprimé côté serveur par /auth/logout. À utiliser
   * quand la révocation côté serveur est impossible ou inutile (échec du refresh dans
   * l'intercepteur, par exemple).
   */
  clearLocalState(): void {
    this.accessToken.set(null);
    this.user.set(null);
    this.endedByLogout.set(false);
  }

  /**
   * Révoque le refresh token côté serveur, puis efface l'état local, même si l'appel échoue.
   * `/auth/logout` est anonyme côté serveur : la preuve est le cookie HttpOnly `rt`, pas le jeton
   * d'accès, donc un jeton d'accès expiré ou absent n'empêche pas la révocation.
   *
   * Sous le même verrou que refresh() : sinon, le logout d'un onglet pendant le refresh d'un autre
   * présenterait le token que ce refresh vient de remplacer. L'API n'aurait rien à révoquer, et la
   * réponse du refresh, arrivée après celle du logout, reposerait un cookie valide : la session
   * survivrait à « Déconnexion ».
   */
  logout(): Observable<void> {
    return withCrossTabLock(
      SESSION_COOKIE_LOCK,
      this.http.post<void>('/api/v1/auth/logout', {}),
    ).pipe(
      tap(() => this.endByLogout()),
      catchError(() => {
        this.endByLogout();
        return of(undefined);
      }),
    );
  }

  forgotPassword(email: string): Observable<unknown> {
    const body: PostApiV1AuthForgotPasswordData['body'] = { email };
    return this.http.post('/api/v1/auth/forgot-password', body, {
      context: new HttpContext().set(SKIP_ERROR_TOAST, true),
    });
  }

  resetPassword(userId: string, token: string, newPassword: string): Observable<unknown> {
    const body: PostApiV1AuthResetPasswordData['body'] = { userId, token, newPassword };
    return this.http.post('/api/v1/auth/reset-password', body, {
      context: new HttpContext().set(SKIP_ERROR_TOAST, true),
    });
  }

  confirmEmail(userId: string, token: string): Observable<unknown> {
    const body: PostApiV1AuthConfirmEmailData['body'] = { userId, token };
    return this.http.post('/api/v1/auth/confirm-email', body, {
      context: new HttpContext().set(SKIP_ERROR_TOAST, true),
    });
  }

  private endByLogout(): void {
    this.clearLocalState();
    this.endedByLogout.set(true);
  }

  private applyTokens(res: TokenResponse, email?: string): void {
    this.accessToken.set(res.accessToken);
    this.endedByLogout.set(false);
    if (email) this.user.set({ email });
  }
}
