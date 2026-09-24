import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { Router } from '@angular/router';

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const token = auth.accessToken();

  let clonedReq = req.clone({
    withCredentials: true
  });

  if (token && !req.url.includes('/auth/login') && !req.url.includes('/auth/register')) {
    clonedReq = clonedReq.clone({
      setHeaders: {
        Authorization: 'Bearer ' + token
      }
    });
  }

  return next(clonedReq).pipe(
    catchError((error: HttpErrorResponse) => {
      // If 401 Unauthorized and not already refreshing/logging in
      if (error.status === 401 && !req.url.includes('/auth/refresh') && !req.url.includes('/auth/login')) {
        return auth.refreshToken().pipe(
          switchMap(res => {
            const retryReq = req.clone({
              withCredentials: true,
              setHeaders: {
                Authorization: 'Bearer ' + res.accessToken
              }
            });
            return next(retryReq);
          }),
          catchError(refreshErr => {
            auth.logout().subscribe();
            router.navigate(['/login']);
            return throwError(() => refreshErr);
          })
        );
      }
      return throwError(() => error);
    })
  );
};
