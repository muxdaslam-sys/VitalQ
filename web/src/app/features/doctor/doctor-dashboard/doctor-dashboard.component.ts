import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription, timer } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { DoctorService } from '../../../core/services/doctor.service';
import { SignalRService } from '../../../core/services/signalr.service';
import { NurseService } from '../../../core/services/nurse.service';
import { Doctor, QueueToken, DoctorDashboardStats } from '../../../shared/models/queue.model';
import { TriageAssessment } from '../../../shared/models/triage.model';
import { PatientVisitHistory } from '../../../shared/models/patient.model';

/**
 * ============================================================================
 * COMPONENT: Doctor Consultation Room Console (/doctor)
 * ============================================================================
 * 
 * High-performance clinical cockpit for attending physicians:
 * - Real-time priority queue management with CTAS urgency and dynamic aging
 * - Live in-room consultation stopwatch tracking patient encounter length
 * - Melodic hospital audio summon chime via Web Audio API
 * - 3-Strike skip safety enforcement and CTAS 3-slot fallback re-queuing
 * - Inline pre-call vitals preview accordion and historical medical records dossier
 * - Real-time physician duty status toggle (Available vs. On Break)
 * ============================================================================
 */
@Component({
  selector: 'app-doctor-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './doctor-dashboard.component.html',
  styleUrl: './doctor-dashboard.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DoctorDashboardComponent implements OnInit, OnDestroy {

  // #region 1. Dependency Injection & Service Wiring
  auth = inject(AuthService);
  private router = inject(Router);
  private doctorService = inject(DoctorService);
  private nurseService = inject(NurseService);
  private signalR = inject(SignalRService);
  // #endregion

  // #region 2. Reactive State Signals

  /** Physician profile linked to current active session */
  doctorProfile = signal<Doctor | null>(null);

  /** Shift-wide performance metrics (assigned, completed, waiting, skipped, avg minutes) */
  dashboardStats = signal<DoctorDashboardStats | null>(null);

  /** Patient currently summoned into the consultation chamber */
  currentCalled = signal<QueueToken | null>(null);

  /** Nurse triage vitals recorded for the currently summoned patient */
  currentVitals = signal<TriageAssessment | null>(null);

  /** Prioritized list of waiting patients awaiting physician call */
  waitingQueue = signal<QueueToken[]>([]);

  /** Temporarily held patients who missed their initial call */
  skippedQueue = signal<QueueToken[]>([]);

  /** Consultations successfully completed and archived today */
  completedQueue = signal<QueueToken[]>([]);

  /** Active navigation tab in the queue desk: 'waiting' | 'skipped' | 'completed' */
  activeTab = signal<'waiting' | 'skipped' | 'completed'>('waiting');

  /** Screen-level loading indicator during initial data synchronization */
  isLoading = signal<boolean>(true);

  /** Button-level loading state during queue transitions */
  isActionLoading = signal<boolean>(false);

  /** Ephemeral notification banners */
  errorMessage = signal<string>('');
  successMessage = signal<string>('');

  /** Live consultation stopwatch timer text formatted as MM:SS */
  consultationTimerText = signal<string>('00:00');

  /** Elapsed seconds of the active consultation encounter */
  elapsedConsultationSeconds = signal<number>(0);

  /**
   * Ambient Consultation Pacing Metric (Option C):
   * Compares elapsed consultation seconds against physician's target duration.
   * - Normal (0% to 100%): Dark teal glass badge
   * - Caution (100% to 150%): Warm amber badge with past target duration
   * - Overtime (> 150%): Rose pulse badge indicating significant queue pacing delay
   */
  consultationPacing = computed(() => {
    const elapsedSecs = this.elapsedConsultationSeconds();
    const targetMins = this.doctorProfile()?.avgConsultationMinutes || 15;
    const targetSecs = targetMins * 60;

    if (elapsedSecs <= targetSecs) {
      return {
        status: 'normal' as const,
        badgeClass: 'bg-black/25 text-teal-100 border-teal-500/30',
        tag: ''
      };
    }

    const overSecs = elapsedSecs - targetSecs;
    const overMins = Math.floor(overSecs / 60) + 1;

    if (elapsedSecs <= targetSecs * 1.5) {
      return {
        status: 'caution' as const,
        badgeClass: 'bg-amber-400 text-slate-900 border-amber-300 font-extrabold shadow-sm',
        tag: `+${overMins}m past target`
      };
    }

    return {
      status: 'overtime' as const,
      badgeClass: 'bg-rose-500 text-white border-rose-400 font-extrabold shadow-sm animate-pulse',
      tag: `Overtime (+${overMins}m)`
    };
  });

  /** Pre-call vitals inspection accordion state */
  expandedVitalsTokenId = signal<string | null>(null);
  expandedVitalsData = signal<TriageAssessment | null>(null);
  isLoadingExpandedVitals = signal<boolean>(false);

  /** Historical patient medical dossier drawer state */
  isHistoryDrawerOpen = signal<boolean>(false);
  activeHistoryPatientName = signal<string>('');
  patientHistoryList = signal<PatientVisitHistory[]>([]);
  isLoadingHistory = signal<boolean>(false);

  /** Complete consultation modal state */
  isCompleteModalOpen = signal<boolean>(false);
  consultationNotes = signal<string>('');
  isSubmittingComplete = signal<boolean>(false);

  /** Shift availability toggle dropdown visibility */
  isStatusMenuOpen = signal<boolean>(false);

  // #endregion

  // #region 3. Subscriptions & Timers
  private subs: Subscription[] = [];
  private stopwatchIntervalId: any = null;
  private autoDismissTimerId: any = null;
  // #endregion

  // #region 4. Lifecycle Hooks

  ngOnInit(): void {
    this.loadDoctorProfileAndData();
  }

  ngOnDestroy(): void {
    this.stopConsultationTimer();
    if (this.autoDismissTimerId) {
      clearTimeout(this.autoDismissTimerId);
    }
    if (this.doctorProfile()) {
      this.signalR.leaveDoctorGroup(this.doctorProfile()!.id);
    }
    this.subs.forEach(s => s.unsubscribe());
  }

  // #endregion

  // #region 5. Data Loading & Shift Synchronization

  /**
   * Initializes doctor session, retrieves profile, and joins room broadcast socket.
   */
  loadDoctorProfileAndData(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');

    this.doctorService.getDoctorProfile().subscribe({
      next: (doc) => {
        this.doctorProfile.set(doc);
        this.fetchQueueAndStats(doc.id, true);
        this.connectSignalR(doc.id);
        this.initHeartbeatPoller(doc.id);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(
          err.error?.message || 'Could not load physician profile. Ensure your account is assigned to an active doctor record.'
        );
      }
    });
  }

  /**
   * Fail-Safe Silent Background Heartbeat Poller (Option B):
   * Runs every 30 seconds as an automatic fallback to SignalR WebSockets.
   * Ensures doctor room queue never freezes even during hospital Wi-Fi roaming or TCP half-opens.
   */
  private initHeartbeatPoller(doctorId: string): void {
    const heartbeatSub = timer(30000, 30000).subscribe(() => {
      this.fetchQueueAndStats(doctorId, false);
    });
    this.subs.push(heartbeatSub);
  }

  /**
   * Fetches fresh queue snapshot, completed archive, and shift metrics from backend.
   * Supports silent background polling without toggling screen-level loading spinners.
   */
  fetchQueueAndStats(doctorId: string, showSpinner = false): void {
    if (showSpinner) {
      this.isLoading.set(true);
    }

    // 1. Fetch live queue tokens
    this.doctorService.getDoctorQueue(doctorId).subscribe({
      next: (tokens) => {
        const called = tokens.find(t => t.status === 'Called') || null;
        this.currentCalled.set(called);

        const waiting = tokens.filter(t => t.status === 'Waiting');
        this.waitingQueue.set(waiting);

        const skipped = tokens.filter(t => t.status === 'Skipped');
        this.skippedQueue.set(skipped);

        this.isLoading.set(false);

        // Sync stopwatch and clinical vitals
        if (called) {
          this.loadCurrentPatientVitals(called.id);
          this.startConsultationTimer(called.calledAtUtc);
        } else {
          this.currentVitals.set(null);
          this.stopConsultationTimer();
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        if (showSpinner) {
          this.errorMessage.set(err.error?.message || 'Failed to refresh room queue.');
        }
      }
    });

    // 2. Fetch shift dashboard metrics
    this.doctorService.getDashboardStats(doctorId).subscribe({
      next: (stats) => this.dashboardStats.set(stats),
      error: () => { /* Metrics fallback to default */ }
    });

    // 3. Fetch completed consultations history today
    this.doctorService.getCompletedHistoryToday(doctorId).subscribe({
      next: (history) => this.completedQueue.set(history),
      error: () => { /* Non-blocking history refresh */ }
    });
  }

  /**
   * Retrieves vitals recorded by triage nurse for the active in-room patient.
   */
  loadCurrentPatientVitals(tokenId: string): void {
    this.nurseService.getTriageAssessment(tokenId).subscribe({
      next: (vitals) => this.currentVitals.set(vitals),
      error: () => this.currentVitals.set(null)
    });
  }

  // #endregion

  // #region 6. Real-Time SignalR Event Mesh

  /**
   * Connects to SignalR Hub and subscribes to physician room broadcast channel.
   */
  private connectSignalR(doctorId: string): void {
    this.signalR.startConnection().then(() => {
      this.signalR.joinDoctorGroup(doctorId);

      const sub1 = this.signalR.onQueueUpdated$.subscribe(() => {
        this.fetchQueueAndStats(doctorId);
      });

      const sub2 = this.signalR.onTokenBooked$.subscribe(() => {
        this.fetchQueueAndStats(doctorId);
      });

      const sub3 = this.signalR.onScoresRecalculated$.subscribe(() => {
        this.fetchQueueAndStats(doctorId);
      });

      const sub4 = this.signalR.onDoctorStatusChanged$.subscribe((data) => {
        if (data.doctorId === doctorId) {
          const profile = this.doctorProfile();
          if (profile) {
            this.doctorProfile.set({ ...profile, status: data.status });
          }
        }
      });

      this.subs.push(sub1, sub2, sub3, sub4);
    });
  }

  // #endregion

  // #region 7. Live Consultation Stopwatch

  /**
   * Starts a 1-second interval timer computing elapsed consultation minutes and seconds.
   */
  private startConsultationTimer(calledAtUtc?: string): void {
    this.stopConsultationTimer();

    const startTime = calledAtUtc ? new Date(calledAtUtc).getTime() : Date.now();

    const updateTimer = () => {
      const elapsedSeconds = Math.max(0, Math.floor((Date.now() - startTime) / 1000));
      this.elapsedConsultationSeconds.set(elapsedSeconds);
      const mins = Math.floor(elapsedSeconds / 60);
      const secs = elapsedSeconds % 60;
      this.consultationTimerText.set(
        `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`
      );
    };

    updateTimer();
    this.stopwatchIntervalId = setInterval(updateTimer, 1000);
  }

  private stopConsultationTimer(): void {
    if (this.stopwatchIntervalId) {
      clearInterval(this.stopwatchIntervalId);
      this.stopwatchIntervalId = null;
    }
    this.elapsedConsultationSeconds.set(0);
    this.consultationTimerText.set('00:00');
  }

  // #endregion

  // #region 8. Queue Summoning & Call Actions

  /**
   * Summons the next highest-ranking waiting patient into the consultation room.
   */
  callNext(): void {
    const profile = this.doctorProfile();
    if (!profile || this.isActionLoading()) return;

    this.isActionLoading.set(true);
    this.errorMessage.set('');

    this.doctorService.callNext(profile.id).subscribe({
      next: (token) => {
        this.isActionLoading.set(false);
        this.currentCalled.set(token);
        this.playMelodicChime();
        this.showSuccessToast(`Patient ${token.tokenNumber} (${token.patientName}) called into ${profile.roomNumber}!`);
        this.fetchQueueAndStats(profile.id);
      },
      error: (err) => {
        this.isActionLoading.set(false);
        this.errorMessage.set(err.error?.message || 'Could not summon next patient.');
      }
    });
  }

  /**
   * Re-transmits auditory chime and visual broadcast for the currently summoned patient.
   */
  resummonCurrent(): void {
    const current = this.currentCalled();
    if (!current || this.isActionLoading()) return;

    this.isActionLoading.set(true);
    this.doctorService.summonPatient(current.id).subscribe({
      next: () => {
        this.isActionLoading.set(false);
        this.playMelodicChime();
        this.showSuccessToast(`Re-broadcasted chime & screen alert for ${current.tokenNumber} (${current.patientName}).`);
      },
      error: (err) => {
        this.isActionLoading.set(false);
        this.errorMessage.set(err.error?.message || 'Failed to re-summon patient.');
      }
    });
  }

  // #endregion

  // #region 9. Skipping Logic & 3-Strike Safety Guard

  /**
   * Puts absent patient on hold enforcing the 3-strike policy.
   * Strike 1 & 2: Moved to Skipped tab on temporary hold.
   * Strike 3: Auto-transitioned to Expired / No-Show with slot released.
   */
  skipCurrentPatient(): void {
    const current = this.currentCalled();
    const profile = this.doctorProfile();
    if (!current || !profile || this.isActionLoading()) return;

    const currentStrikes = current.skipCount ?? 0;
    const nextStrike = currentStrikes + 1;

    let confirmMsg = `Patient ${current.tokenNumber} did not respond to summon.\n\n`;
    if (nextStrike >= 3) {
      confirmMsg += `WARNING: This is Call 3 of 3 (Final Strike).\nMarking absent now will immediately EXPIRE this appointment as a No-Show. Proceed?`;
    } else {
      confirmMsg += `Marking as absent (Call ${nextStrike} of 3).\nPatient will be placed on temporary hold. Proceed?`;
    }

    if (!confirm(confirmMsg)) return;

    this.isActionLoading.set(true);
    this.doctorService.skipPatient(current.id).subscribe({
      next: () => {
        this.isActionLoading.set(false);
        this.currentCalled.set(null);
        this.currentVitals.set(null);
        this.stopConsultationTimer();

        if (nextStrike >= 3) {
          this.showSuccessToast(`Token ${current.tokenNumber} expired as No-Show after 3 missed calls.`);
        } else {
          this.showSuccessToast(`Token ${current.tokenNumber} placed on temporary hold (Strike ${nextStrike} of 3).`);
        }

        this.fetchQueueAndStats(profile.id);
      },
      error: (err) => {
        this.isActionLoading.set(false);
        this.errorMessage.set(err.error?.message || 'Could not skip patient.');
      }
    });
  }

  // #endregion

  // #region 10. CTAS-Aware Re-Queue Action

  /**
   * Restores an absent patient back into the active queue when they present at the clinic.
   * Applies CTAS-aware repositioning:
   * - Red Emergency: Guaranteed Position #1
   * - Routine: Dynamic 3-slot fallback behind current waiting patients
   */
  requeuePatient(token: QueueToken): void {
    const profile = this.doctorProfile();
    if (!profile || this.isActionLoading()) return;

    this.isActionLoading.set(true);
    this.doctorService.requeuePatient(token.id).subscribe({
      next: () => {
        this.isActionLoading.set(false);
        const isRed = token.triageLevel === 'Red';
        const msg = isRed
          ? `Emergency Token ${token.tokenNumber} prioritized immediately to Position #1!`
          : `Token ${token.tokenNumber} re-activated into Waiting Queue (3 slots back).`;

        this.showSuccessToast(msg);
        this.fetchQueueAndStats(profile.id);
      },
      error: (err) => {
        this.isActionLoading.set(false);
        this.errorMessage.set(err.error?.message || 'Could not re-queue patient.');
      }
    });
  }

  // #endregion

  // #region 11. Consultation Completion & Clinical Notes Modal

  openCompleteModal(): void {
    this.consultationNotes.set('');
    this.isCompleteModalOpen.set(true);
  }

  closeCompleteModal(): void {
    this.isCompleteModalOpen.set(false);
  }

  submitCompleteConsultation(): void {
    const current = this.currentCalled();
    const profile = this.doctorProfile();
    if (!current || !profile || this.isSubmittingComplete()) return;

    this.isSubmittingComplete.set(true);
    this.doctorService.completeConsultation(current.id, this.consultationNotes()).subscribe({
      next: () => {
        this.isSubmittingComplete.set(false);
        this.isCompleteModalOpen.set(false);
        this.currentCalled.set(null);
        this.currentVitals.set(null);
        this.stopConsultationTimer();
        this.showSuccessToast(`Consultation finalized for ${current.tokenNumber} (${current.patientName}).`);
        this.fetchQueueAndStats(profile.id);
      },
      error: (err) => {
        this.isSubmittingComplete.set(false);
        this.errorMessage.set(err.error?.message || 'Failed to complete consultation.');
      }
    });
  }

  // #endregion

  // #region 12. Pre-Call Waiting Vitals Preview Accordion

  /**
   * Toggles the inline vitals inspection card for any patient in the waiting queue.
   */
  toggleVitalsPreview(token: QueueToken): void {
    if (this.expandedVitalsTokenId() === token.id) {
      this.expandedVitalsTokenId.set(null);
      this.expandedVitalsData.set(null);
      return;
    }

    this.expandedVitalsTokenId.set(token.id);
    this.isLoadingExpandedVitals.set(true);
    this.expandedVitalsData.set(null);

    this.nurseService.getTriageAssessment(token.id).subscribe({
      next: (vitals) => {
        this.expandedVitalsData.set(vitals);
        this.isLoadingExpandedVitals.set(false);
      },
      error: () => {
        this.expandedVitalsData.set(null);
        this.isLoadingExpandedVitals.set(false);
      }
    });
  }

  // #endregion

  // #region 13. Patient Longitudinal Medical History Dossier

  /**
   * Opens the slide-over drawer displaying past consultations and diagnostic history.
   */
  openPatientHistory(patientId: string, patientName: string): void {
    this.activeHistoryPatientName.set(patientName);
    this.isHistoryDrawerOpen.set(true);
    this.isLoadingHistory.set(true);
    this.patientHistoryList.set([]);

    this.doctorService.getPatientPastEncounters(patientId).subscribe({
      next: (history) => {
        this.patientHistoryList.set(history);
        this.isLoadingHistory.set(false);
      },
      error: () => {
        this.patientHistoryList.set([]);
        this.isLoadingHistory.set(false);
      }
    });
  }

  closePatientHistory(): void {
    this.isHistoryDrawerOpen.set(false);
  }

  // #endregion

  // #region 14. Physician Shift Availability Console Toggle

  toggleStatusMenu(): void {
    this.isStatusMenuOpen.update(v => !v);
  }

  /**
   * Updates physician duty status between 'Available' (In Clinic) and 'Busy' (On Break).
   * Note: 'OnLeave' is configured exclusively by Hospital Administration.
   */
  setDoctorStatus(status: 'Available' | 'Busy'): void {
    const profile = this.doctorProfile();
    if (!profile) return;

    this.isStatusMenuOpen.set(false);
    this.doctorService.updateDoctorStatus(status, profile.id).subscribe({
      next: (updatedDoc) => {
        this.doctorProfile.set(updatedDoc);
        const currentStats = this.dashboardStats();
        if (currentStats) {
          this.dashboardStats.set({ ...currentStats, status: updatedDoc.status });
        }
        const label = status === 'Available' ? 'Available in Clinic' : 'On Break (Paused)';
        this.showSuccessToast(`Duty status updated to ${label}.`);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Could not update duty status.');
      }
    });
  }

  // #endregion

  // #region 15. Native Web Audio Chime Synthesizer

  /**
   * Generates a modern dual-tone hospital chime using the browser's native Web Audio API.
   * Rings a pure D5 (587.33 Hz) followed smoothly by A5 (880.00 Hz) with an exponential decay.
   * Zero external MP3/WAV assets required.
   */
  private playMelodicChime(): void {
    try {
      const AudioCtx = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      const ctx = new AudioCtx();
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();

      osc.type = 'sine';
      osc.frequency.setValueAtTime(587.33, ctx.currentTime);
      osc.frequency.setValueAtTime(880.00, ctx.currentTime + 0.18);

      gain.gain.setValueAtTime(0.25, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.85);

      osc.connect(gain);
      gain.connect(ctx.destination);

      osc.start();
      osc.stop(ctx.currentTime + 0.85);
    } catch {
      // Audio autoplay gracefully handled if browser policy restricts audio context
    }
  }

  // #endregion

  // #region 16. Presentation & Badge Formatting Utilities

  getTriageBadgeClass(level?: string): string {
    switch (level) {
      case 'Red':
        return 'bg-rose-50 text-rose-700 border-rose-200';
      case 'Yellow':
        return 'bg-amber-50 text-amber-800 border-amber-200';
      case 'Green':
        return 'bg-emerald-50 text-emerald-800 border-emerald-200';
      default:
        return 'bg-slate-50 text-slate-700 border-slate-200';
    }
  }

  getTriageIndicatorClass(level?: string): string {
    switch (level) {
      case 'Red':
        return 'bg-rose-500 shadow-rose-500/50';
      case 'Yellow':
        return 'bg-amber-500 shadow-amber-500/50';
      case 'Green':
        return 'bg-emerald-500 shadow-emerald-500/50';
      default:
        return 'bg-slate-400';
    }
  }

  getStrikeBadgeClass(count?: number): string {
    const c = count ?? 0;
    if (c >= 2) return 'bg-rose-100 text-rose-800 border-rose-300 font-extrabold';
    if (c === 1) return 'bg-amber-100 text-amber-800 border-amber-300 font-bold';
    return 'bg-slate-100 text-slate-700 border-slate-200 font-medium';
  }

  getWaitingMinutes(triagedAtUtc?: string, bookedAtUtc?: string): number {
    const timeStr = triagedAtUtc || bookedAtUtc;
    if (!timeStr) return 0;
    const diffMs = Date.now() - new Date(timeStr).getTime();
    return Math.max(0, Math.floor(diffMs / 60000));
  }

  getHoldMinutes(calledAtUtc?: string): number {
    if (!calledAtUtc) return 0;
    const diffMs = Date.now() - new Date(calledAtUtc).getTime();
    return Math.max(0, Math.floor(diffMs / 60000));
  }

  formatTime(isoUtc?: string): string {
    if (!isoUtc) return '--:--';
    return new Date(isoUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  formatDate(isoUtc?: string): string {
    if (!isoUtc) return '--';
    return new Date(isoUtc).toLocaleDateString([], { month: 'short', day: 'numeric', year: 'numeric' });
  }

  private showSuccessToast(message: string): void {
    this.successMessage.set(message);
    if (this.autoDismissTimerId) clearTimeout(this.autoDismissTimerId);
    this.autoDismissTimerId = setTimeout(() => this.successMessage.set(''), 6000);
  }

  dismissToast(): void {
    this.successMessage.set('');
    this.errorMessage.set('');
  }

  // #endregion

  // #region 17. Teardown & Session Management

  logout(): void {
    this.auth.logout().subscribe(() => this.router.navigate(['/login']));
  }

  // #endregion
}
