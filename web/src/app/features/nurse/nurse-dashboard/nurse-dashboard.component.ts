/**
 * ============================================================================
 * COMPONENT: NurseDashboardComponent (Clinical Triage & Station Workstation)
 * ============================================================================
 * 
 * PURPOSE:
 * Serves as the central command console for triage nurses in hospital OPDs.
 * Architected with a clean 3-pillar workflow:
 * 1. [📋 Triage Desk]: Fast, focused vital signs intake and CTAS scoring (80% of shift).
 * 2. [🩺 Doctor Rooms]: Live physician duty roster, active chamber lines, and re-queuing.
 * 3. [🏥 Intake & Directory]: Emergency walk-in registrations and patient directory search.
 * 
 * Modals are decoupled into dedicated child components to eliminate monolithic code.
 * ============================================================================
 */

import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription, Subject, of, timer } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, catchError } from 'rxjs/operators';

import { AuthService } from '../../../core/services/auth.service';
import { NurseService } from '../../../core/services/nurse.service';
import { SignalRService } from '../../../core/services/signalr.service';
import { ToastService } from '../../../core/services/toast.service';

import { TriageModalComponent } from '../components/triage-modal/triage-modal.component';
import { DoctorQueueModalComponent } from '../components/doctor-queue-modal/doctor-queue-modal.component';
import { PatientChartModalComponent } from '../components/patient-chart-modal/patient-chart-modal.component';
import { QueueStatusModalComponent } from '../components/queue-status-modal/queue-status-modal.component';
import { EmergencyWalkinModalComponent } from '../components/emergency-walkin-modal/emergency-walkin-modal.component';

import { QueueToken } from '../../../shared/models/queue.model';
import { TriageRequest, PatientSearchResult } from '../../../shared/models/triage.model';
import {
  NurseDashboardStatsResponse,
  DoctorQueueSummaryDto,
  DoctorDetailedQueueResponse,
  PatientQueuePositionResponse,
  ExistingPatientTokenBookingRequest
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
    QueueStatusModalComponent,
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
  pendingTriage = signal<QueueToken[]>([]);
  doctors = signal<DoctorQueueSummaryDto[]>([]);
  isRefreshing = signal<boolean>(false);

  /** Focused 3-Pillar Tab: 'triage' (Default) | 'doctors' | 'intake' */
  activeTab = signal<'triage' | 'doctors' | 'intake'>('triage');

  /** In-table search filter query for Triage Desk */
  tableFilterQuery = signal<string>('');

  /** Computed list of triage tokens filtered in real time */
  filteredQueue = computed(() => {
    const q = this.tableFilterQuery().trim().toLowerCase();
    const list = this.pendingTriage();
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

  showBookExistingModal = signal<boolean>(false);
  bookExistingPatient = signal<PatientSearchResult | null>(null);
  isSubmittingBooking = signal<boolean>(false);
  bookExistingForm: ExistingPatientTokenBookingRequest = {
    doctorId: '',
    isEmergencyCodeRed: false,
    clinicalNotes: ''
  };
  // #endregion

  // #region 4. Decoupled Modal State
  activeTriageToken = signal<QueueToken | null>(null);
  activeDoctorQueue = signal<DoctorDetailedQueueResponse | null>(null);
  activePatientChart = signal<PatientDetailResponse | null>(null);
  activeQueuePosition = signal<PatientQueuePositionResponse | null>(null);
  showWalkInModal = signal<boolean>(false);
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

  loadAllData(showLoader = true): void {
    if (showLoader) this.isRefreshing.set(true);

    this.nurseService.getDashboardStats().subscribe({
      next: data => {
        this.stats.set(data);
        this.doctors.set(data.doctors);
        if (showLoader) this.isRefreshing.set(false);
      },
      error: () => {
        if (showLoader) this.isRefreshing.set(false);
      }
    });

    this.nurseService.getPendingTriage().subscribe({
      next: (tokens: QueueToken[]) => this.pendingTriage.set(tokens),
      error: () => {}
    });
  }

  private initSignalR(): void {
    this.signalR.startConnection().then(async () => {
      await this.signalR.joinNurseStationGroup();
    }).catch(err => {
      console.warn('SignalR connection failed, falling back to on-demand polling:', err);
    });

    this.subs.add(this.signalR.onTokenBooked$.subscribe(() => this.loadAllData(false)));
    this.subs.add(this.signalR.onQueueUpdated$.subscribe(() => this.loadAllData(false)));
    this.subs.add(this.signalR.onTriageCompleted$.subscribe(() => this.loadAllData(false)));
    this.subs.add(this.signalR.onDoctorStatusChanged$.subscribe(() => this.loadAllData(false)));
    this.subs.add(this.signalR.onPatientCalled$.subscribe(() => this.loadAllData(false)));
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

  submitTriage(request: TriageRequest): void {
    const token = this.activeTriageToken();
    if (!token) return;

    this.nurseService.submitTriage(token.id, request).subscribe({
      next: () => {
        this.toast.success(`Vitals recorded for ${token.tokenNumber}. Routed to Dr. ${token.doctorName}.`);
        this.activeTriageToken.set(null);
        this.loadAllData(false);
      },
      error: (err) => {
        this.toast.error(err.error?.message || 'Failed to submit triage vitals.');
      }
    });
  }

  openDoctorQueue(doctorId: string): void {
    this.nurseService.getDoctorQueue(doctorId).subscribe({
      next: data => this.activeDoctorQueue.set(data),
      error: () => this.toast.error('Failed to load doctor queue.')
    });
  }

  openPatientChart(patientId: string): void {
    this.nurseService.getPatientDetails(patientId).subscribe({
      next: (chart: PatientDetailResponse) => this.activePatientChart.set(chart),
      error: () => this.toast.error('Failed to load patient medical chart.')
    });
  }

  openQueueStatus(tokenId: string): void {
    this.nurseService.getPatientQueuePosition(tokenId).subscribe({
      next: pos => this.activeQueuePosition.set(pos),
      error: () => this.toast.error('Failed to retrieve patient queue position.')
    });
  }

  onSearchInput(val: string): void {
    this.searchQuery.set(val);
    this.searchSubject.next(val);
  }

  openBookExisting(patient: PatientSearchResult): void {
    this.bookExistingPatient.set(patient);
    this.bookExistingForm = {
      doctorId: this.doctors().length > 0 ? this.doctors()[0].doctorId : '',
      isEmergencyCodeRed: false,
      clinicalNotes: ''
    };
    this.showBookExistingModal.set(true);
  }

  submitBookExisting(): void {
    const patient = this.bookExistingPatient();
    if (!patient || !this.bookExistingForm.doctorId) {
      this.toast.warning('Please select an assigned physician.');
      return;
    }

    if (this.bookExistingForm.isEmergencyCodeRed && !this.bookExistingForm.clinicalNotes?.trim()) {
      this.toast.warning('Clinical emergency remarks are mandatory for Code-Red emergency bypass.');
      return;
    }

    this.isSubmittingBooking.set(true);
    this.nurseService.bookTokenForExistingPatient(patient.patientId, this.bookExistingForm).subscribe({
      next: (token) => {
        this.isSubmittingBooking.set(false);
        this.showBookExistingModal.set(false);
        this.toast.success(`Token ${token.tokenNumber} booked for ${patient.fullName}.`);
        this.loadAllData(false);
      },
      error: (err: any) => {
        this.isSubmittingBooking.set(false);
        this.toast.error(err.error?.message || 'Failed to book token.');
      }
    });
  }

  logout(): void {
    this.auth.logout().subscribe(() => {
      this.router.navigate(['/login']);
    });
  }

  // #endregion
}
