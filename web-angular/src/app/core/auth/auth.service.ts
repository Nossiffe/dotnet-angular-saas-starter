import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, of, tap } from 'rxjs';

export interface TokenResponse {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
}
export interface Credentials {
  email: string;
  password: string;
  tenantId?: string;
}
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

  register(body: Credentials): Observable<unknown> {
    return this.http.post('/api/v1/auth/register', body);
  }

  login(body: Credentials): Observable<TokenResponse> {
    return this.http
      .post<TokenResponse>('/api/v1/auth/login', body)
      .pipe(tap((res) => this.applyTokens(res, body.email)));
  }

  refresh(): Observable<TokenResponse> {
    return this.http
      .post<TokenResponse>('/api/v1/auth/refresh', { refreshToken: this.refreshToken() })
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
    return this.http.post<void>('/api/v1/auth/logout', { refreshToken: rt }).pipe(
      tap(() => clear()),
      catchError(() => {
        clear();
        return of(undefined);
      }),
    );
  }

  forgotPassword(email: string): Observable<unknown> {
    return this.http.post('/api/v1/auth/forgot-password', { email });
  }

  resetPassword(userId: string, token: string, newPassword: string): Observable<unknown> {
    return this.http.post('/api/v1/auth/reset-password', { userId, token, newPassword });
  }

  confirmEmail(userId: string, token: string): Observable<unknown> {
    return this.http.post('/api/v1/auth/confirm-email', { userId, token });
  }

  private applyTokens(res: TokenResponse, email?: string): void {
    this.accessToken.set(res.accessToken);
    this.refreshToken.set(res.refreshToken);
    localStorage.setItem(REFRESH_KEY, res.refreshToken);
    if (email) this.user.set({ email });
  }
}
