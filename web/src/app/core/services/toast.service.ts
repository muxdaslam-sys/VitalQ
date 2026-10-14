/**
 * ============================================================================
 * SERVICE: ToastService (Clinical Notification & Alert System)
 * ============================================================================
 * 
 * PURPOSE:
 * Provides a modern, non-blocking in-app notification system that replaces
 * archaic browser native alert() dialogs.
 * 
 * ARCHITECTURE:
 * - Powered by Angular 19 Signals (reactive toasts list).
 * - Exposes simple methods: success(), error(), warning(), info().
 * - Each toast automatically dismisses itself after a configurable duration.
 * - Single source of truth used across Admin, Doctor, and Nurse modules.
 * ============================================================================
 */

import { Injectable, signal } from '@angular/core';

/**
 * Data model for an individual toast alert message.
 */
export interface ToastMessage {
  /** Unique ID used to identify and dismiss this toast */
  id: string;
  /** Severity level determining background color, border, and icon */
  type: 'success' | 'error' | 'info' | 'warning';
  /** Optional bold title heading displayed above the message */
  title?: string;
  /** Primary descriptive message explaining what happened */
  message: string;
  /** Milliseconds before auto-dismissal (0 = persist until dismissed) */
  duration?: number;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  // --------------------------------------------------------------------------
  // STATE: REACTIVE TOAST LIST
  // Using an Angular Signal so any UI component (e.g. ToastContainer)
  // re-renders automatically whenever a message is added or removed.
  // --------------------------------------------------------------------------
  toasts = signal<ToastMessage[]>([]);

  /**
   * Displays a toast notification on the screen.
   * 
   * @param message Text content of the notification.
   * @param type Severity level ('success' | 'error' | 'info' | 'warning').
   * @param title Optional title header.
   * @param duration Time in milliseconds to show before auto-hiding (default: 3500ms).
   */
  show(
    message: string,
    type: 'success' | 'error' | 'info' | 'warning' = 'info',
    title?: string,
    duration = 3500
  ): void {
    const id = Math.random().toString(36).substring(2, 9);
    const toast: ToastMessage = { id, type, title, message, duration };

    // Append the new toast to the signal array
    this.toasts.update(list => [...list, toast]);

    // Schedule auto-removal if a positive duration is specified
    if (duration > 0) {
      setTimeout(() => this.remove(id), duration);
    }
  }

  /**
   * Helper shortcut: Display a green success confirmation toast.
   * Use for successful creates, updates, status changes, and password resets.
   */
  success(message: string, title = 'Success'): void {
    this.show(message, 'success', title, 3500);
  }

  /**
   * Helper shortcut: Display a red error alert toast.
   * Stays on screen longer (5000ms) so users can read the error message.
   */
  error(message: string, title = 'Operation Failed'): void {
    this.show(message, 'error', title, 5000);
  }

  /**
   * Helper shortcut: Display an amber warning toast.
   * Use for validation warnings or missing mandatory form fields.
   */
  warning(message: string, title = 'Attention'): void {
    this.show(message, 'warning', title, 4000);
  }

  /**
   * Helper shortcut: Display a blue/teal informational notice toast.
   * Use for clipboard copy confirmations or routine system notices.
   */
  info(message: string, title = 'Notice'): void {
    this.show(message, 'info', title, 3000);
  }

  /**
   * Removes a specific toast from the screen by its unique ID.
   */
  remove(id: string): void {
    this.toasts.update(list => list.filter(t => t.id !== id));
  }
}
