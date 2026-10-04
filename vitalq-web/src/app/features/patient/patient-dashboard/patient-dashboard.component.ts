import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subscription, timer } from 'rxjs';
import { BookingService } from '../../../core/services/booking.service';
import { SignalRService } from '../../../core/services/signalr.service';
import { ToastService } from '../../../core/services/toast.service';
import { AuthService } from '../../../core/services/auth.service';
import { QueueToken } from '../../../shared/models/queue.model';

@Component({
  selector: 'app-patient-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './patient-dashboard.component.html',
  styleUrls: ['./patient-dashboard.component.css']
})
export class PatientDashboardComponent implements OnInit, OnDestroy {
  // Expose Math for template calculations
  protected readonly Math = Math;

  private bookingService = inject(BookingService);
  private signalR = inject(SignalRService);
  private toast = inject(ToastService);
  auth = inject(AuthService);

  // State Signals
  loading = signal<boolean>(true);
  tokens = signal<QueueToken[]>([]);
  selectedTokenId = signal<string>('');

  // Cancel Appointment Modal
  isCancelModalOpen = signal<boolean>(false);
  cancelReason = signal<string>('');
  cancelling = signal<boolean>(false);
  cancelError = signal<string>('');
  successToast = signal<string>('');

  private subs: Subscription[] = [];
  private previousCalledIds = new Set<string>();

  // Computed Derivations
  selectedToken = computed(() => {
    const list = this.tokens();
    const id = this.selectedTokenId();
    return list.find(t => t.id === id) || (list.length > 0 ? list[0] : null);
  });

  activeTokenCount = computed(() => this.tokens().length);

  ngOnInit(): void {
    this.loadTokens();
    this.initSignalR();
    this.initHeartbeatPoller();
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s.unsubscribe());
  }

  private initHeartbeatPoller(): void {
    const heartbeatSub = timer(45000, 45000).subscribe(() => {
      this.refreshTokensSilently();
    });
    this.subs.push(heartbeatSub);
  }

  loadTokens(): void {
    this.loading.set(true);
    this.bookingService.getMyTokens().subscribe({
      next: (tokens) => {
        this.tokens.set(tokens);
        if (tokens.length > 0 && !this.selectedTokenId()) {
          this.selectedTokenId.set(tokens[0].id);
        }
        this.loading.set(false);
        this.checkCalledTokens(tokens);
      },
      error: () => this.loading.set(false)
    });
  }

  refreshTokensSilently(): void {
    this.bookingService.getMyTokens().subscribe({
      next: (data) => {
        this.tokens.set(data);
        this.checkCalledTokens(data);
      }
    });
  }

  initSignalR(): void {
    this.signalR.startConnection().then(() => {
      const sub1 = this.signalR.onPatientCalled$.subscribe((calledToken) => {
        this.playHospitalChime();
        this.selectedTokenId.set(calledToken.id);
        this.refreshTokensSilently();
      });

      const sub2 = this.signalR.onQueueUpdated$.subscribe(() => {
        this.refreshTokensSilently();
      });

      const sub3 = this.signalR.onScoresRecalculated$.subscribe(() => {
        this.refreshTokensSilently();
      });

      this.subs.push(sub1, sub2, sub3);
    });
  }

  private checkCalledTokens(tokenList: QueueToken[]): void {
    tokenList.forEach(t => {
      if (t.status === 'Called' && !this.previousCalledIds.has(t.id)) {
        this.previousCalledIds.add(t.id);
        this.playHospitalChime();
      }
    });
  }

  private playHospitalChime(): void {
    try {
      const AudioCtx = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      const ctx = new AudioCtx();
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();

      osc.type = 'sine';
      osc.frequency.setValueAtTime(587.33, ctx.currentTime);
      osc.frequency.setValueAtTime(880.00, ctx.currentTime + 0.15);

      gain.gain.setValueAtTime(0.2, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.8);

      osc.connect(gain);
      gain.connect(ctx.destination);

      osc.start();
      osc.stop(ctx.currentTime + 0.8);
    } catch {
      // Audio playback fallback
    }
  }

  selectSlip(tokenId: string): void {
    this.selectedTokenId.set(tokenId);
  }

  openCancelModal(): void {
    this.cancelReason.set('');
    this.cancelError.set('');
    this.isCancelModalOpen.set(true);
  }

  closeCancelModal(): void {
    this.isCancelModalOpen.set(false);
  }

  confirmCancellation(): void {
    const token = this.selectedToken();
    if (!token) return;

    this.cancelling.set(true);
    this.cancelError.set('');

    this.bookingService.cancelToken(token.id, this.cancelReason()).subscribe({
      next: () => {
        this.cancelling.set(false);
        this.isCancelModalOpen.set(false);
        this.selectedTokenId.set('');
        this.showToast(`Token ${token.tokenNumber} has been cancelled.`);
        this.loadTokens();
      },
      error: (err) => {
        this.cancelling.set(false);
        const validationErrors = err.error?.errors ? (Object.values(err.error.errors) as string[][]).flat().join(' ') : null;
        const msg = err.error?.message || validationErrors || err.error?.title || 'Could not cancel token.';
        this.cancelError.set(msg);
        this.toast.error(msg);
      }
    });
  }

  printSlip(): void {
    window.print();
  }

  showToast(msg: string): void {
    this.successToast.set(msg);
    setTimeout(() => this.successToast.set(''), 4000);
  }

  getAgingProgressPercent(score: number): number {
    return Math.min(Math.round(score), 100);
  }

  getStatusDisplay(status: string): string {
    switch (status) {
      case 'Booked': return 'Step 1: Visit Nurse Desk';
      case 'Waiting': return 'In Line for Doctor';
      case 'Called': return 'Doctor Calling — Enter Room';
      case 'Skipped': return 'On Hold (Missed Call)';
      case 'Completed': return 'Visit Completed';
      case 'Cancelled': return 'Cancelled';
      default: return status;
    }
  }

  getStatusBadgeClass(status: string): string {
    switch (status) {
      case 'Booked': return 'bg-amber-50 text-amber-800 border border-amber-200';
      case 'Waiting': return 'bg-teal-50 text-teal-800 border border-teal-200';
      case 'Called': return 'bg-emerald-50 text-emerald-800 border border-emerald-300 animate-pulse font-extrabold';
      case 'Completed': return 'bg-emerald-50 text-emerald-700 border border-emerald-200';
      case 'Cancelled': return 'bg-rose-50 text-rose-700 border border-rose-200';
      case 'Skipped': return 'bg-amber-50 text-amber-800 border border-amber-200';
      default: return 'bg-slate-100 text-slate-700 border border-slate-200';
    }
  }

  getTriageBadgeClass(level?: string): string {
    switch (level) {
      case 'Red': return 'bg-rose-50 text-rose-700 border border-rose-200';
      case 'Yellow': return 'bg-amber-50 text-amber-700 border border-amber-200';
      case 'Green': return 'bg-emerald-50 text-emerald-700 border border-emerald-200';
      default: return 'bg-teal-50 text-teal-700 border border-teal-200';
    }
  }
}
