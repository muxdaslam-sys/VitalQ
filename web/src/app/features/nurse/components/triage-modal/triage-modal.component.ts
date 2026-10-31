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

import { Component, Input, Output, EventEmitter, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TeleportToBodyDirective } from '../../../../shared/directives/teleport.directive';
import { NurseService } from '../../../../core/services/nurse.service';
import { ToastService } from '../../../../core/services/toast.service';
import { QueueToken } from '../../../../shared/models/queue.model';
import { TriageRequest } from '../../../../shared/models/triage.model';

@Component({
  selector: 'app-triage-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, TeleportToBodyDirective],
  templateUrl: './triage-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TriageModalComponent implements OnInit {
  ngOnInit(): void {
    if (this.token) {
      this.vitalsForm.spO2 = this.token.spO2 ?? null;
      this.vitalsForm.systolicBp = this.token.systolicBp ?? null;
      this.vitalsForm.diastolicBp = this.token.diastolicBp ?? null;
      this.vitalsForm.heartRate = this.token.heartRate ?? null;
      this.vitalsForm.temperature = this.token.temperature ?? null;
      this.vitalsForm.painScale = this.token.painScale ?? null;
      this.vitalsForm.nurseNotes = this.token.nurseNotes ?? null;
    }
  }
  private readonly nurseService = inject(NurseService);
  private readonly toast = inject(ToastService);

  /** Active token undergoing clinical assessment */
  @Input({ required: true }) token!: QueueToken;

  /** Emitted when modal backdrop or close button is clicked */
  @Output() close = new EventEmitter<void>();

  /** Emitted with validated triage payload upon form submission */
  @Output() saved = new EventEmitter<QueueToken>();

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
      this.vitalsForm.diastolicBp,
      this.vitalsForm.heartRate,
      this.vitalsForm.temperature,
      this.vitalsForm.painScale
    );
  }

  /**
   * Pre-populates override level with current calculated level upon toggle,
   * or clears override data when toggled off.
   */
  onOverrideToggle(): void {
    if (this.vitalsForm.isManualOverride) {
      if (!this.vitalsForm.overrideTriageLevel) {
        this.vitalsForm.overrideTriageLevel = this.effectiveLevel();
      }
    } else {
      this.vitalsForm.overrideTriageLevel = null;
      this.vitalsForm.overrideReason = null;
    }
  }

  /**
   * Validates mandatory inputs and submits triage assessment directly.
   */
  onSubmit(): void {
    if (this.isSubmitting()) return;

    if (this.vitalsForm.isManualOverride) {
      if (!this.vitalsForm.overrideTriageLevel) {
        this.vitalsForm.overrideTriageLevel = this.effectiveLevel();
      }
      if (!this.vitalsForm.overrideReason?.trim()) {
        this.toast.warning('Clinical override justification reason is mandatory.');
        return;
      }
    }

    this.isSubmitting.set(true);
    this.nurseService.submitTriage(this.token.id, this.vitalsForm).subscribe({
      next: (updatedToken) => {
        this.isSubmitting.set(false);
        this.toast.success(`Vitals recorded for ${this.token.tokenNumber}. Routed to Dr. ${this.token.doctorName}.`);
        this.saved.emit(updatedToken);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.toast.error(err.error?.message || 'Failed to submit triage vitals.');
      }
    });
  }
}
