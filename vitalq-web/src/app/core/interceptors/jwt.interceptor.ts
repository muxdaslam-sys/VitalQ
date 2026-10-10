/**
 * ============================================================================
 * INTERCEPTOR: jwtInterceptor (Bearer Token Injection & Infinite-Loop-Safe 401 Recovery)
 * ============================================================================
 * 
 * PURPOSE:
 * Transparently manages HTTP transport security for all outbound REST API requests.
 * 
 * RESPONSIBILITIES & HIGH-CONCURRENCY SAFEGUARDS:
 * 1. Secure Cookie Transport:
 *    Always sets `withCredentials: true` so the browser delivers the HttpOnly
 *    refresh cookie for authentication endpoints.
 * 
 * 2. Authorization Header Injection:
 *    Attaches `Authorization: Bearer <token>` for all protected endpoints
 *    (skips public authentication endpoints `/auth/login` and `/auth/register`).
 * 
 * 3. Infinite Retry Loop Defense (`X-VitalQ-Retry`):
 *    If an API request is retried with a fresh token and STILL returns 401 (e.g. account
 *    deactivated, revoked role permissions, or corrupt session), the interceptor
 *    detects the `X-VitalQ-Retry` flag and immediately aborts the cycle.
 *    This completely eliminates client-side infinite request loops that could flood
 *    the hospital backend or lock the browser.
 * 
 * 4. Deduplicated Token Recovery:
 *    Delegates 401 recovery to `authService.refreshToken()`, which utilizes an
 *    internal RxJS `shareReplay` mutex so multiple parallel failures share ONE
 *    single network round-trip.
 * ============================================================================
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
