/**
 * ============================================================================
 * SERVICE: AuthService (Central Authentication & Session Authority)
 * ============================================================================
 * 
 * PURPOSE:
 * Manages user credentials verification, local token storage, silent background
 * token rotation with concurrency mutex locks, session revocation, and role-based routing.
 * ============================================================================
 */

import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, of, throwError, shareReplay, finalize } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, UserResponse } from '../../shared/models/auth.model';
import { SelfRegisterRequest } from '../../shared/models/patient.model';
import { SignalRService } from './signalr.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  //#region 1. Dependencies & API Configuration

  /** Injected Angular HTTP client for authentication endpoints */
  private http = inject(HttpClient);

  /** Injected Router for centralized session teardown navigation */
  private router = inject(Router);

  /** Injected SignalR service for clean WebSocket disconnection upon session termination */
  private signalR = inject(SignalRService);

  /** Base URL for authentication controller (e.g. http://localhost:5089/api/auth) */
  private authUrl = `${environment.apiUrl}/auth`;

  //#endregion

  //#region 2. Reactive State Signals & Computed Accessors

  /** Current logged-in user profile, initialized from local storage cache */
  currentUser = signal<UserResponse | null>(this.getStoredUser());

  /** Current active JWT Bearer token */
  accessToken = signal<string | null>(this.getStoredToken());

  /** Computed boolean checking if a valid user session is currently active and unexpired */
  isAuthenticated = computed(() => !!this.currentUser() && !this.isTokenExpired());

  /** Computed hospital role string ('Admin' | 'Doctor' | 'Nurse' | 'Patient') */
  userRole = computed(() => this.currentUser()?.role || null);

  //#endregion

  //#region 3. Concurrency-Safe Token Rotation Mutex

  /**
   * Mutex lock observable:
   * Holds the active refresh HTTP stream. Multiple parallel calls share this single
   * observable via `shareReplay({ bufferSize: 1, refCount: true })`.
   * Reset to `null` via `finalize` once all subscribers receive the refreshed token.
   */
  private refreshInProgress$: Observable<AuthResponse> | null = null;

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

  //#endregion

  //#region 4. Authentication & Registration Operations

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
   * Registers a first-time online patient with their phone and DOB.
   * Endpoint: POST /api/patient/self-register
   * Creates an official patient chart, permanent MRN, user identity, and establishes a JWT session.
   * 
   * @param request Self-registration payload with FullName, PhoneNumber, DateOfBirth, Gender.
   */
  selfRegister(request: SelfRegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/patient/self-register`, request, { withCredentials: true }).pipe(
      tap(res => {
        this.setSession(res);
      })
    );
  }

  //#endregion

  //#region 5. Session Revocation & Sign-out

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

  //#endregion

  //#region 6. Role Routing & Storage Helpers

  /**
   * Determines the authorized landing home route for a specific user role.
   * 
   * @param role Hospital clinical or administrative role.
   */
  /**
   * Checks if the active JWT token is expired or within 10 seconds of expiry.
   * Decodes the payload directly without external dependencies.
   */
  isTokenExpired(): boolean {
    const token = this.getStoredToken();
    if (!token) return true;
    try {
      const parts = token.split('.');
      if (parts.length !== 3) return true;
      const payload = JSON.parse(atob(parts[1].replace(/-/g, '+').replace(/_/g, '/')));
      if (!payload.exp) return false;
      const expiryMs = payload.exp * 1000;
      return Date.now() >= (expiryMs - 10000);
    } catch {
      return true;
    }
  }

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
  private setSession(authResult: AuthResponse): void {
    sessionStorage.setItem('vq_token', authResult.accessToken);
    sessionStorage.setItem('vq_user', JSON.stringify(authResult.user));
    localStorage.removeItem('vq_token');
    localStorage.removeItem('vq_user');
    this.accessToken.set(authResult.accessToken);
    this.currentUser.set(authResult.user);
  }

  /**
   * Purges cached session tokens and resets reactive signals.
   */
  private clearSession(): void {
    sessionStorage.removeItem('vq_token');
    sessionStorage.removeItem('vq_user');
    localStorage.removeItem('vq_token');
    localStorage.removeItem('vq_user');
    this.accessToken.set(null);
    this.currentUser.set(null);
  }

  /**
   * Safely reads the stored JWT token from sessionStorage.
   */
  private getStoredToken(): string | null {
    if (typeof window === 'undefined') return null;
    return sessionStorage.getItem('vq_token');
  }

  /**
   * Safely reads and parses the stored user profile from sessionStorage.
   */
  private getStoredUser(): UserResponse | null {
    if (typeof window === 'undefined') return null;
    const raw = sessionStorage.getItem('vq_user');
    if (!raw) return null;
    try {
      return JSON.parse(raw) as UserResponse;
    } catch {
      return null;
    }
  }

  //#endregion
}
