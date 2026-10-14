/**
 * jwtInterceptor: Attaches Bearer authentication headers and withCredentials to outbound HTTP requests,
 * and recovers gracefully from 401 Unauthorized responses with deduplicated refresh token rotation.
 */

import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.accessToken();

  // 1. Ensure withCredentials is true for all API calls (passes secure HttpOnly cookies)
  let clonedReq = req.clone({
    withCredentials: true
  });

  // 2. Attach Authorization header if access token exists (skip public endpoints)
  if (token && !req.url.includes('/auth/login') && !req.url.includes('/auth/register')) {
    clonedReq = clonedReq.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
  }

  // 3. Execute request and safely handle unauthorized 401 errors
  return next(clonedReq).pipe(
    catchError((error: HttpErrorResponse) => {
      // Check if this request has ALREADY been retried once
      const isRetry = req.headers.has('X-VitalQ-Retry');

      // Only attempt refresh if:
      // - Response is 401 Unauthorized
      // - Request is NOT already an auth refresh/login call
      // - Request has NOT already been retried (prevents cyclic infinite loops)
      if (error.status === 401 && !isRetry && !req.url.includes('/auth/refresh') && !req.url.includes('/auth/login')) {
        return auth.refreshToken().pipe(
          switchMap(res => {
            // Replay the failed request with the newly issued access token.
            // Tag with 'X-VitalQ-Retry' header to prevent infinite re-entry.
            const retryReq = req.clone({
              withCredentials: true,
              setHeaders: {
                Authorization: `Bearer ${res.accessToken}`,
                'X-VitalQ-Retry': 'true'
              }
            });
            return next(retryReq);
          })
          // Note: If refreshToken() fails, AuthService centrally wipes session
          // and routes to /login once, preventing duplicate logout storms.
        );
      }

      return throwError(() => error);
    })
  );
};
