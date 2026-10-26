/**
 * ============================================================================
 * COMPONENT: TriageModalComponent (Vital Signs Intake & CTAS Assessment)
 * ============================================================================
 * 
 * PURPOSE:
 * Provides an isolated modal dialog for clinical nurses to record vital signs:
 * - SpO2, Blood Pressure (Systolic/Diastolic), Heart Rate, Temperature, Pain Scale.
 * - Live CTAS Urgency Preview (Red / Yellow / Green) computed on every keystroke.
 * - Clinical Override support with mandatory justification audit notes.
 * - Teleports to `document.body` to avoid parent layout clipping or z-index stacking issues.
 * ============================================================================
 */

import { Component, Input, Output, EventEmitter, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TeleportToBodyDirective } from '../../../../shared/directives/teleport.directive';
import { NurseService } from '../../../../core/services/nurse.service';
import { QueueToken } from '../../../../shared/models/queue.model';
import { TriageRequest } from '../../../../shared/models/triage.model';

@Component({
  selector: 'app-triage-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, TeleportToBodyDirective],
  templateUrl: './triage-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TriageModalComponent {
  private readonly nurseService = inject(NurseService);

  /** Active token undergoing clinical assessment */
  @Input({ required: true }) token!: QueueToken;

  /** Emitted when modal backdrop or close button is clicked */
  @Output() close = new EventEmitter<void>();

  /** Emitted with validated triage payload upon form submission */
  @Output() saved = new EventEmitter<TriageRequest>();

  /** Indicates asynchronous submission state to prevent duplicate submissions */
  isSubmitting = signal<boolean>(false);

  /** Two-way bound vital signs form state */
  vitalsForm: TriageRequest = {
    spO2: null,
    systolicBp: null,
    diastolicBp: null,
    heartRate: null,
    temperature: null,
    painScale: null,
    isManualOverride: false,
    overrideTriageLevel: null,
    overrideReason: null,
    nurseNotes: null
  };

  /**
   * Real-time CTAS calculation preview based on entered vital signs.
   * Respects manual clinical override if toggled by the nurse.
   */
  effectiveLevel(): string {
    if (this.vitalsForm.isManualOverride && this.vitalsForm.overrideTriageLevel) {
      return this.vitalsForm.overrideTriageLevel;
    }
    return this.nurseService.evaluateLevel(
      this.vitalsForm.spO2,
      this.vitalsForm.systolicBp,
      this.vitalsForm.heartRate,
      this.vitalsForm.temperature,
      this.vitalsForm.painScale
    );
  }

  /**
   * Dispatches triage assessment payload to parent orchestrator.
   */
  onSubmit(): void {
    if (this.isSubmitting()) return;
    this.isSubmitting.set(true);
    this.saved.emit(this.vitalsForm);
  }
}
