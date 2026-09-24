/**
 * ============================================================================
 * COMPONENT: PatientsComponent (Outpatient Directory & Clinical History)
 * ============================================================================
 * 
 * PURPOSE:
 * Allows Hospital Administrators to:
 * 1. Browse complete patient registration records and Medical Record Numbers (MRN).
 * 2. Search patients by MRN, full name, or phone number.
 * 3. Inspect individual patient consultation visits, assigned triage levels (Red/Yellow/Green),
 *    consulting doctors, and consultation notes.
 * 
 * ARCHITECTURAL DESIGN:
 * - Angular 19 Signals for clean, high-performance reactivity.
 * - Reactive computed filtering for instantaneous searching.
 * - In-app ToastService for non-blocking notifications.
 * ============================================================================
 */

import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { ToastService } from '../../../core/services/toast.service';
import { PatientDetailResponse } from '../../../shared/models/admin.model';

@Component({
  selector: 'app-patients',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './patients.component.html',
  styleUrl: './patients.component.css'
})
export class PatientsComponent implements OnInit {
  // --------------------------------------------------------------------------
  // 1. DEPENDENCY INJECTION
  // --------------------------------------------------------------------------
  private adminService = inject(AdminService);
  private toast = inject(ToastService);

  // --------------------------------------------------------------------------
  // 2. COMPONENT STATE (SIGNALS)
  // --------------------------------------------------------------------------

  /** Master list of patient records retrieved from database */
  patients = signal<PatientDetailResponse[]>([]);

  /** Query string typed in search box */
  searchQuery = signal('');

  /** Selected gender filter ('all', 'Male', 'Female', 'Other') */
  selectedGender = signal<'all' | 'Male' | 'Female' | 'Other'>('all');

  /** Patient whose clinical visit history is currently open in modal dialog */
  selectedPatient = signal<PatientDetailResponse | null>(null);

  // --------------------------------------------------------------------------
  // 3. COMPUTED REACTIVE FILTER
  // --------------------------------------------------------------------------
  filteredPatients = computed(() => {
    let list = this.patients();
    const query = this.searchQuery().trim().toLowerCase();
    const gender = this.selectedGender();

    // 1. Text Search Filter (MRN, Name, Phone Number)
    if (query) {
      list = list.filter(p =>
        p.fullName.toLowerCase().includes(query) ||
        p.phoneNumber.includes(query) ||
        p.medicalRecordNumber.toLowerCase().includes(query)
      );
    }

    // 2. Gender Category Filter
    if (gender !== 'all') {
      list = list.filter(p => p.gender.toLowerCase() === gender.toLowerCase());
    }

    return list;
  });

  // --------------------------------------------------------------------------
  // 4. LIFECYCLE HOOKS & DATA LOADING
  // --------------------------------------------------------------------------
  ngOnInit(): void {
    this.loadPatients();
  }

  /**
   * Fetches latest patient records from backend API.
   */
  loadPatients(): void {
    this.adminService.getPatients().subscribe({
      next: p => this.patients.set(p),
      error: () => {
        this.patients.set([]);
        this.toast.error('Failed to load patient directory from server.');
      }
    });
  }

  // --------------------------------------------------------------------------
  // 5. USER ACTIONS & MODAL CONTROLS
  // --------------------------------------------------------------------------

  /**
   * Opens the visit history modal for a selected patient.
   */
  openHistory(p: PatientDetailResponse): void {
    this.selectedPatient.set(p);
  }

  /**
   * Closes the visit history modal.
   */
  closeHistory(): void {
    this.selectedPatient.set(null);
  }
}
