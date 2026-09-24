/**
 * ============================================================================
 * COMPONENT: StaffComponent (Hospital Staff & Clinical Users Management)
 * ============================================================================
 * 
 * PURPOSE:
 * Allows Hospital Administrators to:
 * 1. View all system users (Triage Nurses and System Administrators).
 * 2. Search dynamically across names, phone numbers, and staff IDs/usernames.
 * 3. Filter by role (All, Nurse, Admin) and account status (Active, Suspended).
 * 4. Create new clinical accounts with initial login credentials.
 * 5. Update user information and reset passwords if personnel lose credentials.
 * 6. Activate or Suspend staff access with one click.
 * 
 * ARCHITECTURE:
 * - Angular 19 Signals for clean reactive state management.
 * - Reactive computed filter pipeline.
 * - Non-blocking ToastService notifications instead of browser alerts.
 * - Submission safety (isSubmitting) to guard against duplicate API calls.
 * ============================================================================
 */

import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { ToastService } from '../../../core/services/toast.service';
import { UserResponse, CreateUserRequest, UpdateUserRequest } from '../../../shared/models/admin.model';

@Component({
  selector: 'app-staff',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './staff.component.html',
  styleUrl: './staff.component.css'
})
export class StaffComponent implements OnInit {
  // --------------------------------------------------------------------------
  // 1. DEPENDENCY INJECTION
  // --------------------------------------------------------------------------
  private adminService = inject(AdminService);
  private toast = inject(ToastService);

  // --------------------------------------------------------------------------
  // 2. COMPONENT STATE (SIGNALS)
  // --------------------------------------------------------------------------

  /** Master list of staff accounts loaded from backend */
  staffList = signal<UserResponse[]>([]);

  /** Controls display of the Add Staff User modal */
  showAddModal = signal(false);

  /** Stores ID of staff account currently being edited (null = closed) */
  editingStaffId = signal<string | null>(null);

  /** Indicates an HTTP request is in-flight; disables submit buttons */
  isSubmitting = signal(false);

  /** Query string typed in search bar */
  searchQuery = signal('');

  /** Selected role filter ('all', 'Nurse', or 'Admin') */
  selectedRole = signal<'all' | 'Nurse' | 'Admin'>('all');

  /** Selected status filter ('all', 'active', or 'suspended') */
  selectedStatus = signal<'all' | 'active' | 'suspended'>('all');

  /** Map tracking which staff passwords are unmasked in table */
  visiblePasswords = signal<{ [id: string]: boolean }>({});

  /** Temporary key tracking which credential was copied */
  copiedText = signal<string | null>(null);

  /** Form state model for creating a new staff account */
  newStaff: CreateUserRequest = {
    username: '',
    password: '',
    fullName: '',
    phoneNumber: '',
    role: 'Nurse'
  };

  /** Form state model for editing an existing staff account */
  editStaff: UpdateUserRequest = {
    fullName: '',
    phoneNumber: '',
    role: 'Nurse',
    password: '',
    isActive: true
  };

  // --------------------------------------------------------------------------
  // 3. COMPUTED REACTIVE FILTER
  // --------------------------------------------------------------------------
  filteredStaff = computed(() => {
    let list = this.staffList();
    const query = this.searchQuery().trim().toLowerCase();
    const role = this.selectedRole();
    const status = this.selectedStatus();

    // 1. Text Search Filter
    if (query) {
      list = list.filter(u =>
        u.fullName.toLowerCase().includes(query) ||
        u.username.toLowerCase().includes(query) ||
        u.phoneNumber.toLowerCase().includes(query) ||
        u.role.toLowerCase().includes(query)
      );
    }

    // 2. Role Filter
    if (role !== 'all') {
      list = list.filter(u => u.role === role);
    }

    // 3. Status Filter
    if (status === 'active') {
      list = list.filter(u => u.isActive);
    } else if (status === 'suspended') {
      list = list.filter(u => !u.isActive);
    }

    return list;
  });

  // --------------------------------------------------------------------------
  // 4. LIFECYCLE HOOKS & DATA LOADING
  // --------------------------------------------------------------------------
  ngOnInit(): void {
    this.loadStaff();
  }

  /**
   * Fetches latest staff directory from the backend.
   */
  loadStaff(): void {
    this.adminService.getUsers().subscribe({
      next: users => this.staffList.set(users),
      error: () => {
        this.staffList.set([]);
        this.toast.error('Failed to load staff list from server.');
      }
    });
  }

  // --------------------------------------------------------------------------
  // 5. USER ACTIONS & CREDENTIAL CONTROLS
  // --------------------------------------------------------------------------

  /**
   * Toggles masked vs unmasked password display for a staff row.
   */
  togglePasswordVisibility(id: string): void {
    this.visiblePasswords.update(map => ({ ...map, [id]: !map[id] }));
  }

  /**
   * Copies credential text to clipboard with notification feedback.
   */
  copyToClipboard(text: string, id: string): void {
    navigator.clipboard.writeText(text).then(() => {
      this.copiedText.set(id);
      this.toast.info('Copied credential to clipboard', 'Clipboard');
      setTimeout(() => this.copiedText.set(null), 2000);
    }).catch(() => {
      this.toast.error('Unable to copy to clipboard.');
    });
  }

  /**
   * Resets form state and opens Add Staff modal dialog.
   */
  openAddModal(): void {
    this.newStaff = {
      username: '',
      password: '',
      fullName: '',
      phoneNumber: '',
      role: 'Nurse'
    };
    this.showAddModal.set(true);
  }

  /**
   * Submits new staff account creation request to the API.
   */
  saveStaff(): void {
    if (!this.newStaff.fullName || !this.newStaff.username || !this.newStaff.password) {
      this.toast.warning('Please fill in Name, Username, and Initial Password.');
      return;
    }

    this.isSubmitting.set(true);
    this.adminService.createUser(this.newStaff).subscribe({
      next: (created) => {
        this.isSubmitting.set(false);
        this.showAddModal.set(false);
        this.toast.success(`Account for ${created.fullName} (${created.role}) created successfully.`);
        this.loadStaff();
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.toast.error(err.error?.message || 'Failed to create staff account.');
      }
    });
  }

  /**
   * Pre-populates the edit form with selected user data and opens edit modal.
   */
  openEditModal(user: UserResponse): void {
    this.editingStaffId.set(user.id);
    this.editStaff = {
      fullName: user.fullName,
      phoneNumber: user.phoneNumber,
      role: user.role,
      password: '', // Blank by default so password is not accidentally changed
      isActive: user.isActive
    };
  }

  /**
   * Submits staff profile modifications and optional password reset to backend.
   */
  updateStaff(): void {
    const id = this.editingStaffId();
    if (!id) return;

    this.isSubmitting.set(true);
    this.adminService.updateUser(id, this.editStaff).subscribe({
      next: (updated) => {
        this.isSubmitting.set(false);
        this.editingStaffId.set(null);
        this.toast.success(`Updated account for ${updated.fullName}.`);
        this.loadStaff();
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.toast.error(err.error?.message || 'Failed to update staff user.');
      }
    });
  }

  /**
   * Fast toggle between Active and Suspended status without opening a modal.
   */
  toggleStatus(user: UserResponse): void {
    const updatedReq: UpdateUserRequest = {
      fullName: user.fullName,
      phoneNumber: user.phoneNumber,
      role: user.role,
      isActive: !user.isActive
    };

    this.adminService.updateUser(user.id, updatedReq).subscribe({
      next: () => {
        const state = updatedReq.isActive ? 'Activated' : 'Suspended';
        this.toast.info(`Account for ${user.fullName} is now ${state}.`);
        this.loadStaff();
      },
      error: (err) => {
        this.toast.error(err.error?.message || 'Failed to update account status.');
      }
    });
  }
}
