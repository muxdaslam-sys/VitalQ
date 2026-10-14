/**
 * ============================================================================
 * SERVICE: IdleTimeoutService (Clinical Workstation 15m Auto-Lock Engine)
 * ============================================================================
 * 
 * PURPOSE:
 * Enforces HIPAA Security Rule Section 164.312(a)(2)(iii) (Automatic Logoff)
 * and backs up the clinical login page security badge:
 * "Workstations auto-lock after 15m • HIPAA Section 164.312 Access Audit Logging Enabled"
 * 
 * PERFORMANCE & ARCHITECTURE:
 * - Zone-Isolated Listeners: Event listeners ('mousemove', 'keydown', 'click', 'scroll', 'touchstart')
 *   are attached OUTSIDE the Angular zone via `NgZone.runOutsideAngular`.
 *   This ensures mouse moves and keystrokes NEVER trigger redundant Angular Change Detection cycles,
 *   preserving 60fps rendering across heavy clinical queue dashboards.
 * - Auto-Logout Action: If 15 continuous minutes pass with zero user activity,
 *   the workstation automatically terminates the session via `AuthService.logout()`.
 * ============================================================================
 */

import { Injectable, inject, NgZone } from '@angular/core';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class IdleTimeoutService {
  private auth = inject(AuthService);
  private ngZone = inject(NgZone);

  /** 15 Minutes Inactivity Limit (15m * 60s * 1000ms) */
  private readonly TIMEOUT_MS = 15 * 60 * 1000;

  /** Background browser timer handle */
  private idleTimerId: any = null;

  /** Flag ensuring listeners are registered only once per application lifecycle */
  private isWatching = false;

  /**
   * Begins tracking clinical user input across the workstation.
   * Typically invoked on application initialization (AppComponent).
   */
  startWatching(): void {
    if (this.isWatching || typeof window === 'undefined') return;
    this.isWatching = true;

    this.resetTimer();

    // Attach listeners OUTSIDE Angular zone to avoid triggering change detection on every mouse move
    this.ngZone.runOutsideAngular(() => {
      const activityEvents = ['mousemove', 'keydown', 'click', 'scroll', 'touchstart'];
      activityEvents.forEach(eventName => {
        window.addEventListener(eventName, () => this.resetTimer(), { passive: true });
      });
    });
  }

  /**
   * Resets the 15-minute countdown clock upon verified user input.
   */
  private resetTimer(): void {
    if (this.idleTimerId) {
      clearTimeout(this.idleTimerId);
      this.idleTimerId = null;
    }

    // Only set countdown if an active user session exists
    if (this.auth.isAuthenticated()) {
      this.idleTimerId = setTimeout(() => {
        this.handleTimeout();
      }, this.TIMEOUT_MS);
    }
  }

  /**
   * Executes session auto-lock when inactivity window expires.
   */
  private handleTimeout(): void {
    if (this.auth.isAuthenticated()) {
      console.warn('[VitalQ Security] Workstation idle timeout reached (15m). Auto-locking session per HIPAA Section 164.312.');
      this.ngZone.run(() => {
        this.auth.logout().subscribe();
      });
    }
  }
}
