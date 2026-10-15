import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subscription, timer } from 'rxjs';
import { BookingService } from '../../../core/services/booking.service';
import { SignalRService } from '../../../core/services/signalr.service';
import { ToastService } from '../../../core/services/toast.service';
import { AuthService } from '../../../core/services/auth.service';
import { QueueToken } from '../../../shared/models/queue.model';
import { TeleportToBodyDirective } from '../../../shared/directives/teleport.directive';

@Component({
  selector: 'app-patient-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, TeleportToBodyDirective],
  templateUrl: './patient-dashboard.component.html',
  styleUrls: ['./patient-dashboard.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
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

  private subs: Subscription[] = [];
  private previousCalledIds = new Set<string>();
  private joinedTokenIds = new Set<string>();

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
    this.joinedTokenIds.forEach(id => this.signalR.leaveTokenGroup(id));
    this.joinedTokenIds.clear();
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
        this.syncTokenGroups(tokens);
      },
      error: () => this.loading.set(false)
    });
  }

  refreshTokensSilently(): void {
    this.bookingService.getMyTokens().subscribe({
      next: (data) => {
        this.tokens.set(data);
        this.checkCalledTokens(data);
        this.syncTokenGroups(data);
      }
    });
  }

  initSignalR(): void {
    this.signalR.startConnection().then(() => {
      this.syncTokenGroups(this.tokens());

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

  private syncTokenGroups(tokenList: QueueToken[]): void {
    if (!this.signalR.isConnected) return;
    const currentIds = new Set(tokenList.map(t => t.id));

    // Join any newly loaded active tokens
    tokenList.forEach(t => {
      if (!this.joinedTokenIds.has(t.id)) {
        this.signalR.joinTokenGroup(t.id);
        this.joinedTokenIds.add(t.id);
      }
    });

    // Leave any removed or completed tokens
    this.joinedTokenIds.forEach(id => {
      if (!currentIds.has(id)) {
        this.signalR.leaveTokenGroup(id);
        this.joinedTokenIds.delete(id);
      }
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
    this.toast.success(msg);
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
      case 'Expired': return 'Expired';
      default: return status;
    }
  }

  getStatusBadgeClass(status: string): string {
    switch (status) {
      case 'Booked': return 'bg-amber-100 text-amber-900 border border-amber-300 font-extrabold';
      case 'Waiting': return 'bg-teal-100 text-teal-900 border border-teal-300 font-extrabold';
      case 'Called': return 'bg-emerald-600 text-white border border-emerald-500 animate-pulse font-extrabold shadow-xs';
      case 'Completed': return 'bg-emerald-100 text-emerald-800 border border-emerald-200 font-bold';
      case 'Cancelled': return 'bg-rose-100 text-rose-800 border border-rose-200 font-bold';
      case 'Expired': return 'bg-slate-100 text-slate-700 border border-slate-300 font-bold';
      case 'Skipped': return 'bg-amber-100 text-amber-900 border border-amber-300 font-bold';
      default: return 'bg-slate-100 text-slate-700 border border-slate-200 font-bold';
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
