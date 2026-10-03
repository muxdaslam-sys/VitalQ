/**
 * ============================================================================
 * COMPONENT: PatientLayoutComponent (Hospital Patient Portal Master Shell)
 * ============================================================================
 * 
 * PURPOSE:
 * Acts as the master wrapper layout for all patient-facing views.
 * 
 * CORE RESPONSIBILITIES:
 * 1. Persistent Top Header Navigation Bar: Displays hospital branding, user identity,
 *    live WebSocket connectivity status, and responsive navigation tabs.
 * 2. Mobile Bottom Tab Bar: Provides ergonomic thumb-level navigation on mobile devices.
 * 3. Mounts `ToastContainerComponent`: Hosts floating, non-blocking clinical alerts
 *    in the top-right corner across all patient interactions.
 * 4. User Session Controls: Clean logout and session invalidation.
 * ============================================================================
 */

import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ToastContainerComponent } from '../../../shared/components/toast-container/toast-container.component';

@Component({
  selector: 'app-patient-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet, ToastContainerComponent],
  templateUrl: './patient-layout.component.html',
  styleUrl: './patient-layout.component.css'
})
export class PatientLayoutComponent {
  /** Injected singleton AuthService providing current user profile and session methods */
  auth = inject(AuthService);

  /** Injected Angular Router for navigation redirects */
  private router = inject(Router);

  /**
   * Logs out the current patient session, clears JWT tokens from storage,
   * and navigates to the login screen.
   */
  logout(): void {
    this.auth.logout().subscribe(() => {
      this.router.navigate(['/login']);
    });
  }
}
