/**
 * ============================================================================
 * COMPONENT: SkipPatientModalComponent (3-Strike Absent Safety Enforcement)
 * ============================================================================
 * 
 * PURPOSE:
 * Displays clinical warning dialog before holding or expiring an absent patient:
 * - Visualizes current unanswered strikes (Strike 1 to 3).
 * - Enforces hospital 3-strike safety policy:
 *   * Strike 1 & 2: Placed on temporary hold in the Skipped tab.
 *   * Strike 3: Automatically cancelled as No-Show with consultation slot released.
 * - Uses `teleportToBody` to avoid layout container clipping.
 * ============================================================================
 */

import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TeleportToBodyDirective } from '../../../../shared/directives/teleport.directive';
import { QueueToken } from '../../../../shared/models/queue.model';

@Component({
  selector: 'app-skip-patient-modal',
  standalone: true,
  imports: [CommonModule, TeleportToBodyDirective],
  templateUrl: './skip-patient-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SkipPatientModalComponent {
  /** Patient token currently summoned in chamber */
  @Input({ required: true }) currentCalled!: QueueToken;

  /** Loading state flag preventing duplicate requests and controlled by parent */
  @Input() isSubmitting: boolean = false;

  /** Emitted when modal backdrop or cancel button is clicked */
  @Output() close = new EventEmitter<void>();

  /** Emitted when physician confirms marking absent / expiring */
  @Output() confirm = new EventEmitter<void>();

  /**
   * Computed strike number if absent call is confirmed.
   */
  nextStrike(): number {
    return (this.currentCalled.skipCount ?? 0) + 1;
  }

  /**
   * Dispatches confirmation event to parent controller.
   */
  onConfirm(): void {
    if (this.isSubmitting) return;
    this.confirm.emit();
  }
}
