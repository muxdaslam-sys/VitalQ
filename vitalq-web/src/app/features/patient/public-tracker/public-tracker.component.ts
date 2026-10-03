/**
 * ============================================================================
 * COMPONENT: PublicTrackerComponent (Walk-In Paper Slip Live Queue Tracker)
 * ============================================================================
 * 
 * PURPOSE:
 * Provides a lightweight, mobile-first, public live tracker for walk-in OPD
 * patients holding a physical token slip (e.g. CARD-104) printed by the reception desk.
 * 
 * CORE FEATURES:
 * 1. Anonymous Access: Does not require login, PIN, or patient portal account.
 * 2. Deep-Linking Support: Reads ?token=... from URL query parameters.
 * 3. Real-Time WebSocket Push: Subscribes to SignalR for instant queue shifts.
 * 4. Audio-Visual Summon Alert: Triggers an audio chime and flashing emergency
 *    room banner when the doctor presses "Call Next".
 * ============================================================================
 */

import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { BookingService } from '../../../core/services/booking.service';
import { SignalRService } from '../../../core/services/signalr.service';
import { QueueToken } from '../../../shared/models/queue.model';

@Component({
  selector: 'app-public-tracker',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './public-tracker.component.html'
})
export class PublicTrackerComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private bookingService = inject(BookingService);
  private signalR = inject(SignalRService);

  // --------------------------------------------------------------------------
  // REACTIVE STATE (SIGNALS)
  // --------------------------------------------------------------------------
  tokenInput = signal<string>('');
  token = signal<QueueToken | null>(null);
  isLoading = signal<boolean>(false);
  errorMessage = signal<string | null>(null);
  hasCalledAlerted = signal<boolean>(false);

  /** Cleanup registry for active subscriptions */
  private subs: Subscription[] = [];

  // --------------------------------------------------------------------------
  // LIFECYCLE HOOKS
  // --------------------------------------------------------------------------
  ngOnInit(): void {
    // 1. Establish SignalR WebSocket connection for real-time live push
    this.initSignalR();

    // 2. Read query params (e.g. /track?token=CARD-104)
    const sub = this.route.queryParams.subscribe(params => {
      const tokenNumber = params['token'];
      if (tokenNumber) {
        this.tokenInput.set(tokenNumber);
        this.fetchToken(tokenNumber);
      }
    });
    this.subs.push(sub);
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s.unsubscribe());
  }

  // --------------------------------------------------------------------------
  // SEARCH & DATA FETCHING
  // --------------------------------------------------------------------------
  onSearch(): void {
    const raw = this.tokenInput().trim().toUpperCase();
    if (!raw) return;
    this.router.navigate([], { relativeTo: this.route, queryParams: { token: raw } });
  }

  fetchToken(tokenNumber: string): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.bookingService.trackToken(tokenNumber).subscribe({
      next: (data) => {
        this.token.set(data);
        this.isLoading.set(false);
        this.checkIfCalled(data);
      },
      error: (err) => {
        this.token.set(null);
        this.isLoading.set(false);
        this.errorMessage.set(err.error?.message || `No active token found matching "${tokenNumber}" for today.`);
      }
    });
  }

  // --------------------------------------------------------------------------
  // SIGNALR REAL-TIME SYNC & AUDIO NOTIFICATION
  // --------------------------------------------------------------------------
  private initSignalR(): void {
    this.signalR.startConnection().then(() => {
      // Refresh token if doctor calls patient or queue moves
      const callSub = this.signalR.onPatientCalled$.subscribe(calledToken => {
        if (this.token() && this.token()!.id === calledToken.id) {
          this.token.set(calledToken);
          this.checkIfCalled(calledToken);
        }
      });

      const updateSub = this.signalR.onQueueUpdated$.subscribe(updatedToken => {
        if (this.token() && this.token()!.id === updatedToken.id) {
          this.token.set(updatedToken);
          this.checkIfCalled(updatedToken);
        }
      });

      this.subs.push(callSub, updateSub);
    });
  }

  private checkIfCalled(t: QueueToken): void {
    if (t.status === 'Called' && !this.hasCalledAlerted()) {
      this.hasCalledAlerted.set(true);
      this.playChime();
    }
  }

  /**
   * Generates a modern dual-tone hospital chime using Web Audio API.
   * Runs natively in browser without requiring external audio asset files.
   */
  private playChime(): void {
    try {
      const audioCtx = new (window.AudioContext || (window as any).webkitAudioContext)();
      const osc = audioCtx.createOscillator();
      const gain = audioCtx.createGain();
      osc.type = 'sine';
      osc.frequency.setValueAtTime(587.33, audioCtx.currentTime); // D5
      osc.frequency.setValueAtTime(880.00, audioCtx.currentTime + 0.2); // A5
      gain.gain.setValueAtTime(0.2, audioCtx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.001, audioCtx.currentTime + 0.9);
      osc.connect(gain);
      gain.connect(audioCtx.destination);
      osc.start();
      osc.stop(audioCtx.currentTime + 0.9);
    } catch {
      // Audio autoplay gracefully handled
    }
  }
}
