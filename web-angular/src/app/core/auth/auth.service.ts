import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

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

  register(body: Credentials): Observable<unknown> {
    return this.http.post('/auth/register', body);
  }

  login(body: Credentials): Observable<TokenResponse> {
    return this.http
      .post<TokenResponse>('/auth/login', body)
      .pipe(tap((res) => this.applyTokens(res, body.email)));
  }

  refresh(): Observable<TokenResponse> {
    return this.http
      .post<TokenResponse>('/auth/refresh', { refreshToken: this.refreshToken() })
      .pipe(tap((res) => this.applyTokens(res, this.user()?.email)));
  }

  logout(): void {
    this.accessToken.set(null);
    this.refreshToken.set(null);
    this.user.set(null);
    localStorage.removeItem(REFRESH_KEY);
  }

  private applyTokens(res: TokenResponse, email?: string): void {
    this.accessToken.set(res.accessToken);
    this.refreshToken.set(res.refreshToken);
    localStorage.setItem(REFRESH_KEY, res.refreshToken);
    if (email) this.user.set({ email });
  }
}
