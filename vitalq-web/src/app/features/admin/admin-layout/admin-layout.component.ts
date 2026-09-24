/**
 * ============================================================================
 * COMPONENT: AdminLayoutComponent (Hospital Administration Master Shell)
 * ============================================================================
 * 
 * PURPOSE:
 * Acts as the master wrapper layout for all administrative views:
 * - /admin/dashboard
 * - /admin/doctors
 * - /admin/staff
 * - /admin/departments
 * - /admin/nursing-stations
 * - /admin/patients
 * 
 * CORE RESPONSIBILITIES:
 * 1. Mobile-Responsive Navigation Drawer (Hamburger button toggle for phones & tablets).
 * 2. Hospital Operational Topbar (Displays live system clock, active OPD shift, dynamic page titles).
 * 3. Mounts the global ToastContainerComponent for non-blocking clinical alerts.
 * 4. User Session & Logout controls.
 * ============================================================================
 */

import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet, NavigationEnd } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../core/services/auth.service';
import { ToastContainerComponent } from '../../../shared/components/toast-container/toast-container.component';
import { filter } from 'rxjs/operators';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, ToastContainerComponent],
  templateUrl: './admin-layout.component.html',
  styleUrl: './admin-layout.component.css'
})
export class AdminLayoutComponent implements OnInit, OnDestroy {
  // --------------------------------------------------------------------------
  // 1. DEPENDENCY INJECTION
  // --------------------------------------------------------------------------
  auth = inject(AuthService);
  private router = inject(Router);

  // --------------------------------------------------------------------------
  // 2. COMPONENT STATE (SIGNALS)
  // --------------------------------------------------------------------------
  
  /** Controls visibility of the slide-out navigation drawer on mobile/tablet screens */
  isMobileDrawerOpen = signal(false);

  /** Real-time clock signal updating every 1000ms for hospital shift awareness */
  currentTime = signal(new Date());

  /** Dynamic breadcrumb page title determined by active route URL */
  pageTitle = signal('Overview Dashboard');

  /** Subscriptions & Interval cleanup references to prevent memory leaks */
  private clockTimer: any;
  private routerSub?: Subscription;

  // --------------------------------------------------------------------------
  // 3. LIFECYCLE HOOKS
  // --------------------------------------------------------------------------
  ngOnInit(): void {
    // Start live hospital clock timer
    this.clockTimer = setInterval(() => {
      this.currentTime.set(new Date());
    }, 1000);

    // Initial page title based on current browser URL
    this.updateTitle(this.router.url);

    // Listen to router navigation changes to automatically update breadcrumbs
    // and close mobile drawer whenever the admin clicks a menu item
    this.routerSub = this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe((event: any) => {
      this.updateTitle(event.urlAfterRedirects || event.url);
      this.isMobileDrawerOpen.set(false);
    });
  }

  ngOnDestroy(): void {
    // Clear timer and unsubscribe on component teardown
    if (this.clockTimer) clearInterval(this.clockTimer);
    this.routerSub?.unsubscribe();
  }

  // --------------------------------------------------------------------------
  // 4. HELPER METHODS
  // --------------------------------------------------------------------------

  /**
   * Sets human-readable title based on the active route.
   */
  updateTitle(url: string): void {
    if (url.includes('/doctors')) {
      this.pageTitle.set('Doctors & Duty Roster');
    } else if (url.includes('/staff')) {
      this.pageTitle.set('Staff & Clinical Users');
    } else if (url.includes('/departments')) {
      this.pageTitle.set('Departments & Flow Prefixes');
    } else if (url.includes('/nursing-stations')) {
      this.pageTitle.set('Nursing Triage Desks');
    } else if (url.includes('/patients')) {
      this.pageTitle.set('Patient Directory & History');
    } else {
      this.pageTitle.set('Clinical Overview Dashboard');
    }
  }

  /**
   * Toggles the mobile drawer menu between open and closed.
   */
  toggleDrawer(): void {
    this.isMobileDrawerOpen.update(open => !open);
  }

  /**
   * Terminates administrative session and redirects to the login screen.
   */
  logout(): void {
    this.auth.logout().subscribe(() => {
      this.router.navigate(['/login']);
    });
  }
}
