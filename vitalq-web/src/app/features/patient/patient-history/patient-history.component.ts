import { Component, OnInit, inject, signal, computed } from '@angular/core';
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
  styleUrls: ['./patient-history.component.css']
})
export class PatientHistoryComponent implements OnInit {
  private patientService = inject(PatientService);

  visits = signal<PatientVisitHistory[]>([]);
  familyMembers = signal<PatientProfile[]>([]);
  loading = signal<boolean>(true);

  // Filters
  selectedFilterId = signal<string | null>(null);
  searchTerm = signal<string>('');

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

  ngOnInit(): void {
    this.loadHistoryData();
  }

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

  getMemberVisitCount(memberId: string): number {
    return this.visits().filter(v => v.patientId === memberId).length;
  }

  getStatusDisplay(status: string): string {
    switch (status) {
      case 'Completed': return 'Visit Completed';
      case 'Cancelled': return 'Cancelled';
      case 'Skipped': return 'On Hold / Missed';
      default: return status;
    }
  }

  getStatusBadgeClass(status: string): string {
    switch (status) {
      case 'Completed':
        return 'bg-emerald-50 text-emerald-700 border border-emerald-200';
      case 'Cancelled':
        return 'bg-rose-50 text-rose-700 border border-rose-200';
      case 'Skipped':
        return 'bg-amber-50 text-amber-800 border border-amber-200';
      default:
        return 'bg-slate-100 text-slate-700 border border-slate-200';
    }
  }

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
}
