/**
 * ============================================================================
 * COMPONENT: CompleteConsultationModalComponent (Clinical Notes & Discharge)
 * ============================================================================
 * 
 * PURPOSE:
 * Isolated modal dialog for physicians to finalize an active patient encounter:
 * - Summarizes current patient token, name, and total consultation duration.
 * - Captures clinical observations, diagnosis impressions, and prescription instructions.
 * - Enforces mandatory non-empty clinical advice (minimum 5 characters).
 * - Submits notes and dispatches encounter conclusion event to release room chamber.
 * - Uses `teleportToBody` to escape parent layout clipping and guarantee viewport centering.
 * ============================================================================
 */

import { Component, Input, Output, EventEmitter, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TeleportToBodyDirective } from '../../../../shared/directives/teleport.directive';
import { QueueToken } from '../../../../shared/models/queue.model';

@Component({
  selector: 'app-complete-consultation-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, TeleportToBodyDirective],
  templateUrl: './complete-consultation-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CompleteConsultationModalComponent {
  /** Active in-room patient token */
  @Input({ required: true }) currentCalled!: QueueToken;

  /** Formatted stopwatch string of total encounter duration (MM:SS) */
  @Input() consultationTimerText: string = '00:00';

  /** Submission state flag controlled by parent dashboard for clean error recovery */
  @Input() isSubmitting: boolean = false;

  /** Emitted when modal backdrop or cancel button is clicked */
  @Output() close = new EventEmitter<void>();

  /** Emitted with finalized clinical notes to complete consultation */
  @Output() complete = new EventEmitter<string>();

  /** Clinical observations, diagnosis, or prescription text */
  consultationNotes = signal<string>('');

  /**
   * Finalizes the encounter and dispatches the notes to the parent controller.
   */
  onSubmit(): void {
    const trimmed = this.consultationNotes().trim();
    if (this.isSubmitting || trimmed.length < 5) return;
    this.complete.emit(trimmed);
  }
}
