import {
  HttpContextToken,
  HttpErrorResponse,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, catchError, switchMap, throwError } from 'rxjs';
import { finalize, shareReplay } from 'rxjs/operators';
import { AuthService, TokenResponse } from './auth.service';

const SKIP_AUTH_RETRY = new HttpContextToken(() => false);

let refresh$: Observable<TokenResponse> | null = null;

// Le logout aussi : il tient le verrou du cookie de session (AuthService.logout()), et un refresh
// lancé par un 401 sur lui attendrait ce verrou pendant que le logout attendrait ce refresh. Il est
// anonyme côté serveur, sa preuve est le cookie : le jeton d'accès ne lui sert à rien.
const isAuthCall = (url: string): boolean =>
  url.includes('/api/v1/auth/login') ||
  url.includes('/api/v1/auth/register') ||
  url.includes('/api/v1/auth/refresh') ||
  url.includes('/api/v1/auth/logout');

const isSameOrigin = (url: string): boolean =>
  url.startsWith('/') || url.startsWith(window.location.origin);

const withBearer = (req: HttpRequest<unknown>, token: string): HttpRequest<unknown> =>
  req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  if (isAuthCall(req.url) || !isSameOrigin(req.url)) return next(req);

  const token = auth.token();
  const authReq = token ? withBearer(req, token) : req;

  return next(authReq).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status !== 401 || !auth.token() || req.context.get(SKIP_AUTH_RETRY))
        return throwError(() => err);
      // Un refresh partagé par les requêtes concurrentes, oublié dès qu'il se termine, même si
      // toutes les requêtes qui l'attendaient ont été annulées entre-temps.
      refresh$ ??= auth.refresh().pipe(
        finalize(() => (refresh$ = null)),
        shareReplay(1),
      );
      // Un seul refresh par requête : le rejeu passe directement au handler suivant. Seul l'échec
      // du refresh efface la session ; une erreur du rejeu (403 si le rôle actuel ne suffit pas,
      // par exemple) est transmise telle quelle.
      return refresh$.pipe(
        catchError((refreshErr: unknown) => {
          auth.clearLocalState();
          return throwError(() => refreshErr);
        }),
        switchMap((res) => {
          return next(
            withBearer(req, res.accessToken).clone({
              context: req.context.set(SKIP_AUTH_RETRY, true),
            }),
          );
        }),
      );
    }),
  );
};
