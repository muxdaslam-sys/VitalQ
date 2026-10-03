import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { Subscription } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { QueueService } from '../../../core/services/queue.service';
import { SignalRService } from '../../../core/services/signalr.service';
import { TriageService } from '../../../core/services/triage.service';
import { Doctor, QueueToken, CompleteConsultationRequest, TriageAssessment } from '../../../shared/models';
import { environment } from '../../../../environments/environment';

export type DoctorProfile = Doctor;

@Component({
  selector: 'app-doctor-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './doctor-dashboard.component.html',
  styleUrl: './doctor-dashboard.component.css'
})
export class DoctorDashboardComponent implements OnInit, OnDestroy {
  auth = inject(AuthService);
  private router = inject(Router);
  private http = inject(HttpClient);
  private queueService = inject(QueueService);
  private signalR = inject(SignalRService);
  private triageService = inject(TriageService);

  doctorProfile = signal<DoctorProfile | null>(null);
  currentCalled = signal<QueueToken | null>(null);
  currentVitals = signal<TriageAssessment | null>(null);
  waitingQueue = signal<QueueToken[]>([]);
  skippedQueue = signal<QueueToken[]>([]);

  isLoading = signal<boolean>(true);
  errorMessage = signal<string>('');
  successMessage = signal<string>('');

  // Complete consultation modal
  isCompleteModalOpen = signal<boolean>(false);
  consultationNotes = signal<string>('');
  isSubmittingComplete = signal<boolean>(false);

  private subs: Subscription[] = [];

  ngOnInit(): void {
    this.loadDoctorProfileAndQueue();
  }

  ngOnDestroy(): void {
    if (this.doctorProfile()) {
      this.signalR.leaveDoctorGroup(this.doctorProfile()!.id);
    }
    this.subs.forEach(s => s.unsubscribe());
  }

  loadDoctorProfileAndQueue(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');

    // Fetch doctor profile for current user
    this.http.get<DoctorProfile>(`${environment.apiUrl}/doctors/me`).subscribe({
      next: (doc) => {
        this.doctorProfile.set(doc);
        this.fetchQueue(doc.id);
        this.connectSignalR(doc.id);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err.error?.message || 'Could not load doctor profile. Make sure you are logged in as a Doctor.');
      }
    });
  }

  fetchQueue(doctorId: string): void {
    this.queueService.getDoctorQueue(doctorId).subscribe({
      next: (tokens) => {
        const called = tokens.find(t => t.status === 'Called') || null;
        this.currentCalled.set(called);

        const waiting = tokens.filter(t => t.status === 'Waiting');
        this.waitingQueue.set(waiting);

        const skipped = tokens.filter(t => t.status === 'Skipped');
        this.skippedQueue.set(skipped);

        this.isLoading.set(false);

        // If there's an active in-room patient, load their vitals assessment
        if (called) {
          this.loadVitals(called.id);
        } else {
          this.currentVitals.set(null);
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err.error?.message || 'Failed to load queue.');
      }
    });
  }

  loadVitals(tokenId: string): void {
    this.triageService.getTriageAssessment(tokenId).subscribe({
      next: (vitals) => this.currentVitals.set(vitals),
      error: () => this.currentVitals.set(null)
    });
  }

  connectSignalR(doctorId: string): void {
    this.signalR.startConnection().then(() => {
      this.signalR.joinDoctorGroup(doctorId);

      const sub1 = this.signalR.onQueueUpdated$.subscribe(() => {
        this.fetchQueue(doctorId);
      });

      const sub2 = this.signalR.onTokenBooked$.subscribe(() => {
        this.fetchQueue(doctorId);
      });

      const sub3 = this.signalR.onScoresRecalculated$.subscribe(() => {
        this.fetchQueue(doctorId);
      });

      this.subs.push(sub1, sub2, sub3);
    });
  }

  callNext(): void {
    if (!this.doctorProfile()) return;
    this.errorMessage.set('');
    this.successMessage.set('');

    this.queueService.callNext(this.doctorProfile()!.id).subscribe({
      next: (token) => {
        this.currentCalled.set(token);
        this.successMessage.set(`Patient ${token.tokenNumber} (${token.patientName}) called into ${this.doctorProfile()!.roomNumber}!`);
        this.fetchQueue(this.doctorProfile()!.id);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Could not call next patient.');
      }
    });
  }

  skipCurrentPatient(): void {
    const current = this.currentCalled();
    if (!current) return;

    if (!confirm(`Mark patient ${current.tokenNumber} as absent / on hold?`)) return;

    this.errorMessage.set('');
    this.queueService.skipPatient(current.id).subscribe({
      next: () => {
        this.currentCalled.set(null);
        this.currentVitals.set(null);
        this.successMessage.set(`Token ${current.tokenNumber} was put on hold.`);
        this.fetchQueue(this.doctorProfile()!.id);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Could not skip patient.');
      }
    });
  }

  openCompleteModal(): void {
    this.consultationNotes.set('');
    this.isCompleteModalOpen.set(true);
  }

  closeCompleteModal(): void {
    this.isCompleteModalOpen.set(false);
  }

  submitCompleteConsultation(): void {
    const current = this.currentCalled();
    if (!current) return;

    this.isSubmittingComplete.set(true);
    this.queueService.completeConsultation(current.id, this.consultationNotes()).subscribe({
      next: () => {
        this.isSubmittingComplete.set(false);
        this.isCompleteModalOpen.set(false);
        this.currentCalled.set(null);
        this.currentVitals.set(null);
        this.successMessage.set(`Consultation completed for ${current.tokenNumber} (${current.patientName}).`);
        this.fetchQueue(this.doctorProfile()!.id);
      },
      error: (err) => {
        this.isSubmittingComplete.set(false);
        this.errorMessage.set(err.error?.message || 'Failed to complete consultation.');
      }
    });
  }

  requeuePatient(token: QueueToken): void {
    this.queueService.requeuePatient(token.id).subscribe({
      next: () => {
        this.successMessage.set(`Token ${token.tokenNumber} re-activated into waiting queue.`);
        this.fetchQueue(this.doctorProfile()!.id);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Could not re-queue patient.');
      }
    });
  }

  getTriageBadgeClass(level?: string): string {
    switch (level) {
      case 'Red':
        return 'bg-rose-100 text-rose-800 border-rose-200';
      case 'Yellow':
        return 'bg-amber-100 text-amber-800 border-amber-200';
      case 'Green':
        return 'bg-emerald-100 text-emerald-800 border-emerald-200';
      default:
        return 'bg-slate-100 text-slate-700 border-slate-200';
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

  logout(): void {
    this.auth.logout().subscribe(() => this.router.navigate(['/login']));
  }
}
