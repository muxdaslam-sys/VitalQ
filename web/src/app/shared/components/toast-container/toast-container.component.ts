/**
 * ============================================================================
 * COMPONENT: ToastContainerComponent (Floating Toast Notifications Host)
 * ============================================================================
 * 
 * PURPOSE:
 * Renders floating clinical alerts in the top-right corner of the screen.
 * Automatically displays and animates toasts created by ToastService.
 * ============================================================================
 */

import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './toast-container.component.html'
})
export class ToastContainerComponent {
  /** Injected singleton instance of ToastService to read active toasts */
  toastService = inject(ToastService);
}
