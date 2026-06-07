import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, of, tap } from 'rxjs';
import type {
  AuthResponse,
  LoginCommand,
  RegisterCommand,
  PostApiV1AuthLoginData,
  PostApiV1AuthRefreshData,
  PostApiV1AuthRegisterData,
  PostApiV1AuthLogoutData,
  PostApiV1AuthForgotPasswordData,
  PostApiV1AuthResetPasswordData,
  PostApiV1AuthConfirmEmailData,
} from '../../api/generated';

// Réponse jeton = DTO généré `AuthResponse` (Hey API). Pas d'interface à la main.
export type TokenResponse = AuthResponse;
// Identifiants de connexion = DTO généré `LoginCommand` (tenantId facultatif côté UI).
export type Credentials = Omit<LoginCommand, 'tenantId'> & Partial<Pick<LoginCommand, 'tenantId'>>;
export interface CurrentUser {
  email: string;
}

const REFRESH_KEY = 'rt';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly accessToken = signal<string | null>(null);
  private readonly refreshToken = signal<string | null>(localStorage.getItem(REFRESH_KEY));
  private readonly user = signal<CurrentUser | null>(null);

  readonly currentUser = this.user.asReadonly();
  readonly isAuthenticated = computed(() => this.accessToken() !== null);

  token(): string | null {
    return this.accessToken();
  }

  refreshTokenValue(): string | null {
    return this.refreshToken();
  }

  register(body: RegisterCommand): Observable<unknown> {
    const reqBody: PostApiV1AuthRegisterData['body'] = body;
    return this.http.post('/api/v1/auth/register', reqBody);
  }

  login(body: Credentials): Observable<TokenResponse> {
    const reqBody: PostApiV1AuthLoginData['body'] = { tenantId: null, ...body };
    return this.http
      .post<TokenResponse>('/api/v1/auth/login', reqBody)
      .pipe(tap((res) => this.applyTokens(res, body.email)));
  }

  refresh(): Observable<TokenResponse> {
    const reqBody: PostApiV1AuthRefreshData['body'] = {
      refreshToken: this.refreshToken() ?? '',
    };
    return this.http
      .post<TokenResponse>('/api/v1/auth/refresh', reqBody)
      .pipe(tap((res) => this.applyTokens(res, this.user()?.email)));
  }

  /**
   * Revoke the refresh token server-side, then clear local state.
   * Local state is cleared even if the server call fails.
   */
  logout(): Observable<void> {
    const rt = this.refreshToken();
    const clear = (): void => {
      this.accessToken.set(null);
      this.refreshToken.set(null);
      this.user.set(null);
      localStorage.removeItem(REFRESH_KEY);
    };
    if (!rt) {
      clear();
      return of(undefined);
    }
    const logoutBody: PostApiV1AuthLogoutData['body'] = { refreshToken: rt };
    return this.http.post<void>('/api/v1/auth/logout', logoutBody).pipe(
      tap(() => clear()),
      catchError(() => {
        clear();
        return of(undefined);
      }),
    );
  }

  forgotPassword(email: string): Observable<unknown> {
    const body: PostApiV1AuthForgotPasswordData['body'] = { email };
    return this.http.post('/api/v1/auth/forgot-password', body);
  }

  resetPassword(userId: string, token: string, newPassword: string): Observable<unknown> {
    const body: PostApiV1AuthResetPasswordData['body'] = { userId, token, newPassword };
    return this.http.post('/api/v1/auth/reset-password', body);
  }

  confirmEmail(userId: string, token: string): Observable<unknown> {
    const body: PostApiV1AuthConfirmEmailData['body'] = { userId, token };
    return this.http.post('/api/v1/auth/confirm-email', body);
  }

  private applyTokens(res: TokenResponse, email?: string): void {
    this.accessToken.set(res.accessToken);
    this.refreshToken.set(res.refreshToken);
    localStorage.setItem(REFRESH_KEY, res.refreshToken);
    if (email) this.user.set({ email });
  }
}
