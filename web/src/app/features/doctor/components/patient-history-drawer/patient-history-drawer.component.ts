/**
 * ============================================================================
 * COMPONENT: PatientHistoryDrawerComponent (Longitudinal Medical Dossier)
 * ============================================================================
 * 
 * PURPOSE:
 * Slide-over drawer displaying past consultations, diagnostic impressions, and notes
 * for the summoned or inspected patient:
 * - Timeline of past hospital visits, attending doctors, and clinical advice.
 * - Distinguishes between completed advice and encounter audit remarks.
 * - Teleports to `document.body` for smooth right-aligned slide-over overlay.
 * ============================================================================
 */

import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TeleportToBodyDirective } from '../../../../shared/directives/teleport.directive';
import { PatientVisitHistory } from '../../../../shared/models/patient.model';

@Component({
  selector: 'app-patient-history-drawer',
  standalone: true,
  imports: [CommonModule, TeleportToBodyDirective],
  templateUrl: './patient-history-drawer.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PatientHistoryDrawerComponent {
  /** Patient full name displayed in drawer title */
  @Input({ required: true }) patientName: string = '';

  /** List of past consultations and clinical encounters */
  @Input({ required: true }) historyList: PatientVisitHistory[] = [];

  /** Loading state while retrieving historical records */
  @Input() isLoading: boolean = false;

  /** Emitted when backdrop or close button is clicked */
  @Output() close = new EventEmitter<void>();

  /**
   * Formats ISO timestamp to human-friendly date string.
   */
  formatDate(isoUtc?: string): string {
    if (!isoUtc) return '--';
    return new Date(isoUtc).toLocaleDateString([], { month: 'short', day: 'numeric', year: 'numeric' });
  }
}
