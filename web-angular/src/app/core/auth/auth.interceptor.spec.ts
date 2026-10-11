import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors, HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

const TOKEN_RESP = { accessToken: 'AT', expiresAt: '2030-01-01T00:00:00Z' };
const NEW_TOKEN_RESP = { accessToken: 'AT2', expiresAt: '2030-01-01T00:00:00Z' };

describe('authInterceptor', () => {
  let auth: AuthService;
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
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

  it('adds the Bearer token when authenticated', () => {
    // Connexion d'abord, pour obtenir un jeton
    auth.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock.expectOne('/api/v1/auth/login').flush(TOKEN_RESP);

    http.get('/api/v1/products').subscribe();
    const req = httpMock.expectOne('/api/v1/products');
    expect(req.request.headers.get('Authorization')).toBe('Bearer AT');
    req.flush([]);
  });

  it('does not add the token to /api/v1/auth/login', () => {
    auth.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    const req = httpMock.expectOne('/api/v1/auth/login');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush(TOKEN_RESP);
  });

  // Le logout tient le verrou du cookie de session : un refresh lancé par un 401 sur lui attendrait
  // ce verrou, et le logout attendrait ce refresh.
  it('sends the logout without the access token, and a 401 on it triggers no refresh', () => {
    auth.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock.expectOne('/api/v1/auth/login').flush(TOKEN_RESP);

    let loggedOut = false;
    auth.logout().subscribe(() => (loggedOut = true));
    const req = httpMock.expectOne('/api/v1/auth/logout');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush(null, { status: 401, statusText: 'Unauthorized' });

    httpMock.expectNone('/api/v1/auth/refresh');
    expect(loggedOut).toBe(true);
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('a failed refresh calls clearLocalState(), not logout()', () => {
    auth.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock.expectOne('/api/v1/auth/login').flush(TOKEN_RESP);

    const clearSpy = vi.spyOn(auth, 'clearLocalState');
    const logoutSpy = vi.spyOn(auth, 'logout');

    // Une requête qui reçoit 401
    http.get('/api/v1/products').subscribe({ error: () => undefined });

    httpMock.expectOne('/api/v1/products').flush(null, { status: 401, statusText: 'Unauthorized' });

    // L'intercepteur appelle refresh, qui échoue
    httpMock
      .expectOne('/api/v1/auth/refresh')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(clearSpy).toHaveBeenCalledTimes(1);
    expect(logoutSpy).not.toHaveBeenCalled();

    clearSpy.mockRestore();
    logoutSpy.mockRestore();
  });

  it('a 401 triggers a refresh, then retries with the new token', () => {
    auth.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock.expectOne('/api/v1/auth/login').flush(TOKEN_RESP);

    // Une requête qui reçoit 401
    let responseReceived = false;
    http.get('/api/v1/products').subscribe(() => (responseReceived = true));

    const firstReq = httpMock.expectOne('/api/v1/products');
    firstReq.flush(null, { status: 401, statusText: 'Unauthorized' });

    // L'intercepteur appelle refresh, corps vide (le cookie `rt` part seul)
    const refreshReq = httpMock.expectOne('/api/v1/auth/refresh');
    expect(refreshReq.request.method).toBe('POST');
    expect(refreshReq.request.body).toEqual({});
    refreshReq.flush(NEW_TOKEN_RESP);

    // Puis rejoue la requête avec le nouveau jeton
    const retryReq = httpMock.expectOne('/api/v1/products');
    expect(retryReq.request.headers.get('Authorization')).toBe('Bearer AT2');
    retryReq.flush([]);

    expect(responseReceived).toBe(true);
  });

  // Un seul refresh par requête : le rejeu ne repasse pas par l'intercepteur. Un second 401 est
  // transmis tel quel, sans nouveau refresh, si bien que le front ne boucle pas.
  it('a 401 on the retried request is passed on without a second refresh', () => {
    auth.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock.expectOne('/api/v1/auth/login').flush(TOKEN_RESP);

    let status = 0;
    http
      .get('/api/v1/products')
      .subscribe({ error: (err: { status: number }) => (status = err.status) });

    httpMock.expectOne('/api/v1/products').flush(null, { status: 401, statusText: 'Unauthorized' });
    httpMock.expectOne('/api/v1/auth/refresh').flush(NEW_TOKEN_RESP);
    httpMock.expectOne('/api/v1/products').flush(null, { status: 401, statusText: 'Unauthorized' });

    httpMock.expectNone('/api/v1/auth/refresh');
    expect(status).toBe(401);
  });

  // Après un refresh réussi, un refus du rejeu (le rôle actuel ne suffit pas, par exemple) est une
  // réponse ordinaire : la session, valide, n'est pas effacée.
  it('an error on the retried request keeps the refreshed session', () => {
    auth.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock.expectOne('/api/v1/auth/login').flush(TOKEN_RESP);
    const clearSpy = vi.spyOn(auth, 'clearLocalState');

    let status = 0;
    http
      .post('/api/v1/organizations/o/invitations', {})
      .subscribe({ error: (err: { status: number }) => (status = err.status) });

    httpMock
      .expectOne('/api/v1/organizations/o/invitations')
      .flush(null, { status: 401, statusText: 'Unauthorized' });
    httpMock.expectOne('/api/v1/auth/refresh').flush(NEW_TOKEN_RESP);
    httpMock
      .expectOne('/api/v1/organizations/o/invitations')
      .flush(null, { status: 403, statusText: 'Forbidden' });

    expect(status).toBe(403);
    expect(clearSpy).not.toHaveBeenCalled();
    expect(auth.token()).toBe('AT2');
    clearSpy.mockRestore();
  });

  // Toutes les requêtes qui attendaient le refresh ont été annulées (composant détruit, par
  // exemple) : le refresh se termine quand même, puis il est oublié. Le 401 suivant en lance un
  // nouveau au lieu de rejouer le jeton de l'ancien.
  it('a refresh whose requests were all cancelled is not reused by the next 401', () => {
    auth.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock.expectOne('/api/v1/auth/login').flush(TOKEN_RESP);

    const cancelled = http.get('/api/v1/products').subscribe({ error: () => undefined });
    httpMock.expectOne('/api/v1/products').flush(null, { status: 401, statusText: 'Unauthorized' });
    const firstRefresh = httpMock.expectOne('/api/v1/auth/refresh');
    cancelled.unsubscribe();
    firstRefresh.flush(NEW_TOKEN_RESP);

    http.get('/api/v1/products').subscribe({ error: () => undefined });
    httpMock.expectOne('/api/v1/products').flush(null, { status: 401, statusText: 'Unauthorized' });
    httpMock.expectOne('/api/v1/auth/refresh').flush(NEW_TOKEN_RESP);
    httpMock.expectOne('/api/v1/products').flush([]);
  });
});
