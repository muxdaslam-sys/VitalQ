/**
 * ============================================================================
 * COMPONENT: NurseDashboardComponent (Clinical Triage Command Desk)
 * ============================================================================
 * 
 * PURPOSE:
 * Serves as the high-throughput clinical workstation for triage nurses in hospital OPDs:
 * 1. [Triage Desk]: Fast, focused vital signs intake and CTAS scoring with
 *    live SLA wait indicators, 1-click restore for missed turns, and departed walk-in cancellation.
 * 2. [Doctor Rooms]: Live physician duty roster, active chamber lines with current patient inside,
 *    and room re-queuing.
 * 3. [Intake & Directory]: Unified intake for both walk-in and returning patients with
 *    automatic phone deduplication and Code-Red emergency bypass options.
 * 
 * PRODUCTION PERFORMANCE:
 * - Coordinated forkJoin data loading prevents UI flicker.
 * - SignalR broadcasts are debounced (300ms) to stop database stampedes.
 * - Single unified intake modal eliminates duplicate HTML and fragmented booking flows.
 * ============================================================================
 */

import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription, Subject, of, timer, forkJoin } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, catchError } from 'rxjs/operators';

import { AuthService } from '../../../core/services/auth.service';
import { NurseService } from '../../../core/services/nurse.service';
import { SignalRService } from '../../../core/services/signalr.service';
import { ToastService } from '../../../core/services/toast.service';

import { TriageModalComponent } from '../components/triage-modal/triage-modal.component';
import { DoctorQueueModalComponent } from '../components/doctor-queue-modal/doctor-queue-modal.component';
import { PatientChartModalComponent } from '../components/patient-chart-modal/patient-chart-modal.component';
import { EmergencyWalkinModalComponent } from '../components/emergency-walkin-modal/emergency-walkin-modal.component';

import { QueueToken } from '../../../shared/models/queue.model';
import { TriageRequest, PatientSearchResult } from '../../../shared/models/triage.model';
import {
  NurseDashboardStatsResponse,
  DoctorQueueSummaryDto,
  DoctorDetailedQueueResponse
} from '../../../shared/models/nurse.model';
import { PatientDetailResponse } from '../../../shared/models/admin.model';

@Component({
  selector: 'app-nurse-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TriageModalComponent,
    DoctorQueueModalComponent,
    PatientChartModalComponent,
    EmergencyWalkinModalComponent
  ],
  templateUrl: './nurse-dashboard.component.html',
  styleUrl: './nurse-dashboard.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NurseDashboardComponent implements OnInit, OnDestroy {
  // #region 1. Dependency Injection & Subscriptions
  auth = inject(AuthService);
  private nurseService = inject(NurseService);
  private signalR = inject(SignalRService);
  private toast = inject(ToastService);
  private router = inject(Router);

  private subs = new Subscription();
  private searchSubject = new Subject<string>();
  private refreshSubject = new Subject<void>();

  constructor() {
    // Debounced patient directory search for Tab 3
    const searchSub = this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(query => {
        const q = query.trim();
        if (q.length < 2) {
          this.isSearching.set(false);
          this.searchResults.set([]);
          return of([]);
        }
        this.isSearching.set(true);
        return this.nurseService.searchPatients(q).pipe(
          catchError(() => {
            this.toast.error('Patient search encountered an error.');
            return of([]);
          })
        );
      })
    ).subscribe(results => {
      this.searchResults.set(results);
      this.isSearching.set(false);
    });
    this.subs.add(searchSub);
  }
  // #endregion

  // #region 2. Dashboard State Signals
  stats = signal<NurseDashboardStatsResponse | null>(null);
  allDeskTokens = signal<QueueToken[]>([]);
  doctors = signal<DoctorQueueSummaryDto[]>([]);
  isRefreshing = signal<boolean>(false);

  /** Focused 3-Pillar Tab: 'triage' (Default) | 'doctors' | 'intake' */
  activeTab = signal<'triage' | 'doctors' | 'intake'>('triage');

  /** Triage Desk Segment: 'awaiting' (Default) | 'skipped' */
  triageSegment = signal<'awaiting' | 'skipped'>('awaiting');

  /** In-table search filter query for Triage Desk */
  tableFilterQuery = signal<string>('');

  /** Computed count of walk-ins awaiting triage */
  pendingTriageCount = computed(() =>
    this.allDeskTokens().filter(t => t.status === 'Booked').length
  );

  /** Computed count of patients currently skipped / on hold */
  skippedTokensCount = computed(() =>
    this.allDeskTokens().filter(t => t.status === 'Skipped').length
  );

  /** Active Code-Red emergency tokens requiring immediate clinical intervention */
  activeEmergencyTokens = computed(() =>
    this.allDeskTokens().filter(t =>
      (t.baseWeight >= 1000 || t.triageLevel === 'Red') &&
      (t.status === 'Booked' || t.status === 'Waiting' || t.status === 'Called')
    )
  );

  /** Computed list of triage tokens filtered in real time by segment and search */
  filteredQueue = computed(() => {
    const segment = this.triageSegment();
    const q = this.tableFilterQuery().trim().toLowerCase();
    
    // 1. Filter by segment status
    let list = this.allDeskTokens().filter(t =>
      segment === 'awaiting' ? t.status === 'Booked' : t.status === 'Skipped'
    );

    // 2. Filter by search text query
    if (!q) return list;
    return list.filter(t =>
      t.tokenNumber.toLowerCase().includes(q) ||
      t.patientName.toLowerCase().includes(q) ||
      t.patientPhone.includes(q) ||
      t.doctorName.toLowerCase().includes(q)
    );
  });
  // #endregion

  // #region 3. Directory Search State (Tab 3)
  searchQuery = signal<string>('');
  searchResults = signal<PatientSearchResult[]>([]);
  isSearching = signal<boolean>(false);
  // #endregion

  // #region 4. Decoupled Modal State
  activeTriageToken = signal<QueueToken | null>(null);
  activeDoctorQueue = signal<DoctorDetailedQueueResponse | null>(null);
  activePatientChart = signal<PatientDetailResponse | null>(null);
  showWalkInModal = signal<boolean>(false);
  selectedPatientForIntake = signal<PatientSearchResult | null>(null);
  // #endregion

  // #region 5. Lifecycle & SignalR Real-Time Sync
  ngOnInit(): void {
    this.loadAllData();
    this.initSignalR();
    this.initHeartbeatPoller();
  }

  ngOnDestroy(): void {
    this.signalR.leaveNurseStationGroup();
    this.subs.unsubscribe();
  }

  /**
   * Coordinated forkJoin data loading:
   * Prevents UI flicker and manages refresh spinner strictly until all requests complete.
   */
  loadAllData(showLoader = true): void {
    if (showLoader) this.isRefreshing.set(true);

    forkJoin({
      stats: this.nurseService.getDashboardStats().pipe(
        catchError(() => of(null))
      ),
      tokens: this.nurseService.getPendingTriage().pipe(
        catchError((err) => {
          this.toast.error(err.error?.message || 'Failed to load active station queue.');
          return of([]);
        })
      )
    }).subscribe({
      next: ({ stats, tokens }) => {
        if (stats) {
          this.stats.set(stats);
          this.doctors.set(stats.doctors);
        }
        this.allDeskTokens.set(tokens);
        if (showLoader) this.isRefreshing.set(false);
      },
      error: () => {
        if (showLoader) this.isRefreshing.set(false);
      }
    });
  }

  /**
   * High-Performance Real-Time SignalR Sync:
   * Debounces incoming events (300ms) to coalesce rapid-fire updates into a single smooth reload.
   */
  private initSignalR(): void {
    this.signalR.startConnection().then(async () => {
      await this.signalR.joinNurseStationGroup();
    }).catch(err => {
      console.warn('SignalR connection failed, falling back to on-demand polling:', err);
    });

    this.subs.add(
      this.refreshSubject.pipe(debounceTime(300)).subscribe(() => {
        this.loadAllData(false);
      })
    );

    this.subs.add(this.signalR.onTokenBooked$.subscribe(() => this.refreshSubject.next()));
    this.subs.add(this.signalR.onQueueUpdated$.subscribe(() => this.refreshSubject.next()));
    this.subs.add(this.signalR.onTriageCompleted$.subscribe(() => this.refreshSubject.next()));
    this.subs.add(this.signalR.onDoctorStatusChanged$.subscribe(() => this.refreshSubject.next()));
    this.subs.add(this.signalR.onPatientCalled$.subscribe(() => this.refreshSubject.next()));
  }

  /**
   * Fail-Safe Silent Background Heartbeat Poller:
   * Polls every 45s as a parachute fallback to SignalR.
   */
  private initHeartbeatPoller(): void {
    const heartbeatSub = timer(45000, 45000).subscribe(() => {
      this.loadAllData(false);
    });
    this.subs.add(heartbeatSub);
  }
  // #endregion

  // #region 6. User Actions & Modal Triggers

  openTriage(token: QueueToken): void {
    this.activeTriageToken.set(token);
  }

  onTriageSaved(): void {
    this.activeTriageToken.set(null);
    this.loadAllData(false);
  }

  cancelToken(token: QueueToken): void {
    if (!confirm(`Cancel walk-in token ${token.tokenNumber} for ${token.patientName}?`)) {
      return;
    }

    this.nurseService.cancelToken(token.id).subscribe({
      next: () => {
        this.toast.success(`Token ${token.tokenNumber} has been cancelled.`);
        this.loadAllData(false);
      },
      error: (err) => {
        this.toast.error(err.error?.message || 'Failed to cancel token.');
      }
    });
  }

  requeueFromDesk(token: QueueToken): void {
    this.nurseService.requeuePatient(token.id).subscribe({
      next: () => {
        this.toast.success(`Patient ${token.patientName} (${token.tokenNumber}) restored to active line for Dr. ${token.doctorName}.`);
        this.loadAllData(false);
      },
      error: (err) => {
        this.toast.error(err.error?.message || 'Failed to restore patient.');
      }
    });
  }

  openDoctorQueue(doctorId: string): void {
    this.nurseService.getDoctorQueue(doctorId).subscribe({
      next: data => this.activeDoctorQueue.set(data),
      error: () => this.toast.error('Failed to load doctor queue.')
    });
  }

  /**
   * Refreshes the doctor's active chamber queue in place after a patient is restored.
   */
  onDoctorQueuePatientRequeued(doctorId: string): void {
    this.loadAllData(false);
    this.nurseService.getDoctorQueue(doctorId).subscribe({
      next: data => this.activeDoctorQueue.set(data),
      error: () => this.activeDoctorQueue.set(null)
    });
  }

  openPatientChart(patientId: string): void {
    this.nurseService.getPatientDetails(patientId).subscribe({
      next: (chart: PatientDetailResponse) => this.activePatientChart.set(chart),
      error: () => this.toast.error('Failed to load patient medical chart.')
    });
  }

  openIntakeModal(patient?: PatientSearchResult): void {
    this.selectedPatientForIntake.set(patient || null);
    this.showWalkInModal.set(true);
  }

  onSearchInput(val: string): void {
    this.searchQuery.set(val);
    this.searchSubject.next(val);
  }

  getWaitTimeMinutes(bookedAtUtc: string): number {
    if (!bookedAtUtc) return 0;
    const booked = new Date(bookedAtUtc).getTime();
    const now = Date.now();
    return Math.max(0, Math.floor((now - booked) / 60000));
  }

  logout(): void {
    this.auth.logout().subscribe(() => {
      this.router.navigate(['/login']);
    });
  }

  // #endregion
}
