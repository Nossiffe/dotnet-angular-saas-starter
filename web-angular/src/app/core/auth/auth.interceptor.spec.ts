import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors, HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

const TOKEN_RESP = { accessToken: 'AT', expiresAt: '2030-01-01T00:00:00Z', refreshToken: 'RT' };
const NEW_TOKEN_RESP = {
  accessToken: 'AT2',
  expiresAt: '2030-01-01T00:00:00Z',
  refreshToken: 'RT2',
};

describe('authInterceptor', () => {
  let auth: AuthService;
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('(a) attache Authorization: Bearer quand authentifié', () => {
    // Log in first to get a token
    auth.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock.expectOne('/api/v1/auth/login').flush(TOKEN_RESP);

    // Now make a request
    http.get('/api/v1/products').subscribe();
    const req = httpMock.expectOne('/api/v1/products');
    expect(req.request.headers.get('Authorization')).toBe('Bearer AT');
    req.flush([]);
  });

  it("(b) n'attache pas Authorization sur /api/v1/auth/login", () => {
    auth.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    const req = httpMock.expectOne('/api/v1/auth/login');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush(TOKEN_RESP);
  });

  it('(d) échec du refresh appelle clearLocalState() (pas logout())', () => {
    // Log in first
    auth.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock.expectOne('/api/v1/auth/login').flush(TOKEN_RESP);

    const clearSpy = vi.spyOn(auth, 'clearLocalState');
    const logoutSpy = vi.spyOn(auth, 'logout');

    // Make a request that returns 401
    http.get('/api/v1/products').subscribe({ error: () => undefined });

    // Return 401
    httpMock.expectOne('/api/v1/products').flush(null, { status: 401, statusText: 'Unauthorized' });

    // Interceptor should call refresh — return failure
    httpMock
      .expectOne('/api/v1/auth/refresh')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(clearSpy).toHaveBeenCalledTimes(1);
    expect(logoutSpy).not.toHaveBeenCalled();

    clearSpy.mockRestore();
    logoutSpy.mockRestore();
  });

  it('(c) un 401 déclenche un refresh puis retentetive avec le nouveau token', () => {
    // Log in first
    auth.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock.expectOne('/api/v1/auth/login').flush(TOKEN_RESP);

    // Make a request that returns 401
    let responseReceived = false;
    http.get('/api/v1/products').subscribe(() => (responseReceived = true));

    // Return 401
    const firstReq = httpMock.expectOne('/api/v1/products');
    firstReq.flush(null, { status: 401, statusText: 'Unauthorized' });

    // Interceptor should call refresh
    const refreshReq = httpMock.expectOne('/api/v1/auth/refresh');
    expect(refreshReq.request.method).toBe('POST');
    refreshReq.flush(NEW_TOKEN_RESP);

    // Interceptor should retry with new token
    const retryReq = httpMock.expectOne('/api/v1/products');
    expect(retryReq.request.headers.get('Authorization')).toBe('Bearer AT2');
    retryReq.flush([]);

    expect(responseReceived).toBe(true);
  });
});
