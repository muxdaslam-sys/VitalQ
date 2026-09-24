import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { LoginRequest } from '../../../shared/models/auth.model';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

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
    // Navigate to patient live display with token query
    this.router.navigate(['/patient/dashboard'], { queryParams: { token: token.toUpperCase() } });
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
