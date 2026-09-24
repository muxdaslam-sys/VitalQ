import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, catchError, of, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, UserResponse } from '../../shared/models/auth.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private authUrl = environment.apiUrl + '/auth';

  // Signals for reactive state
  currentUser = signal<UserResponse | null>(this.getStoredUser());
  accessToken = signal<string | null>(localStorage.getItem('vq_token'));

  isAuthenticated = computed(() => !!this.currentUser());
  userRole = computed(() => this.currentUser()?.role || null);

  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(this.authUrl + '/login', credentials, { withCredentials: true }).pipe(
      tap(res => {
        this.setSession(res);
      })
    );
  }

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

  logout(): Observable<any> {
    return this.http.post(this.authUrl + '/logout', {}, { withCredentials: true }).pipe(
      tap(() => this.clearSession()),
      catchError(() => {
        this.clearSession();
        return of(null);
      })
    );
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

  private setSession(authResult: AuthResponse) {
    localStorage.setItem('vq_token', authResult.accessToken);
    localStorage.setItem('vq_user', JSON.stringify(authResult.user));
    this.accessToken.set(authResult.accessToken);
    this.currentUser.set(authResult.user);
  }

  private clearSession() {
    localStorage.removeItem('vq_token');
    localStorage.removeItem('vq_user');
    this.accessToken.set(null);
    this.currentUser.set(null);
  }

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
