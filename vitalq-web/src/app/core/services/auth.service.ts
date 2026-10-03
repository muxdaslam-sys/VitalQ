/**
 * ============================================================================
 * SERVICE: AuthService (Identity, JWT Authentication & Session Manager)
 * ============================================================================
 * 
 * PURPOSE:
 * Manages user authentication, token storage, silent background refresh, and
 * role-based route navigation across all clinical portals.
 * 
 * REACTIVITY ARCHITECTURE:
 * - Angular Signals: Exposes `currentUser` and `accessToken` as signals so any
 *   component or layout reacts instantaneously when login status changes.
 * - Computed Signals: Derives `isAuthenticated` and `userRole` with zero overhead.
 * - Security: Uses `withCredentials: true` so refresh tokens are transported via
 *   secure HttpOnly cookies while the short-lived access token is stored in memory.
 * ============================================================================
 */

import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, catchError, of, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, UserResponse } from '../../shared/models/auth.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  /** Injected Angular HTTP client for authentication endpoints */
  private http = inject(HttpClient);

  /** Base URL for authentication controller (e.g. http://localhost:5089/api/auth) */
  private authUrl = environment.apiUrl + '/auth';

  // --------------------------------------------------------------------------
  // REACTIVE STATE SIGNALS
  // --------------------------------------------------------------------------

  /** Current logged-in user profile, initialized from local storage cache */
  currentUser = signal<UserResponse | null>(this.getStoredUser());

  /** Current active JWT Bearer token */
  accessToken = signal<string | null>(localStorage.getItem('vq_token'));

  /** Computed boolean checking if a valid user session is currently active */
  isAuthenticated = computed(() => !!this.currentUser());

  /** Computed hospital role string ('Admin' | 'Doctor' | 'Nurse' | 'Patient') */
  userRole = computed(() => this.currentUser()?.role || null);

  /**
   * Authenticates user credentials against the backend API.
   * Endpoint: POST /api/auth/login
   * Automatically sets local session and updates signals on success.
   * 
   * @param credentials Username/Phone and Password payload.
   */
  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(this.authUrl + '/login', credentials, { withCredentials: true }).pipe(
      tap(res => {
        this.setSession(res);
      })
    );
  }

  /**
   * Transparently exchanges an expired access token using the HttpOnly refresh cookie.
   * Endpoint: POST /api/auth/refresh
   * Invoked automatically by `jwtInterceptor` upon receiving a 401 Unauthorized error.
   */
  refreshToken(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(this.authUrl + '/refresh', {}, { withCredentials: true }).pipe(
      tap(res => {
        this.setSession(res);
      }),
      catchError(err => {
        this.clearSession();
        return throwError(() => err);
      })
    );
  }

  /**
   * Terminates the current session on both client and server.
   * Endpoint: POST /api/auth/logout
   */
  logout(): Observable<any> {
    return this.http.post(this.authUrl + '/logout', {}, { withCredentials: true }).pipe(
      tap(() => this.clearSession()),
      catchError(() => {
        this.clearSession();
        return of(null);
      })
    );
  }

  /**
   * Determines the authorized landing home route for a specific user role.
   * 
   * @param role Hospital clinical or administrative role.
   */
  getRoleDefaultRoute(role: string): string {
    switch (role) {
      case 'Admin': return '/admin/dashboard';
      case 'Doctor': return '/doctor';
      case 'Nurse': return '/nurse';
      case 'Patient': return '/patient';
      default: return '/login';
    }
  }

  /**
   * Persists authentication tokens and user profile to storage and signals.
   */
  private setSession(authResult: AuthResponse) {
    localStorage.setItem('vq_token', authResult.accessToken);
    localStorage.setItem('vq_user', JSON.stringify(authResult.user));
    this.accessToken.set(authResult.accessToken);
    this.currentUser.set(authResult.user);
  }

  /**
   * Purges cached session tokens and resets reactive signals.
   */
  private clearSession() {
    localStorage.removeItem('vq_token');
    localStorage.removeItem('vq_user');
    this.accessToken.set(null);
    this.currentUser.set(null);
  }

  /**
   * Safely reads and parses the stored user profile from localStorage.
   */
  private getStoredUser(): UserResponse | null {
    const raw = localStorage.getItem('vq_user');
    if (!raw) return null;
    try {
      return JSON.parse(raw) as UserResponse;
    } catch {
      return null;
    }
  }
}
