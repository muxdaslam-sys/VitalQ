/**
 * ============================================================================
 * INTERCEPTOR: jwtInterceptor (Bearer Token Injection & Silent Refresh)
 * ============================================================================
 * 
 * PURPOSE:
 * Transparently handles HTTP security for all outbound REST API requests.
 * 
 * RESPONSIBILITIES:
 * 1. Attaches `withCredentials: true` to all requests to support secure cookie transport.
 * 2. Attaches `Authorization: Bearer <accessToken>` header for protected endpoints.
 * 3. Transparent Token Refresh: If the backend returns a `401 Unauthorized` error:
 *    - Pauses the failed request.
 *    - Calls `authService.refreshToken()` to exchange the HttpOnly cookie for a new JWT.
 *    - Clones the original request with the fresh token and automatically retries it.
 *    - If the refresh cookie is also expired/invalid, logs the user out and redirects to `/login`.
 * ============================================================================
 */

import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { Router } from '@angular/router';

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const token = auth.accessToken();

  // 1. Ensure withCredentials is true for all API calls (passes refresh cookies)
  let clonedReq = req.clone({
    withCredentials: true
  });

  // 2. Attach Authorization header if access token exists (skip public auth endpoints)
  if (token && !req.url.includes('/auth/login') && !req.url.includes('/auth/register')) {
    clonedReq = clonedReq.clone({
      setHeaders: {
        Authorization: 'Bearer ' + token
      }
    });
  }

  // 3. Execute request and catch unauthorized 401 failures
  return next(clonedReq).pipe(
    catchError((error: HttpErrorResponse) => {
      // If 401 Unauthorized and not already on a login/refresh cycle
      if (error.status === 401 && !req.url.includes('/auth/refresh') && !req.url.includes('/auth/login')) {
        // Attempt silent background token refresh
        return auth.refreshToken().pipe(
          switchMap(res => {
            // Retry the original request with the newly issued access token
            const retryReq = req.clone({
              withCredentials: true,
              setHeaders: {
                Authorization: 'Bearer ' + res.accessToken
              }
            });
            return next(retryReq);
          }),
          catchError(refreshErr => {
            // Refresh token has also expired; terminate session and force re-login
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
