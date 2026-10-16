/**
 * ============================================================================
 * COMPONENT: PatientBookComponent (3-Step Appointment Booking Wizard)
 * ============================================================================
 * 
 * PURPOSE:
 * Guides the patient through booking an outpatient consultation slot:
 * - Step 1: Patient Selection (self or dependent family profile)
 * - Step 2: Clinical Specialty (Active department selection)
 * - Step 3: Consulting Physician & Booking Confirmation
 * ============================================================================
 */

import { Component, OnInit, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { BookingService } from '../../../core/services/booking.service';
import { PatientService } from '../../../core/services/patient.service';
import { ToastService } from '../../../core/services/toast.service';
import { Department, Doctor } from '../../../shared/models/queue.model';
import { PatientProfile } from '../../../shared/models/patient.model';

@Component({
  selector: 'app-patient-book',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './patient-book.component.html',
  styleUrls: ['./patient-book.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PatientBookComponent implements OnInit {
  //#region 1. Dependencies & Services

  private bookingService = inject(BookingService);
  private patientService = inject(PatientService);
  private toast = inject(ToastService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  //#endregion

  //#region 2. Reactive State & Selection Signals

  /** Wizard step indicator: 1 = Patient, 2 = Department, 3 = Doctor & Confirm */
  currentStep = signal<number>(1);

  /** Selected Patient GUID */
  selectedPatientId = signal<string>('');

  /** Selected Department GUID */
  selectedDepartmentId = signal<string>('');

  /** Selected Doctor GUID */
  selectedDoctorId = signal<string>('');

  /** List of eligible family member profiles */
  familyMembers = signal<PatientProfile[]>([]);

  /** List of active hospital departments */
  departments = signal<Department[]>([]);

  /** Available on-duty doctors in the selected department */
  doctors = signal<Doctor[]>([]);

  /** Loading flags */
  loadingFamily = signal<boolean>(true);
  loadingDepartments = signal<boolean>(true);
  loadingDoctors = signal<boolean>(false);

  /** Booking submission in flight flag */
  submitting = signal<boolean>(false);

  /** Inline error message if booking fails */
  errorMessage = signal<string>('');

  //#endregion

  //#region 3. Computed Derivations

  /** Profile of the currently selected patient */
  selectedPatient = computed(() => this.familyMembers().find(p => p.id === this.selectedPatientId()));

  /** Details of the currently selected department */
  selectedDepartment = computed(() => this.departments().find(d => d.id === this.selectedDepartmentId()));

  /** Profile of the currently selected doctor */
  selectedDoctor = computed(() => this.doctors().find(d => d.id === this.selectedDoctorId()));

  //#endregion

  //#region 4. Lifecycle & Initial Data Fetching

  ngOnInit(): void {
    this.loadInitialData();
  }

  /**
   * Initializes wizard by loading family members, departments, and inspecting deep-links.
   */
  private loadInitialData(): void {
    // 1. Check deep-linked patientId from family page
    this.route.queryParams.subscribe(params => {
      const pid = params['patientId'];
      if (pid) {
        this.selectedPatientId.set(pid);
        this.currentStep.set(2);
      }
    });

    // 2. Load family members
    this.patientService.getMyFamily().subscribe({
      next: (members) => {
        this.familyMembers.set(members);
        if (members.length > 0 && !this.selectedPatientId()) {
          this.selectedPatientId.set(members[0].id);
        }
        this.loadingFamily.set(false);
      },
      error: () => this.loadingFamily.set(false)
    });

    // 3. Load active departments
    this.bookingService.getDepartments().subscribe({
      next: (depts) => {
        this.departments.set(depts.filter(d => d.isActive));
        this.loadingDepartments.set(false);
      },
      error: () => this.loadingDepartments.set(false)
    });
  }

  //#endregion

  //#region 5. Step Selection & Navigation

  /**
   * Selects patient and preserves selection.
   */
  selectPatient(patientId: string): void {
    this.selectedPatientId.set(patientId);
  }

  /**
   * Selects department, resets doctor selection, and loads on-duty doctors.
   */
  selectDepartment(deptId: string): void {
    this.selectedDepartmentId.set(deptId);
    this.selectedDoctorId.set('');
    this.loadingDoctors.set(true);

    this.bookingService.getDoctorsByDepartment(deptId).subscribe({
      next: (docs) => {
        this.doctors.set(docs.filter(d => d.status !== 'OffDuty'));
        this.loadingDoctors.set(false);
      },
      error: () => this.loadingDoctors.set(false)
    });
  }

  /**
   * Selects consulting doctor.
   */
  selectDoctor(doctorId: string): void {
    this.selectedDoctorId.set(doctorId);
  }

  /**
   * Navigates between wizard steps with safety validation guards.
   */
  goToStep(step: number): void {
    if (step === 2 && !this.selectedPatientId()) {
      this.toast.warning('Please select a patient first.');
      return;
    }
    if (step === 3 && !this.selectedDepartmentId()) {
      this.toast.warning('Please select a department first.');
      return;
    }
    this.currentStep.set(step);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  //#endregion

  //#region 6. Booking Confirmation & Submission

  /**
   * Submits booking request to backend, generates collision-free sequence slip,
   * and navigates to the patient dashboard.
   */
  confirmBooking(): void {
    if (!this.selectedPatientId() || !this.selectedDepartmentId() || !this.selectedDoctorId()) {
      this.toast.warning('Please complete all 3 steps before confirming.');
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    this.bookingService.bookToken({
      patientId: this.selectedPatientId(),
      departmentId: this.selectedDepartmentId(),
      doctorId: this.selectedDoctorId()
    }).subscribe({
      next: (token) => {
        this.submitting.set(false);
        this.toast.success(`Token ${token.tokenNumber} issued successfully!`);
        this.router.navigate(['/patient']);
      },
      error: (err) => {
        this.submitting.set(false);
        const msg = err.error?.message || 'Failed to book appointment. Please try again.';
        this.errorMessage.set(msg);
        this.toast.error(msg);
      }
    });
  }

  //#endregion

  //#region 7. Template Getter Accessors

  getSelectedPatient(): PatientProfile | undefined {
    return this.selectedPatient();
  }

  getSelectedDepartment(): Department | undefined {
    return this.selectedDepartment();
  }

  getSelectedDoctor(): Doctor | undefined {
    return this.selectedDoctor();
  }

  //#endregion
}
