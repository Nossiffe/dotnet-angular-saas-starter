import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, catchError, switchMap, throwError } from 'rxjs';
import { shareReplay } from 'rxjs/operators';
import { AuthService, TokenResponse } from './auth.service';

let refresh$: Observable<TokenResponse> | null = null;

const isAuthCall = (url: string): boolean =>
  url.includes('/auth/login') || url.includes('/auth/register') || url.includes('/auth/refresh');

const withBearer = (req: HttpRequest<unknown>, token: string): HttpRequest<unknown> =>
  req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  if (isAuthCall(req.url)) return next(req);

  const token = auth.token();
  const authReq = token ? withBearer(req, token) : req;

  return next(authReq).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status !== 401 || !auth.token()) return throwError(() => err);
      refresh$ ??= auth.refresh().pipe(shareReplay(1));
      return refresh$.pipe(
        switchMap((res) => { refresh$ = null; return next(withBearer(req, res.accessToken)); }),
        catchError((refreshErr) => { refresh$ = null; auth.logout(); return throwError(() => refreshErr); }),
      );
    }),
  );
};
