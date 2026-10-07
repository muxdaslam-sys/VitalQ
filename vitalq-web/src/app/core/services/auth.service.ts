/**
 * ============================================================================
 * SERVICE: AuthService (Identity, JWT Authentication & Session Manager)
 * ============================================================================
 * 
 * PURPOSE:
 * Manages user authentication, token storage, concurrent-safe token refresh,
 * session revocation, and role-based route navigation.
 * 
 * HIGH-CONCURRENCY ARCHITECTURE (10K+ USERS):
 * 1. Mutex Deduplication (`shareReplay`):
 *    When a 60-minute JWT expires, multiple parallel clinical API requests
 *    (e.g., patient list, vitals, department status) may fail with 401 at the
 *    exact same millisecond. Without a mutex, the client fires multiple refresh
 *    requests, causing token-rotation race conditions that log the user out.
 *    `refreshInProgress$` ensures exactly ONE refresh request hits the network,
 *    and all other waiting requests safely subscribe to that single result.
 * 
 * 2. Centralized Session Invalidation:
 *    If the 7-day refresh token has genuinely expired or been revoked, cleanup
 *    and redirection to `/login` happens here ONCE, preventing multiple parallel
 *    logout bursts from individual failed requests.
 * 
 * 3. Reactive State Signals:
 *    Angular 19 Signals (`currentUser`, `accessToken`, `isAuthenticated`, `userRole`)
 *    provide zero-overhead, glitch-free UI reactivity across all hospital portals.
 * ============================================================================
 */

import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, of, throwError, shareReplay, finalize } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, UserResponse } from '../../shared/models/auth.model';

import { SignalRService } from './signalr.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  /** Injected Angular HTTP client for authentication endpoints */
  private http = inject(HttpClient);

  /** Injected Router for centralized session teardown navigation */
  private router = inject(Router);

  /** Injected SignalR service for clean WebSocket disconnection upon session termination */
  private signalR = inject(SignalRService);

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

  // --------------------------------------------------------------------------
  // CONCURRENCY MUTEX STATE
  // --------------------------------------------------------------------------

  /**
   * Mutex lock observable:
   * Holds the active refresh HTTP stream. Multiple parallel calls share this single
   * observable via `shareReplay({ bufferSize: 1, refCount: true })`.
   * Reset to `null` via `finalize` once all subscribers receive the refreshed token.
   */
  private refreshInProgress$: Observable<AuthResponse> | null = null;

  /**
   * Authenticates user credentials against the backend API.
   * Endpoint: POST /api/auth/login
   * Automatically sets local session and updates signals on success.
   * 
   * @param credentials Username/Phone and Password payload.
   */
  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.authUrl}/login`, credentials, { withCredentials: true }).pipe(
      tap(res => {
        this.setSession(res);
      })
    );
  }

  /**
   * Concurrency-Safe Silent Token Refresh:
   * Exchanges the HttpOnly cookie for a fresh 60-minute JWT Bearer token.
   * 
   * HIGH-PERFORMANCE BEHAVIOR:
   * - If a refresh is ALREADY in flight, subsequent calls attach to `refreshInProgress$`.
   * - If the refresh fails (session expired/revoked), centrally purges tokens and redirects to `/login`.
   * - Releasing the mutex in `finalize()` guarantees subsequent refreshes 60 minutes later create a new stream.
   */
  refreshToken(): Observable<AuthResponse> {
    // If a refresh request is already pending, deduplicate and reuse it
    if (this.refreshInProgress$) {
      return this.refreshInProgress$;
    }

    this.refreshInProgress$ = this.http.post<AuthResponse>(`${this.authUrl}/refresh`, {}, { withCredentials: true }).pipe(
      tap(res => {
        this.setSession(res);
      }),
      catchError(err => {
        // Centralized single session cleanup & redirect (prevents duplicate logout bursts)
        this.clearSession();
        this.router.navigate(['/login']);
        return throwError(() => err);
      }),
      finalize(() => {
        // Unlock mutex once the request completes and emissions finish
        this.refreshInProgress$ = null;
      }),
      shareReplay({ bufferSize: 1, refCount: true })
    );

    return this.refreshInProgress$;
  }

  /**
   * Terminates the current session on both client and server.
   * Endpoint: POST /api/auth/logout
   */
  logout(): Observable<any> {
    // Teardown real-time WebSocket connection to prevent channel leakage on shared clinical workstations
    this.signalR.stopConnection();

    return this.http.post(`${this.authUrl}/logout`, {}, { withCredentials: true }).pipe(
      tap(() => {
        this.clearSession();
        this.router.navigate(['/login']);
      }),
      catchError(() => {
        this.clearSession();
        this.router.navigate(['/login']);
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
