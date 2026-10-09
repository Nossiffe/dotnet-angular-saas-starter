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

// Réponse jeton = DTO généré `AuthResponse` (Hey API). Pas d'interface à la main.
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
   * Refresh the access token. The refresh token itself never touches JS: it
   * lives in the HttpOnly cookie `rt` (Path=/api/v1/auth) and is sent
   * automatically by the browser (same-origin via the dev proxy).
   */
  refresh(): Observable<TokenResponse> {
    return this.http
      .post<TokenResponse>('/api/v1/auth/refresh', {})
      .pipe(tap((res) => this.applyTokens(res, this.user()?.email)));
  }

  /**
   * Silent session restoration attempted at boot (see `restoreSession`
   * provider in `app.config.ts`). Tries the HttpOnly refresh cookie; a 401
   * simply means "not logged in" and must not surface as an error.
   */
  restoreSession(): Observable<boolean> {
    return this.refresh().pipe(
      map(() => true),
      catchError(() => of(false)),
    );
  }

  /**
   * Synchronously clears all local auth state (signals only — the refresh
   * token cookie is invisible to JS and cleared server-side by /auth/logout).
   * Makes no HTTP call. Call this when the server revocation is
   * impossible or inappropriate (e.g. refresh failure in the interceptor).
   */
  clearLocalState(): void {
    this.accessToken.set(null);
    this.user.set(null);
  }

  /**
   * Revoke the refresh token server-side, then clear local state.
   * Local state is cleared even if the server call fails.
   * `/auth/logout` is `AllowAnonymous` server-side (the real credential is the
   * HttpOnly `rt` cookie, not the access token) — an expired/absent access
   * token does not prevent the server-side revocation.
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
