/**
 * ============================================================================
 * COMPONENT: PatientChartModalComponent (EMR Clinical Profile & Visit History)
 * ============================================================================
 * 
 * PURPOSE:
 * Displays full demographic profile, MRN, contact information, and lifetime visit history
 * for a patient looked up at the nurse desk:
 * - Demographics: Full Name, MRN, Phone Number, Date of Birth, Computed Age, Gender.
 * - Historical Encounters: Prior consultations, attending physicians, and clinical consultation notes.
 * ============================================================================
 */

import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TeleportToBodyDirective } from '../../../../shared/directives/teleport.directive';
import { PatientDetailResponse } from '../../../../shared/models/admin.model';

@Component({
  selector: 'app-patient-chart-modal',
  standalone: true,
  imports: [CommonModule, TeleportToBodyDirective],
  templateUrl: './patient-chart-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PatientChartModalComponent {
  /** Detailed patient medical record payload */
  @Input({ required: true }) patient!: PatientDetailResponse;

  /** Emitted when modal backdrop or close button is clicked */
  @Output() close = new EventEmitter<void>();

  /**
   * Computes current integer age from ISO date-of-birth string.
   */
  calculateAge(dobString?: string): number | null {
    if (!dobString) return null;
    const dob = new Date(dobString);
    if (isNaN(dob.getTime())) return null;
    const today = new Date();
    let age = today.getFullYear() - dob.getFullYear();
    const m = today.getMonth() - dob.getMonth();
    if (m < 0 || (m === 0 && today.getDate() < dob.getDate())) {
      age--;
    }
    return Math.max(0, age);
  }
}
