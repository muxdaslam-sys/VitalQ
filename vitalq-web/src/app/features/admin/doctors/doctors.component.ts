/**
 * ============================================================================
 * COMPONENT: DoctorsComponent (Physician Fleet & Duty Roster Management)
 * ============================================================================
 * 
 * PURPOSE:
 * Allows Hospital Administrators to:
 * 1. View all doctors, their department, room location, and consultation duration.
 * 2. Search dynamically across names, specialties, room numbers, and usernames.
 * 3. Filter by clinical department and on-duty availability status.
 * 4. Register new physicians and generate login credentials (Username + Password).
 * 5. Update profiles or reset physician credentials if forgotten.
 * 6. Toggle on-duty availability (Available vs OnLeave) with one click.
 * 
 * ARCHITECTURAL DESIGN:
 * - Built on Angular 19 Signals for simple, robust reactivity.
 * - Uses computed() for instantaneous multi-criteria client-side filtering.
 * - Integrates ToastService for professional non-blocking medical feedback.
 * - Enforces submission safety via isSubmitting signal to eliminate double-clicks.
 * ============================================================================
 */

import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { ToastService } from '../../../core/services/toast.service';
import { DoctorResponse, DepartmentResponse, CreateDoctorRequest, UpdateDoctorRequest } from '../../../shared/models/admin.model';

@Component({
  selector: 'app-doctors',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './doctors.component.html',
  styleUrl: './doctors.component.css'
})
export class DoctorsComponent implements OnInit {
  // --------------------------------------------------------------------------
  // 1. DEPENDENCY INJECTION
  // --------------------------------------------------------------------------
  private adminService = inject(AdminService);
  private toast = inject(ToastService);

  // --------------------------------------------------------------------------
  // 2. COMPONENT STATE (SIGNALS)
  // --------------------------------------------------------------------------

  /** Master list of physicians returned by the database */
  doctors = signal<DoctorResponse[]>([]);

  /** Available hospital departments used for mapping and filter dropdowns */
  departments = signal<DepartmentResponse[]>([]);

  /** Controls display of the Register New Doctor modal popup */
  showModal = signal(false);

  /** Stores the ID of the doctor currently being edited (null = modal closed) */
  editingDoctorId = signal<string | null>(null);

  /** Indicates an HTTP request is in-flight; disables submit buttons to prevent double-clicks */
  isSubmitting = signal(false);

  /** Text query entered in the search bar */
  searchQuery = signal('');

  /** Selected department ID filter ('all' displays all departments) */
  selectedDepartmentId = signal('all');

  /** Selected duty status filter ('all', 'Available', or 'OnLeave') */
  selectedStatus = signal<'all' | 'Available' | 'OnLeave'>('all');

  /** Map tracking which doctor passwords are unmasked in the table view */
  visiblePasswords = signal<{ [id: string]: boolean }>({});

  /** Temporary key tracking which credential was just copied to show a checkmark */
  copiedText = signal<string | null>(null);

  /** Form state model for registering a new physician */
  newDoc: CreateDoctorRequest = {
    username: '', password: '', fullName: '', phoneNumber: '',
    departmentId: '', specialization: '', roomNumber: '', avgConsultationMinutes: 10
  };

  /** Form state model for updating an existing physician */
  editDoc: UpdateDoctorRequest = {
    fullName: '', password: '', departmentId: '', specialization: '',
    roomNumber: '', status: 'Available', avgConsultationMinutes: 10
  };

  // --------------------------------------------------------------------------
  // 3. COMPUTED REACTIVE FILTER
  // Automatically re-evaluates whenever searchQuery, selectedDepartmentId,
  // or selectedStatus changes. No manual event handlers required!
  // --------------------------------------------------------------------------
  filteredDoctors = computed(() => {
    let list = this.doctors();
    const query = this.searchQuery().trim().toLowerCase();
    const dept = this.selectedDepartmentId();
    const status = this.selectedStatus();

    // 1. Text Search Filter (name, specialization, room, username)
    if (query) {
      list = list.filter(d => 
        d.doctorName.toLowerCase().includes(query) ||
        d.specialization.toLowerCase().includes(query) ||
        d.username.toLowerCase().includes(query) ||
        d.roomNumber.toLowerCase().includes(query)
      );
    }

    // 2. Department Category Filter
    if (dept !== 'all') {
      list = list.filter(d => d.departmentId === dept);
    }

    // 3. Duty Status Filter
    if (status !== 'all') {
      list = list.filter(d => d.status === status);
    }

    return list;
  });

  // --------------------------------------------------------------------------
  // 4. LIFECYCLE HOOKS & DATA LOADING
  // --------------------------------------------------------------------------
  ngOnInit(): void {
    this.loadData();
  }

  /**
   * Fetches latest physician and department rosters from backend.
   */
  loadData(): void {
    this.adminService.getDoctors().subscribe({
      next: docs => this.doctors.set(docs),
      error: () => {
        this.doctors.set([]);
        this.toast.error('Failed to load doctor roster from server.');
      }
    });

    this.adminService.getDepartments().subscribe({
      next: depts => this.departments.set(depts),
      error: () => this.departments.set([])
    });
  }

  // --------------------------------------------------------------------------
  // 5. USER ACTIONS & CREDENTIAL CONTROLS
  // --------------------------------------------------------------------------

  /**
   * Toggles masked (••••••••) vs plaintext password display for a doctor row.
   */
  togglePasswordVisibility(id: string): void {
    this.visiblePasswords.update(map => ({ ...map, [id]: !map[id] }));
  }

  /**
   * Copies credential text to user clipboard with visual confirmation and toast.
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
   * Resets form state and opens the Register New Doctor modal.
   */
  openAddModal(): void {
    this.newDoc = {
      username: '', password: '', fullName: '', phoneNumber: '',
      departmentId: '', specialization: '', roomNumber: '', avgConsultationMinutes: 10
    };
    this.showModal.set(true);
  }

  /**
   * Validates and submits new physician registration to the backend.
   */
  saveDoctor(): void {
    if (!this.newDoc.fullName || !this.newDoc.username || !this.newDoc.password || !this.newDoc.departmentId) {
      this.toast.warning('Please complete all required fields (Name, User, Password, Department).');
      return;
    }

    this.isSubmitting.set(true);
    this.adminService.createDoctor(this.newDoc).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.showModal.set(false);
        this.toast.success(`Dr. ${this.newDoc.fullName} was registered successfully.`);
        this.loadData();
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.toast.error(err.error?.message || 'Failed to save doctor.');
      }
    });
  }

  /**
   * Pre-fills edit form with selected physician details and opens edit modal.
   */
  openEditModal(doc: DoctorResponse): void {
    this.editingDoctorId.set(doc.id);
    this.editDoc = {
      fullName: doc.doctorName,
      password: '', // Blank by default so password is not overwritten unless desired
      departmentId: doc.departmentId,
      specialization: doc.specialization,
      roomNumber: doc.roomNumber,
      status: doc.status,
      avgConsultationMinutes: doc.avgConsultationMinutes
    };
  }

  /**
   * Submits physician updates (and optional password reset) to backend.
   */
  updateDoctor(): void {
    const id = this.editingDoctorId();
    if (!id) return;

    this.isSubmitting.set(true);
    this.adminService.updateDoctor(id, this.editDoc).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.editingDoctorId.set(null);
        this.toast.success('Doctor profile and settings updated.');
        this.loadData();
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.toast.error(err.error?.message || 'Failed to update doctor profile.');
      }
    });
  }

  /**
   * Fast one-click toggle between 'Available' (on duty) and 'OnLeave'.
   */
  toggleStatus(doc: DoctorResponse): void {
    const nextStatus = doc.status === 'Available' ? 'OnLeave' : 'Available';
    this.adminService.toggleDoctorStatus(doc.id, nextStatus).subscribe({
      next: () => {
        this.toast.info(`Dr. ${doc.doctorName} marked as ${nextStatus}.`);
        this.loadData();
      },
      error: () => {
        this.toast.error('Could not toggle doctor status.');
      }
    });
  }
}
