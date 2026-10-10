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

// Réponse jeton = DTO généré `AuthResponse` (Hey API).
export type TokenResponse = AuthResponse;
// Identifiants de connexion = DTO généré `LoginRequest` (tenantId facultatif).
export type Credentials = LoginRequest;
export interface CurrentUser {
  email: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly accessToken = signal<string | null>(null);
  private readonly user = signal<CurrentUser | null>(null);

  readonly currentUser = this.user.asReadonly();
  readonly isAuthenticated = computed(() => this.accessToken() !== null);

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
   */
  refresh(): Observable<TokenResponse> {
    return this.http
      .post<TokenResponse>('/api/v1/auth/refresh', {})
      .pipe(tap((res) => this.applyTokens(res, this.user()?.email)));
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
  }

  /**
   * Révoque le refresh token côté serveur, puis efface l'état local, même si l'appel échoue.
   * `/auth/logout` est anonyme côté serveur : la preuve est le cookie HttpOnly `rt`, pas le jeton
   * d'accès, donc un jeton d'accès expiré ou absent n'empêche pas la révocation.
   */
  logout(): Observable<void> {
    return this.http.post<void>('/api/v1/auth/logout', {}).pipe(
      tap(() => this.clearLocalState()),
      catchError(() => {
        this.clearLocalState();
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

  private applyTokens(res: TokenResponse, email?: string): void {
    this.accessToken.set(res.accessToken);
    if (email) this.user.set({ email });
  }
}
