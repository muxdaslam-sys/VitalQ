/**
 * ============================================================================
 * COMPONENT: PatientHistoryComponent (Medical Consultation History & Records)
 * ============================================================================
 * 
 * PURPOSE:
 * Displays the complete clinical encounter timeline (completed visits, doctor
 * diagnosis notes, cancellation reasons, and triage records) for the patient
 * and their registered family dependents.
 * ============================================================================
 */

import { Component, OnInit, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { PatientService } from '../../../core/services/patient.service';
import { PatientVisitHistory, PatientProfile } from '../../../shared/models/patient.model';

@Component({
  selector: 'app-patient-history',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './patient-history.component.html',
  styleUrls: ['./patient-history.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PatientHistoryComponent implements OnInit {
  //#region 1. Dependencies & Services

  private patientService = inject(PatientService);

  //#endregion

  //#region 2. Reactive State & Filters

  /** Raw list of all past visits across the family */
  visits = signal<PatientVisitHistory[]>([]);

  /** Registered family dependents for filter pills */
  familyMembers = signal<PatientProfile[]>([]);

  /** Loading indicator during initial fetch */
  loading = signal<boolean>(true);

  /** Active family member filter (null = all records) */
  selectedFilterId = signal<string | null>(null);

  /** Search keyword filter */
  searchTerm = signal<string>('');

  //#endregion

  //#region 3. Computed Filtered Records

  /**
   * Dynamically filters encounters based on selected family member and search keyword.
   */
  filteredVisits = computed(() => {
    let list = this.visits();
    const filterId = this.selectedFilterId();
    if (filterId) {
      list = list.filter(v => v.patientId === filterId);
    }

    const query = this.searchTerm().trim().toLowerCase();
    if (query) {
      list = list.filter(v => 
        v.tokenNumber.toLowerCase().includes(query) ||
        v.doctorName.toLowerCase().includes(query) ||
        v.departmentName.toLowerCase().includes(query) ||
        (v.consultationNotes && v.consultationNotes.toLowerCase().includes(query))
      );
    }

    return list;
  });

  //#endregion

  //#region 4. Lifecycle & Batch Fetching

  ngOnInit(): void {
    this.loadHistoryData();
  }

  /**
   * Concurrently loads encounter history and family profiles using forkJoin.
   */
  loadHistoryData(): void {
    this.loading.set(true);
    forkJoin({
      history: this.patientService.getMyHistory(),
      family: this.patientService.getMyFamily()
    }).subscribe({
      next: (res) => {
        this.visits.set(res.history);
        this.familyMembers.set(res.family);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  //#endregion

  //#region 5. UI Helpers & Formatting

  /**
   * Calculates total visits completed by a specific family dependent.
   */
  getMemberVisitCount(memberId: string): number {
    return this.visits().filter(v => v.patientId === memberId).length;
  }

  /**
   * Returns human-readable label for encounter status.
   */
  getStatusDisplay(status: string): string {
    switch (status) {
      case 'Completed': return 'Visit Completed';
      case 'Cancelled': return 'Cancelled';
      case 'Expired': return 'Expired (Did not attend)';
      case 'Skipped': return 'On Hold / Missed';
      default: return status;
    }
  }

  /**
   * Returns badge style classes based on encounter outcome.
   */
  getStatusBadgeClass(status: string): string {
    switch (status) {
      case 'Completed':
        return 'bg-emerald-50 text-emerald-700 border border-emerald-200';
      case 'Cancelled':
        return 'bg-rose-50 text-rose-700 border border-rose-200';
      case 'Expired':
        return 'bg-slate-100 text-slate-700 border border-slate-300';
      case 'Skipped':
        return 'bg-amber-50 text-amber-800 border border-amber-200';
      default:
        return 'bg-slate-100 text-slate-700 border border-slate-200';
    }
  }

  /**
   * Returns badge style classes based on nurse triage urgency.
   */
  getTriageBadgeClass(level?: string): string {
    switch (level) {
      case 'Red':
        return 'bg-rose-50 text-rose-700 border-rose-200';
      case 'Yellow':
        return 'bg-amber-50 text-amber-700 border-amber-200';
      case 'Green':
        return 'bg-emerald-50 text-emerald-700 border-emerald-200';
      default:
        return 'bg-teal-50 text-teal-700 border-teal-200';
    }
  }

  //#endregion
}
