import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });
  afterEach(() => httpMock.verify());

  it('starts unauthenticated', () => {
    expect(service.isAuthenticated()).toBe(false);
  });

  it('keeps only the access token, in memory, after login', () => {
    service.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    const req = httpMock.expectOne('/api/v1/auth/login');
    expect(req.request.method).toBe('POST');
    req.flush({ accessToken: 'AT', expiresAt: '2030-01-01T00:00:00Z' });
    expect(service.token()).toBe('AT');
    expect(service.isAuthenticated()).toBe(true);
  });

  it('logout revokes the session through the refresh cookie, with an empty body', () => {
    service.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock
      .expectOne('/api/v1/auth/login')
      .flush({ accessToken: 'AT', expiresAt: '2030-01-01T00:00:00Z' });

    let logoutDone = false;
    service.logout().subscribe(() => (logoutDone = true));

    // Le cookie HttpOnly `rt` part automatiquement — le body est vide.
    const logoutReq = httpMock.expectOne('/api/v1/auth/logout');
    expect(logoutReq.request.method).toBe('POST');
    expect(logoutReq.request.body).toEqual({});
    logoutReq.flush(null, { status: 204, statusText: 'No Content' });

    expect(logoutDone).toBe(true);
    expect(service.isAuthenticated()).toBe(false);
  });

  it('clears the local state even when the server logout fails', () => {
    service.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock
      .expectOne('/api/v1/auth/login')
      .flush({ accessToken: 'AT', expiresAt: '2030-01-01T00:00:00Z' });

    let logoutDone = false;
    service.logout().subscribe(() => (logoutDone = true));

    httpMock
      .expectOne('/api/v1/auth/logout')
      .flush('error', { status: 500, statusText: 'Server Error' });

    expect(logoutDone).toBe(true);
    expect(service.isAuthenticated()).toBe(false);
  });

  it('restoreSession() signs in silently with a valid refresh cookie', () => {
    let restored: boolean | undefined;
    service.restoreSession().subscribe((ok) => (restored = ok));

    const req = httpMock.expectOne('/api/v1/auth/refresh');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush({ accessToken: 'AT', expiresAt: '2030-01-01T00:00:00Z' });

    expect(restored).toBe(true);
    expect(service.isAuthenticated()).toBe(true);
  });

  it('restoreSession() returns false without error when there is no refresh cookie (401)', () => {
    let restored: boolean | undefined;
    service.restoreSession().subscribe((ok) => (restored = ok));

    httpMock
      .expectOne('/api/v1/auth/refresh')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(restored).toBe(false);
    expect(service.isAuthenticated()).toBe(false);
  });
});
