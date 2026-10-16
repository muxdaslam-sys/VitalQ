/**
 * NurseDashboardComponent: Real-time clinical command center for nursing stations.
 * Features:
 * - Live KPI summary (Bookings, Triaged, Completed, Doctors on Duty)
 * - Doctor Roster with click-to-expand live priority queue
 * - Unified Patient Intake Hub with 3 Action Modals:
 *     1. [Full Details] -> Medical Chart, MRN, Past consultation notes
 *     2. [Queue Status] -> Exact # position, wait time, Doctor live line visualizer
 *     3. [Triage Assessment] -> 5 Vitals intake, CTAS Red/Yellow/Green engine
 * - Smart Emergency Walk-In with existing patient recognition & Code-Red Bypass
 */

import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription, Subject, of } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, catchError } from 'rxjs/operators';

import { AuthService } from '../../../core/services/auth.service';
import { NurseService } from '../../../core/services/nurse.service';
import { SignalRService } from '../../../core/services/signalr.service';
import { ToastService } from '../../../core/services/toast.service';
import { TeleportToBodyDirective } from '../../../shared/directives/teleport.directive';

import { QueueToken } from '../../../shared/models/queue.model';
import { TriageRequest, PatientSearchResult } from '../../../shared/models/triage.model';
import {
  NurseDashboardStatsResponse,
  DoctorQueueSummaryDto,
  DoctorDetailedQueueResponse,
  PatientQueuePositionResponse,
  EmergencyWalkInBookingRequest,
  ExistingPatientTokenBookingRequest
} from '../../../shared/models/nurse.model';

import { PatientDetailResponse } from '../../../shared/models/admin.model';

@Component({
  selector: 'app-nurse-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, TeleportToBodyDirective],
  templateUrl: './nurse-dashboard.component.html',
  styleUrl: './nurse-dashboard.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NurseDashboardComponent implements OnInit, OnDestroy {
  // #region 1. Dependency Injection & State Initialization
  auth = inject(AuthService);
  private nurseService = inject(NurseService);
  private signalR = inject(SignalRService);
  private toast = inject(ToastService);
  private router = inject(Router);

  private subs = new Subscription();
  private searchSubject = new Subject<string>();

  constructor() {
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

  // #region 2. Dashboard Reactive State & View Modes
  stats = signal<NurseDashboardStatsResponse | null>(null);
  pendingTriage = signal<QueueToken[]>([]);
  isSubmitting = signal<boolean>(false);
  isRefreshing = signal<boolean>(false);

  /** Active Intake Hub View Tab: 'arrivals' (Station Desk) vs 'directory' (Hospital Patient Lookup) */
  activeTab = signal<'arrivals' | 'directory'>('arrivals');
  // #endregion

  // #region 3. Doctor Roster & Modal State
  showDoctorModal = signal<boolean>(false);
  isLoadingDoctorQueue = signal<boolean>(false);
  selectedDoctorQueue = signal<DoctorDetailedQueueResponse | null>(null);
  expandedTokenId = signal<string | null>(null);
  // #endregion

  // #region 4. Unified Patient Intake & Search State
  /** Local in-table filter query for the Station Arrivals table */
  stationFilterQuery = signal<string>('');

  /** Hospital-wide patient directory search query (Tab 2) */
  searchQuery = signal<string>('');
  searchResults = signal<PatientSearchResult[]>([]);
  isSearching = signal<boolean>(false);
  
  /** Currently selected patient at the intake desk */
  selectedPatient = signal<PatientSearchResult | null>(null);
  patientTokens = signal<QueueToken[]>([]);
  isLoadingPatientTokens = signal<boolean>(false);

  /** Filtered station triage queue based on local table filter input */
  filteredStationQueue = computed(() => {
    const q = this.stationFilterQuery().trim().toLowerCase();
    const queue = this.pendingTriage();
    if (!q) return queue;
    return queue.filter(t =>
      t.tokenNumber.toLowerCase().includes(q) ||
      t.patientName.toLowerCase().includes(q) ||
      t.patientPhone.includes(q) ||
      t.doctorName.toLowerCase().includes(q)
    );
  });
  // #endregion

  // #region 5. Action Modals State (Details, Queue, Triage)
  // Action 1: Full Details Modal
  showDetailsModal = signal<boolean>(false);
  isLoadingDetails = signal<boolean>(false);
  patientDetails = signal<PatientDetailResponse | null>(null);

  // Action 2: Queue Status Modal (Position & Doctor Line Visualizer)
  showQueueModal = signal<boolean>(false);
  isLoadingQueue = signal<boolean>(false);
  queuePositionData = signal<PatientQueuePositionResponse | null>(null);

  // Action 3: Triage Assessment Modal
  showTriageModal = signal<boolean>(false);
  triageToken = signal<QueueToken | null>(null);
  vitalsForm: TriageRequest = {
    spO2: 98,
    systolicBp: 120,
    diastolicBp: 80,
    heartRate: 75,
    temperature: 37.0,
    painScale: 0,
    isManualOverride: false,
    overrideTriageLevel: null,
    overrideReason: null,
    nurseNotes: null
  };

  /** Real-time CTAS calculation preview */
  computedLevel = computed(() => {
    return this.nurseService.evaluateLevel(
      this.vitalsForm.spO2,
      this.vitalsForm.systolicBp,
      this.vitalsForm.heartRate,
      this.vitalsForm.temperature,
      this.vitalsForm.painScale
    );
  });

  /** Effective level (respects manual override if checked) */
  effectiveLevel = computed(() => {
    if (this.vitalsForm.isManualOverride && this.vitalsForm.overrideTriageLevel) {
      return this.vitalsForm.overrideTriageLevel;
    }
    return this.computedLevel();
  });
  // #endregion

  // #region 6. Emergency Walk-In & Instant Booking State
  showWalkInModal = signal<boolean>(false);
  walkInForm: EmergencyWalkInBookingRequest = {
    fullName: '',
    phoneNumber: '',
    dateOfBirth: '' as any,
    gender: 'Male',
    doctorId: '',
    isEmergencyCodeRed: false,
    clinicalNotes: ''
  };
  existingPatientMatch = signal<PatientSearchResult | null>(null);
  walkInSuccessToken = signal<QueueToken | null>(null);

  // Instant token booking for registered patients without demographic re-entry
  showBookExistingModal = signal<boolean>(false);
  bookExistingPatient = signal<PatientSearchResult | null>(null);
  bookExistingForm: ExistingPatientTokenBookingRequest = {
    doctorId: '',
    isEmergencyCodeRed: false,
    clinicalNotes: ''
  };
  bookExistingSuccessToken = signal<QueueToken | null>(null);
  // #endregion

  // #region 7. Lifecycle & Real-Time Sync
  ngOnInit(): void {
    this.loadDashboard();
    this.loadPendingTriage();
    this.initSignalR();
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  private initSignalR(): void {
    this.signalR.startConnection().catch(err => {
      console.warn('SignalR connection failed, falling back to on-demand refresh:', err);
    });

    this.subs.add(
      this.signalR.onTokenBooked$.subscribe(() => {
        this.loadDashboard(false);
        this.loadPendingTriage(false);
        this.refreshActiveDoctorQueue();
      })
    );

    this.subs.add(
      this.signalR.onQueueUpdated$.subscribe(() => {
        this.loadDashboard(false);
        this.loadPendingTriage(false);
        this.refreshActiveDoctorQueue();
      })
    );

    this.subs.add(
      this.signalR.onPatientCalled$.subscribe(() => {
        this.loadDashboard(false);
        this.refreshActiveDoctorQueue();
      })
    );
  }

  private refreshActiveDoctorQueue(): void {
    const currentDoc = this.selectedDoctorQueue();
    if (this.showDoctorModal() && currentDoc) {
      this.nurseService.getDoctorQueue(currentDoc.doctorId).subscribe({
        next: data => this.selectedDoctorQueue.set(data),
        error: () => {}
      });
    }

    const currentToken = this.queuePositionData();
    if (this.showQueueModal() && currentToken) {
      this.nurseService.getPatientQueuePosition(currentToken.tokenId).subscribe({
        next: data => this.queuePositionData.set(data),
        error: () => {}
      });
    }
  }

  // #region 8. Dashboard Data Loading
  loadDashboard(showLoader = true): void {
    if (showLoader) this.isRefreshing.set(true);
    this.nurseService.getDashboardStats().subscribe({
      next: data => {
        this.stats.set(data);
        setTimeout(() => this.isRefreshing.set(false), 400);
      },
      error: () => {
        this.isRefreshing.set(false);
      }
    });
  }

  loadPendingTriage(showLoader = true): void {
    this.nurseService.getPendingTriage().subscribe({
      next: tokens => this.pendingTriage.set(tokens),
      error: () => this.pendingTriage.set([])
    });
  }
  // #endregion

  // #region 9. Doctor Queue Inspection Actions
  openDoctorQueue(doc: DoctorQueueSummaryDto): void {
    this.isLoadingDoctorQueue.set(true);
    this.showDoctorModal.set(true);
    this.expandedTokenId.set(null);

    this.nurseService.getDoctorQueue(doc.doctorId).subscribe({
      next: data => {
        this.selectedDoctorQueue.set(data);
        this.isLoadingDoctorQueue.set(false);
      },
      error: () => {
        this.toast.error('Failed to load live doctor queue.');
        this.isLoadingDoctorQueue.set(false);
        this.showDoctorModal.set(false);
      }
    });
  }

  closeDoctorModal(): void {
    this.showDoctorModal.set(false);
    this.selectedDoctorQueue.set(null);
    this.expandedTokenId.set(null);
  }

  toggleTokenDetails(tokenId: string): void {
    this.expandedTokenId.update(curr => (curr === tokenId ? null : tokenId));
  }
  // #endregion

  // #region 10. Unified Intake Desk & Patient Selection
  onSearchInput(value: string): void {
    this.searchQuery.set(value);
    this.searchSubject.next(value);
  }

  clearSearch(): void {
    this.searchQuery.set('');
    this.searchResults.set([]);
    this.isSearching.set(false);
  }

  selectPatient(patient: PatientSearchResult): void {
    this.selectedPatient.set(patient);
    this.searchResults.set([]);
    this.isLoadingPatientTokens.set(true);

    this.nurseService.getPatientActiveTokens(patient.patientId).subscribe({
      next: tokens => {
        this.patientTokens.set(tokens);
        this.isLoadingPatientTokens.set(false);
      },
      error: () => {
        this.patientTokens.set([]);
        this.isLoadingPatientTokens.set(false);
        this.toast.error('Failed to retrieve tokens for patient.');
      }
    });
  }

  clearSelectedPatient(): void {
    this.selectedPatient.set(null);
    this.patientTokens.set([]);
  }
  // #endregion

  // #region 11. Patient Action Modals Handlers
  // Action Modal 1: Full Details & Medical Chart
  openFullDetails(patientId: string): void {
    this.isLoadingDetails.set(true);
    this.showDetailsModal.set(true);
    this.nurseService.getPatientDetails(patientId).subscribe({
      next: details => {
        this.patientDetails.set(details);
        this.isLoadingDetails.set(false);
      },
      error: () => {
        this.toast.error('Failed to load patient medical chart.');
        this.isLoadingDetails.set(false);
        this.showDetailsModal.set(false);
      }
    });
  }

  closeDetailsModal(): void {
    this.showDetailsModal.set(false);
    this.patientDetails.set(null);
  }

  // Action Modal 2: Queue Status & Doctor Line Visualizer
  openQueueStatus(tokenOrId: QueueToken | { id: string } | string): void {
    const tokenId = typeof tokenOrId === 'string' ? tokenOrId : tokenOrId.id;
    this.isLoadingQueue.set(true);
    this.showQueueModal.set(true);
    this.nurseService.getPatientQueuePosition(tokenId).subscribe({
      next: pos => {
        this.queuePositionData.set(pos);
        this.isLoadingQueue.set(false);
      },
      error: () => {
        this.toast.error('Failed to load queue line visualizer.');
        this.isLoadingQueue.set(false);
        this.showQueueModal.set(false);
      }
    });
  }

  calculateAge(dob: string | undefined | null): string {
    if (!dob) return '';
    const birth = new Date(dob);
    if (isNaN(birth.getTime())) return '';
    const now = new Date();
    let age = now.getFullYear() - birth.getFullYear();
    const m = now.getMonth() - birth.getMonth();
    if (m < 0 || (m === 0 && now.getDate() < birth.getDate())) {
      age--;
    }
    return age >= 0 ? `${age} yrs` : '';
  }

  closeQueueModal(): void {
    this.showQueueModal.set(false);
    this.queuePositionData.set(null);
  }

  isTokenTriagable(token: QueueToken | undefined | null): boolean {
    if (!token) return false;
    return token.status === 'Booked' || token.status === 'Waiting';
  }

  // Action Modal 3: Triage Assessment & Vital Signs Intake
  openTriageModal(token: QueueToken): void {
    if (token.status === 'Completed' || token.status === 'Cancelled') {
      this.toast.warning(`Cannot triage a token that is already ${token.status.toLowerCase()}.`);
      return;
    }
    this.triageToken.set(token);
    this.vitalsForm = {
      spO2: 98,
      systolicBp: 120,
      diastolicBp: 80,
      heartRate: 75,
      temperature: 37.0,
      painScale: 0,
      isManualOverride: false,
      overrideTriageLevel: 'Yellow',
      overrideReason: '',
      nurseNotes: ''
    };
    this.showTriageModal.set(true);
  }

  closeTriageModal(): void {
    this.showTriageModal.set(false);
    this.triageToken.set(null);
  }

  submitTriage(): void {
    const token = this.triageToken();
    if (!token) return;

    if (this.vitalsForm.isManualOverride && !this.vitalsForm.overrideReason?.trim()) {
      this.toast.warning('A reason is mandatory when manually setting triage level.');
      return;
    }

    this.isSubmitting.set(true);
    this.nurseService.submitTriage(token.id, this.vitalsForm).subscribe({
      next: updatedToken => {
        this.isSubmitting.set(false);
        this.closeTriageModal();
        this.toast.success(`Vitals recorded for ${token.tokenNumber}. Level: ${updatedToken.triageLevel}`);
        this.loadDashboard();
        this.loadPendingTriage();
        if (this.selectedPatient()) {
          this.selectPatient(this.selectedPatient()!);
        }
      },
      error: err => {
        this.isSubmitting.set(false);
        this.toast.error(err.error?.message || 'Failed to submit triage vitals.');
      }
    });
  }
  // #endregion

  // #region 12. Enhanced Emergency Walk-In Intake
  openWalkInModal(): void {
    const defaultDocId = this.stats()?.doctors?.[0]?.doctorId || '';
    this.walkInForm = {
      fullName: '',
      phoneNumber: '',
      dateOfBirth: '' as any,
      gender: 'Male',
      doctorId: defaultDocId,
      isEmergencyCodeRed: false,
      clinicalNotes: ''
    };
    this.existingPatientMatch.set(null);
    this.walkInSuccessToken.set(null);
    this.showWalkInModal.set(true);
  }

  closeWalkInModal(): void {
    this.showWalkInModal.set(false);
    this.walkInSuccessToken.set(null);
    this.existingPatientMatch.set(null);
  }

  /**
   * Phone lookup for returning walk-in patients (prevents duplicate charts).
   */
  onWalkInPhoneChange(): void {
    const phone = this.walkInForm.phoneNumber.trim();
    if (phone.length >= 10) {
      this.nurseService.searchPatients(phone).subscribe({
        next: results => {
          if (results.length > 0) {
            const match = results[0];
            this.existingPatientMatch.set(match);
            this.walkInForm.fullName = match.fullName;
            this.walkInForm.dateOfBirth = match.dateOfBirth as any;
            this.walkInForm.gender = match.gender || 'Male';
          } else {
            this.existingPatientMatch.set(null);
          }
        },
        error: () => this.existingPatientMatch.set(null)
      });
    } else {
      this.existingPatientMatch.set(null);
    }
  }

  submitWalkIn(): void {
    if (!this.walkInForm.fullName || !this.walkInForm.phoneNumber || !this.walkInForm.dateOfBirth) {
      this.toast.warning('Please fill in Name, Phone Number, and Date of Birth.');
      return;
    }

    if (!this.walkInForm.doctorId) {
      this.toast.warning('Please select an on-duty doctor.');
      return;
    }

    if (this.walkInForm.isEmergencyCodeRed && !this.walkInForm.clinicalNotes?.trim()) {
      this.toast.warning('Emergency clinical remarks are mandatory for Immediate Code-Red Bypass.');
      return;
    }

    this.isSubmitting.set(true);
    this.nurseService.processEmergencyWalkIn(this.walkInForm).subscribe({
      next: token => {
        this.isSubmitting.set(false);
        this.walkInSuccessToken.set(token);
        const msg = this.walkInForm.isEmergencyCodeRed
          ? `[CODE-RED EMERGENCY]: Slip ${token.tokenNumber} issued and pushed to FRONT of Dr. ${token.doctorName}'s queue!`
          : `Walk-in slip ${token.tokenNumber} issued successfully!`;
        this.toast.success(msg);
        this.loadDashboard();
        this.loadPendingTriage();
      },
      error: err => {
        this.isSubmitting.set(false);
        this.toast.error(err.error?.message || 'Failed to issue walk-in token.');
      }
    });
  }

  startTriageForWalkIn(): void {
    const token = this.walkInSuccessToken();
    if (token) {
      this.closeWalkInModal();
      this.openTriageModal(token);
    }
  }
  // #endregion

  // #region 13. Instant Token Booking for Registered Patients
  openBookExistingModal(patient: PatientSearchResult): void {
    const defaultDocId = this.stats()?.doctors?.[0]?.doctorId || '';
    this.bookExistingPatient.set(patient);
    this.bookExistingForm = {
      doctorId: defaultDocId,
      isEmergencyCodeRed: false,
      clinicalNotes: ''
    };
    this.bookExistingSuccessToken.set(null);
    this.showBookExistingModal.set(true);
  }

  closeBookExistingModal(): void {
    this.showBookExistingModal.set(false);
    this.bookExistingPatient.set(null);
    this.bookExistingSuccessToken.set(null);
  }

  submitBookExisting(): void {
    const patient = this.bookExistingPatient();
    if (!patient) return;

    if (!this.bookExistingForm.doctorId) {
      this.toast.warning('Please select an on-duty doctor.');
      return;
    }

    if (this.bookExistingForm.isEmergencyCodeRed && !this.bookExistingForm.clinicalNotes?.trim()) {
      this.toast.warning('Emergency clinical remarks are mandatory for Immediate Code-Red Bypass.');
      return;
    }

    this.isSubmitting.set(true);
    this.nurseService.bookTokenForExistingPatient(patient.patientId, this.bookExistingForm).subscribe({
      next: token => {
        this.isSubmitting.set(false);
        this.bookExistingSuccessToken.set(token);
        const msg = this.bookExistingForm.isEmergencyCodeRed
          ? `[CODE-RED EMERGENCY]: Slip ${token.tokenNumber} issued and pushed to FRONT of Dr. ${token.doctorName}'s queue!`
          : `Consultation token ${token.tokenNumber} issued for ${patient.fullName}!`;
        this.toast.success(msg);
        this.loadDashboard();
        this.loadPendingTriage();
        if (this.selectedPatient()?.patientId === patient.patientId) {
          this.selectPatient(patient);
        }
      },
      error: err => {
        this.isSubmitting.set(false);
        this.toast.error(err.error?.message || 'Failed to issue token for existing patient.');
      }
    });
  }

  startTriageForExistingToken(): void {
    const token = this.bookExistingSuccessToken();
    if (token) {
      this.closeBookExistingModal();
      this.openTriageModal(token);
    }
  }
  // #endregion

  // #region 14. Session Management
  logout(): void {
    this.auth.logout().subscribe(() => this.router.navigate(['/login']));
  }
  // #endregion
}

