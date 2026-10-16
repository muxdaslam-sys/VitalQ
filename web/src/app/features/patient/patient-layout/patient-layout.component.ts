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

import { Component, inject, signal, ElementRef, HostListener, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ToastContainerComponent } from '../../../shared/components/toast-container/toast-container.component';

@Component({
  selector: 'app-patient-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet, ToastContainerComponent],
  templateUrl: './patient-layout.component.html',
  styleUrl: './patient-layout.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PatientLayoutComponent {
  //#region 1. Dependencies & Services

  /** Injected singleton AuthService providing current user profile and session methods */
  auth = inject(AuthService);

  /** Injected Angular Router for navigation redirects */
  private router = inject(Router);

  /** Injected ElementRef for detecting clicks outside the profile dropdown */
  private elRef = inject(ElementRef);

  //#endregion

  //#region 2. Reactive State & Dropdown Controls

  /** State controlling visibility of user profile popover menu */
  isDropdownOpen = signal<boolean>(false);

  toggleDropdown(): void {
    this.isDropdownOpen.update(v => !v);
  }

  closeDropdown(): void {
    this.isDropdownOpen.set(false);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.elRef.nativeElement.contains(event.target)) {
      this.closeDropdown();
    }
  }

  //#endregion

  //#region 3. User Identity & Session Controls

  /**
   * Generates uppercase 2-letter initials for user avatar display.
   */
  getUserInitials(): string {
    const name = this.auth.currentUser()?.fullName || 'Patient';
    const parts = name.trim().split(/\s+/);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return parts[0].substring(0, 2).toUpperCase();
  }

  /**
   * Logs out the current patient session, clears JWT tokens from storage,
   * and navigates to the login screen.
   */
  logout(): void {
    this.closeDropdown();
    this.auth.logout().subscribe(() => {
      this.router.navigate(['/login']);
    });
  }

  //#endregion
}
