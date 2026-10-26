/**
 * ============================================================================
 * COMPONENT: QueueStatusModalComponent (Real-Time Patient Position & Visualizer)
 * ============================================================================
 * 
 * PURPOSE:
 * Displays real-time queue position telemetry for an individual patient token:
 * - Position indicator (# in line, wait time estimate, patients ahead).
 * - Physician live line visualizer with "This Patient" highlighted.
 * - Re-queue CTA if the patient was previously skipped.
 * ============================================================================
 */

import { Component, Input, Output, EventEmitter, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TeleportToBodyDirective } from '../../../../shared/directives/teleport.directive';
import { NurseService } from '../../../../core/services/nurse.service';
import { ToastService } from '../../../../core/services/toast.service';
import { PatientQueuePositionResponse } from '../../../../shared/models/nurse.model';

@Component({
  selector: 'app-queue-status-modal',
  standalone: true,
  imports: [CommonModule, TeleportToBodyDirective],
  templateUrl: './queue-status-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class QueueStatusModalComponent {
  private readonly nurseService = inject(NurseService);
  private readonly toast = inject(ToastService);

  /** Detailed queue telemetry for active token */
  @Input({ required: true }) queueData!: PatientQueuePositionResponse;

  /** Emitted when modal backdrop or close button is clicked */
  @Output() close = new EventEmitter<void>();

  /** Emitted when a skipped patient is successfully restored */
  @Output() patientRequeued = new EventEmitter<string>();

  /** Loading state during restore request */
  isRequeuing = signal<boolean>(false);

  /**
   * Restores a skipped patient back into the physician's active consultation line.
   */
  onRequeue(tokenId: string, tokenNumber: string): void {
    if (this.isRequeuing()) return;
    this.isRequeuing.set(true);

    this.nurseService.requeuePatient(tokenId).subscribe({
      next: () => {
        this.isRequeuing.set(false);
        this.toast.success(`Token ${tokenNumber} has been restored to active queue.`);
        this.patientRequeued.emit(tokenId);
      },
      error: (err: any) => {
        this.isRequeuing.set(false);
        this.toast.error(err.error?.message || 'Failed to re-queue patient.');
      }
    });
  }
}
