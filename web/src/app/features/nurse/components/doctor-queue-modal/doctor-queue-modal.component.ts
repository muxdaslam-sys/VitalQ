/**
 * ============================================================================
 * COMPONENT: DoctorQueueModalComponent (Live Consultation Chamber Line)
 * ============================================================================
 * 
 * PURPOSE:
 * Displays the complete real-time queue of patients assigned to a specific physician:
 * - Current consultation token and status badge.
 * - Dynamic priority score breakdown and CTAS urgency color coding.
 * - Expandable accordion for recorded vital signs and triage notes.
 * - One-click re-queue action for skipped patients.
 * ============================================================================
 */

import { Component, Input, Output, EventEmitter, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TeleportToBodyDirective } from '../../../../shared/directives/teleport.directive';
import { NurseService } from '../../../../core/services/nurse.service';
import { ToastService } from '../../../../core/services/toast.service';
import { DoctorDetailedQueueResponse } from '../../../../shared/models/nurse.model';

@Component({
  selector: 'app-doctor-queue-modal',
  standalone: true,
  imports: [CommonModule, TeleportToBodyDirective],
  templateUrl: './doctor-queue-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DoctorQueueModalComponent {
  private readonly nurseService = inject(NurseService);
  private readonly toast = inject(ToastService);

  /** Detailed doctor queue data payload */
  @Input({ required: true }) queueData!: DoctorDetailedQueueResponse;

  /** Emitted when modal backdrop or close button is clicked */
  @Output() close = new EventEmitter<void>();

  /** Emitted when a skipped patient is successfully restored to queue */
  @Output() patientRequeued = new EventEmitter<string>();

  /** Loading state flag during requeue operation */
  isRequeuing = signal<boolean>(false);

  /** ID of token currently expanded to view vitals accordion */
  expandedTokenId = signal<string | null>(null);

  /**
   * Toggles vital signs accordion row visibility.
   */
  toggleExpand(tokenId: string): void {
    this.expandedTokenId.update(curr => curr === tokenId ? null : tokenId);
  }

  /**
   * Restores a skipped patient back into the doctor's active waiting line.
   * Routine patients re-enter 3 slots back; Emergency patients re-enter at Position #1.
   */
  onRequeue(tokenId: string, patientName: string): void {
    if (this.isRequeuing()) return;
    this.isRequeuing.set(true);

    this.nurseService.requeuePatient(tokenId).subscribe({
      next: () => {
        this.isRequeuing.set(false);
        this.toast.success(`${patientName} has been restored to active queue.`);
        this.patientRequeued.emit(tokenId);
      },
      error: (err: any) => {
        this.isRequeuing.set(false);
        this.toast.error(err.error?.message || 'Failed to re-queue patient.');
      }
    });
  }
}
