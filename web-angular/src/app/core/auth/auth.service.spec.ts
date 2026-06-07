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

  it('nettoie au logout', () => {
    service.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock
      .expectOne('/api/v1/auth/login')
      .flush({ accessToken: 'AT', expiresAt: '2030-01-01T00:00:00Z', refreshToken: 'RT' });
    service.logout();
    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('rt')).toBeNull();
  });
});
