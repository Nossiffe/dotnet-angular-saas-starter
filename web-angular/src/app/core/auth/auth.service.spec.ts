import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });
  afterEach(() => httpMock.verify());

  it('démarre non authentifié', () => {
    expect(service.isAuthenticated()).toBe(false);
  });

  it('stocke le token et authentifie au login', () => {
    service.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    const req = httpMock.expectOne('/api/v1/auth/login');
    expect(req.request.method).toBe('POST');
    req.flush({ accessToken: 'AT', expiresAt: '2030-01-01T00:00:00Z', refreshToken: 'RT' });
    expect(service.token()).toBe('AT');
    expect(service.isAuthenticated()).toBe(true);
    expect(localStorage.getItem('rt')).toBe('RT');
  });

  it('nettoie au logout (révoque le refresh token)', () => {
    service.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock
      .expectOne('/api/v1/auth/login')
      .flush({ accessToken: 'AT', expiresAt: '2030-01-01T00:00:00Z', refreshToken: 'RT' });

    let logoutDone = false;
    service.logout().subscribe(() => (logoutDone = true));

    // Should call /api/v1/auth/logout with the refresh token
    const logoutReq = httpMock.expectOne('/api/v1/auth/logout');
    expect(logoutReq.request.method).toBe('POST');
    expect((logoutReq.request.body as { refreshToken: string }).refreshToken).toBe('RT');
    logoutReq.flush(null, { status: 204, statusText: 'No Content' });

    expect(logoutDone).toBe(true);
    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('rt')).toBeNull();
  });

  it("nettoie l'état local même si le logout serveur échoue", () => {
    service.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock
      .expectOne('/api/v1/auth/login')
      .flush({ accessToken: 'AT', expiresAt: '2030-01-01T00:00:00Z', refreshToken: 'RT' });

    let logoutDone = false;
    service.logout().subscribe(() => (logoutDone = true));

    httpMock
      .expectOne('/api/v1/auth/logout')
      .flush('error', { status: 500, statusText: 'Server Error' });

    expect(logoutDone).toBe(true);
    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('rt')).toBeNull();
  });
});
