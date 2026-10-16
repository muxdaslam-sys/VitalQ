/**
 * ============================================================================
 * COMPONENT: LoginComponent (Universal Hospital Authentication Gateway)
 * ============================================================================
 * 
 * PURPOSE:
 * Serves as the universal login portal for both Hospital Staff (Doctors, Nurses, Admins)
 * and Outpatients. Provides mode switching, caps lock detection, password visibility,
 * quick paper slip tracking, and first-time patient self-registration.
 * ============================================================================
 */

import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { LoginRequest } from '../../../shared/models/auth.model';
import { SelfRegisterRequest } from '../../../shared/models/patient.model';
import { TeleportToBodyDirective } from '../../../shared/directives/teleport.directive';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, TeleportToBodyDirective],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoginComponent implements OnInit {
  //#region 1. Dependencies & Services

  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  //#endregion

  //#region 2. Reactive State & Form Models

  /** Active login mode: 'staff' vs 'patient' */
  activeMode = signal<'staff' | 'patient'>('staff');

  /** Login credentials payload */
  credentials: LoginRequest = { username: '', password: '' };

  /** Authentication HTTP request in flight */
  isLoading = signal(false);

  /** Inline error message if authentication fails */
  errorMessage = signal<string | null>(null);

  /** Toggle password plain text visibility */
  showPassword = signal(false);

  /** Remember workstation/device session checkbox */
  rememberStation = signal(true);

  /** Support & access help modal visibility */
  showSupportModal = signal(false);

  /** Keyboard Caps Lock detection flag */
  capsLockOn = signal(false);

  /** Quick Token Tracking for Patients with physical slips */
  showQuickTokenModal = signal(false);
  quickTokenInput = signal('');
  quickTokenError = signal<string | null>(null);

  /** Online Patient Self-Registration Form */
  showRegisterModal = signal(false);
  registerFullName = signal('');
  registerPhone = signal('');
  registerDob = signal('');
  registerGender = signal('Male');
  registerLoading = signal(false);
  registerError = signal<string | null>(null);

  //#endregion

  //#region 3. Lifecycle & Active Session Auto-Redirect

  ngOnInit(): void {
    // PREVENT REDUNDANT RE-AUTHENTICATION:
    // If a clinician or patient returns to the login URL while their session is still active,
    // seamlessly route them directly to their portal rather than prompting for credentials.
    if (this.auth.isAuthenticated()) {
      const returnUrl = this.route.snapshot.queryParams['returnUrl'];
      const target = returnUrl || this.auth.getRoleDefaultRoute(this.auth.userRole() || '');
      this.router.navigateByUrl(target);
    }
  }

  //#endregion

  //#region 4. Mode Switching & UI Toggles

  /**
   * Switches portal mode between Hospital Staff and Patient OPD.
   */
  switchMode(mode: 'staff' | 'patient'): void {
    this.activeMode.set(mode);
    this.errorMessage.set(null);
    this.credentials = { username: '', password: '' };
  }

  /**
   * Toggles password masking.
   */
  togglePassword(): void {
    this.showPassword.update((val) => !val);
  }

  /**
   * Detects whether keyboard Caps Lock is engaged to warn users.
   */
  checkCapsLock(event: KeyboardEvent): void {
    if (event.getModifierState) {
      this.capsLockOn.set(event.getModifierState('CapsLock'));
    }
  }

  //#endregion

  //#region 5. Modal Dialog Controls (Register & Quick Token)

  toggleSupportModal(open: boolean): void {
    this.showSupportModal.set(open);
  }

  toggleQuickTokenModal(open: boolean): void {
    this.showQuickTokenModal.set(open);
    this.quickTokenError.set(null);
    if (!open) {
      this.quickTokenInput.set('');
    }
  }

  toggleRegisterModal(open: boolean): void {
    this.showRegisterModal.set(open);
    this.registerError.set(null);
    if (!open) {
      this.registerFullName.set('');
      this.registerPhone.set('');
      this.registerDob.set('');
      this.registerGender.set('Male');
    }
  }

  //#endregion

  //#region 6. Form Submission & Authentication Handlers

  /**
   * Submits quick token tracking query and navigates to the public live display.
   */
  onQuickTokenSubmit(): void {
    const token = this.quickTokenInput().trim();
    if (!token) {
      this.quickTokenError.set('Please enter a valid token number (e.g. CARD-001 or GEN-012).');
      return;
    }
    this.showQuickTokenModal.set(false);
    this.router.navigate(['/track'], { queryParams: { token: token.toUpperCase() } });
  }

  /**
   * Submits first-time patient self-registration request to the backend.
   */
  onRegisterSubmit(): void {
    const fullName = this.registerFullName().trim();
    const phone = this.registerPhone().trim();
    const dob = this.registerDob();
    const gender = this.registerGender();

    if (!fullName || !phone || !dob) {
      this.registerError.set('Please provide your full legal name, phone number, and date of birth.');
      return;
    }

    this.registerLoading.set(true);
    this.registerError.set(null);

    const payload: SelfRegisterRequest = {
      fullName,
      phoneNumber: phone,
      dateOfBirth: dob,
      gender
    };

    this.auth.selfRegister(payload).subscribe({
      next: (res) => {
        this.registerLoading.set(false);
        this.showRegisterModal.set(false);
        const target = this.auth.getRoleDefaultRoute(res.user.role);
        this.router.navigate([target]);
      },
      error: (err) => {
        this.registerLoading.set(false);
        this.registerError.set(err.error?.message || 'Registration failed. Please check your details or visit the hospital reception counter.');
      }
    });
  }

  /**
   * Authenticates credentials and routes user to their role-specific portal.
   */
  onSubmit(): void {
    if (!this.credentials.username.trim() || !this.credentials.password) {
      const fieldName = this.activeMode() === 'staff' ? 'Staff ID / Username' : 'Mobile / Patient ID';
      this.errorMessage.set(`Please provide both your ${fieldName} and password/PIN.`);
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.auth.login(this.credentials).subscribe({
      next: (res) => {
        this.isLoading.set(false);
        const returnUrl = this.route.snapshot.queryParams['returnUrl'];
        if (returnUrl) {
          this.router.navigateByUrl(returnUrl);
        } else {
          const target = this.auth.getRoleDefaultRoute(res.user.role);
          this.router.navigate([target]);
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        const defaultMsg = this.activeMode() === 'staff' 
          ? 'Authentication failed. Please verify your hospital staff credentials.'
          : 'Patient login failed. Please verify your mobile number or PIN.';
        this.errorMessage.set(err.error?.message || defaultMsg);
      }
    });
  }

  //#endregion
}
