import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
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
  styleUrls: ['./patient-book.component.css']
})
export class PatientBookComponent implements OnInit {
  private bookingService = inject(BookingService);
  private patientService = inject(PatientService);
  private toast = inject(ToastService);
  private router = inject(Router);

  // Wizard Step (1: Patient, 2: Department, 3: Doctor & Confirm)
  currentStep = signal<number>(1);

  // Selection signals
  selectedPatientId = signal<string>('');
  selectedDepartmentId = signal<string>('');
  selectedDoctorId = signal<string>('');

  // Data lists
  familyMembers = signal<PatientProfile[]>([]);
  departments = signal<Department[]>([]);
  doctors = signal<Doctor[]>([]);

  // State
  loadingFamily = signal<boolean>(true);
  loadingDepartments = signal<boolean>(true);
  loadingDoctors = signal<boolean>(false);
  submitting = signal<boolean>(false);
  errorMessage = signal<string>('');

  ngOnInit(): void {
    this.loadInitialData();
  }

  private loadInitialData(): void {
    // 1. Load family members
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

    // 2. Load active departments
    this.bookingService.getDepartments().subscribe({
      next: (depts) => {
        this.departments.set(depts.filter(d => d.isActive));
        this.loadingDepartments.set(false);
      },
      error: () => this.loadingDepartments.set(false)
    });
  }

  selectPatient(patientId: string): void {
    this.selectedPatientId.set(patientId);
  }

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

  selectDoctor(doctorId: string): void {
    this.selectedDoctorId.set(doctorId);
  }

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

  getSelectedPatient(): PatientProfile | undefined {
    return this.familyMembers().find(p => p.id === this.selectedPatientId());
  }

  getSelectedDepartment(): Department | undefined {
    return this.departments().find(d => d.id === this.selectedDepartmentId());
  }

  getSelectedDoctor(): Doctor | undefined {
    return this.doctors().find(d => d.id === this.selectedDoctorId());
  }
}
