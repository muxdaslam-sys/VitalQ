import { Component, OnInit, inject, signal } from '@angular/core';
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
  styleUrl: './login.component.css'
})
export class LoginComponent implements OnInit {
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

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

  // Mode Switcher: 'staff' vs 'patient'
  activeMode = signal<'staff' | 'patient'>('staff');

  credentials: LoginRequest = { username: '', password: '' };
  isLoading = signal(false);
  errorMessage = signal<string | null>(null);
  showPassword = signal(false);
  rememberStation = signal(true);
  showSupportModal = signal(false);

  // Caps Lock detection
  capsLockOn = signal(false);

  // Quick Token Tracking for Patients with physical slips
  showQuickTokenModal = signal(false);
  quickTokenInput = signal('');
  quickTokenError = signal<string | null>(null);

  // Online Patient Self-Registration
  showRegisterModal = signal(false);
  registerFullName = signal('');
  registerPhone = signal('');
  registerDob = signal('');
  registerGender = signal('Male');
  registerLoading = signal(false);
  registerError = signal<string | null>(null);

  switchMode(mode: 'staff' | 'patient') {
    this.activeMode.set(mode);
    this.errorMessage.set(null);
    this.credentials = { username: '', password: '' };
  }

  togglePassword() {
    this.showPassword.update((val) => !val);
  }

  toggleSupportModal(open: boolean) {
    this.showSupportModal.set(open);
  }

  toggleQuickTokenModal(open: boolean) {
    this.showQuickTokenModal.set(open);
    this.quickTokenError.set(null);
    if (!open) {
      this.quickTokenInput.set('');
    }
  }

  toggleRegisterModal(open: boolean) {
    this.showRegisterModal.set(open);
    this.registerError.set(null);
    if (!open) {
      this.registerFullName.set('');
      this.registerPhone.set('');
      this.registerDob.set('');
      this.registerGender.set('Male');
    }
  }

  checkCapsLock(event: KeyboardEvent) {
    if (event.getModifierState) {
      this.capsLockOn.set(event.getModifierState('CapsLock'));
    }
  }

  onQuickTokenSubmit() {
    const token = this.quickTokenInput().trim();
    if (!token) {
      this.quickTokenError.set('Please enter a valid token number (e.g. CARD-001 or GEN-012).');
      return;
    }
    this.showQuickTokenModal.set(false);
    // Navigate to public live display with token query
    this.router.navigate(['/track'], { queryParams: { token: token.toUpperCase() } });
  }

  onRegisterSubmit() {
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

  onSubmit() {
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
}
