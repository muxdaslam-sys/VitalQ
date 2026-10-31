/**
 * ============================================================================
 * COMPONENT: EmergencyWalkinModalComponent (Emergency & Fast-Track Intake)
 * ============================================================================
 * 
 * PURPOSE:
 * Provides a dedicated registration flow for unexpected emergency walk-in patients:
 * - 10-digit phone lookup auto-detecting returning registered patients.
 * - Code-Red emergency bypass switch with mandatory clinical justification remarks.
 * - Assigned physician selector populated with on-duty clinic doctors.
 * - Post-booking token confirmation badge.
 * ============================================================================
 */

import { Component, Input, Output, EventEmitter, OnChanges, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TeleportToBodyDirective } from '../../../../shared/directives/teleport.directive';
import { AuthService } from '../../../../core/services/auth.service';
import { NurseService } from '../../../../core/services/nurse.service';
import { ToastService } from '../../../../core/services/toast.service';
import { DoctorQueueSummaryDto, EmergencyWalkInBookingRequest } from '../../../../shared/models/nurse.model';
import { PatientSearchResult } from '../../../../shared/models/triage.model';
import { QueueToken } from '../../../../shared/models/queue.model';

@Component({
  selector: 'app-emergency-walkin-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, TeleportToBodyDirective],
  templateUrl: './emergency-walkin-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EmergencyWalkinModalComponent implements OnChanges {
  /** Optional pre-filled patient from Master Directory search */
  @Input() prefillPatient: PatientSearchResult | null = null;

  ngOnChanges(): void {
    if (this.prefillPatient) {
      this.walkInForm.phoneNumber = this.prefillPatient.phoneNumber;
      this.walkInForm.fullName = this.prefillPatient.fullName;
      this.walkInForm.gender = this.prefillPatient.gender || 'Male';
      if (this.prefillPatient.dateOfBirth) {
        this.walkInForm.dateOfBirth = this.prefillPatient.dateOfBirth.split('T')[0];
      }
      this.existingPatientMatch.set(this.prefillPatient);
    }
    if (this.doctors && this.doctors.length > 0 && !this.walkInForm.doctorId) {
      const availableDoc = this.doctors.find(d => d.status !== 'OnLeave');
      this.walkInForm.doctorId = availableDoc ? availableDoc.doctorId : this.doctors[0].doctorId;
    }
  }
  readonly auth = inject(AuthService);
  private readonly nurseService = inject(NurseService);
  private readonly toast = inject(ToastService);

  /** Active clinic physician roster */
  @Input({ required: true }) doctors: DoctorQueueSummaryDto[] = [];

  /** Emitted when modal backdrop or close button is clicked */
  @Output() close = new EventEmitter<void>();

  /** Emitted with newly generated queue token upon successful intake */
  @Output() booked = new EventEmitter<QueueToken>();

  /** Submission state flag preventing double bookings */
  isSubmitting = signal<boolean>(false);

  /** Holds returning patient detected via phone lookup */
  existingPatientMatch = signal<PatientSearchResult | null>(null);

  /** Newly issued token to display confirmation state */
  successToken = signal<QueueToken | null>(null);

  /** Form state model */
  walkInForm: EmergencyWalkInBookingRequest = {
    fullName: '',
    phoneNumber: '',
    dateOfBirth: '',
    gender: 'Male',
    doctorId: '',
    isEmergencyCodeRed: false,
    clinicalNotes: ''
  };

  /**
   * Automatically inspects entered phone number for returning patients on input or blur.
   */
  onPhoneChange(): void {
    const phone = this.walkInForm.phoneNumber?.trim() || '';
    if (phone.length >= 10) {
      this.nurseService.searchPatients(phone).subscribe({
        next: (res: PatientSearchResult[]) => {
          if (res && res.length > 0) {
            const match = res[0];
            this.existingPatientMatch.set(match);
            this.walkInForm.fullName = match.fullName;
            this.walkInForm.gender = match.gender || 'Male';
            if (match.dateOfBirth) {
              this.walkInForm.dateOfBirth = match.dateOfBirth.split('T')[0];
            }
          } else {
            this.existingPatientMatch.set(null);
          }
        },
        error: () => this.existingPatientMatch.set(null)
      });
    } else {
      this.existingPatientMatch.set(null);
    }
  }

  /**
   * Validates mandatory clinical inputs and submits walk-in registration.
   */
  onDone(): void {
    const token = this.successToken();
    if (token) {
      this.booked.emit(token);
    } else {
      this.close.emit();
    }
  }

  printSlip(): void {
    window.print();
  }

  onSubmit(): void {
    if (!this.walkInForm.fullName.trim() || !this.walkInForm.phoneNumber.trim() || !this.walkInForm.doctorId) {
      this.toast.warning('Please complete patient name, phone number, and select a doctor.');
      return;
    }

    if (this.walkInForm.isEmergencyCodeRed && !this.walkInForm.clinicalNotes?.trim()) {
      this.toast.warning('Clinical emergency remarks are mandatory for Code-Red emergency bypass.');
      return;
    }

    this.isSubmitting.set(true);
    this.nurseService.processEmergencyWalkIn(this.walkInForm).subscribe({
      next: (token) => {
        this.isSubmitting.set(false);
        this.successToken.set(token);
        this.toast.success(`Token ${token.tokenNumber} issued successfully.`);
      },
      error: (err: any) => {
        this.isSubmitting.set(false);
        this.toast.error(err.error?.message || 'Failed to issue emergency walk-in token.');
      }
    });
  }
}
