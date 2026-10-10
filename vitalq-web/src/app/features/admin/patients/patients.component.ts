/**
 * ============================================================================
 * COMPONENT: PatientsComponent (Outpatient Directory & Booking History)
 * ============================================================================
 * 
 * FEATURES:
 * 1. Displays up to 50 patients from the database.
 * 2. Real-time 300ms debounced search by MRN, Name, or Phone Number across the DB.
 * 3. Quick Clear Search (✖) button.
 * 4. Gender category filter chips ('All', 'Male', 'Female').
 * 5. Click ANY patient row to view full details, total completed bookings, and past bookings.
 * 6. One-click "Copy MRN" to clipboard with toast notification.
 * 7. Automatic calculation of patient age from Date of Birth.
 * ============================================================================
 */

import { Component, OnInit, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AdminService } from '../../../core/services/admin.service';
import { ToastService } from '../../../core/services/toast.service';
import { PatientDetailResponse } from '../../../shared/models/admin.model';
import { TeleportToBodyDirective } from '../../../shared/directives/teleport.directive';

@Component({
  selector: 'app-patients',
  standalone: true,
  imports: [CommonModule, FormsModule, TeleportToBodyDirective],
  templateUrl: './patients.component.html',
  styleUrl: './patients.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PatientsComponent implements OnInit {
  // --------------------------------------------------------------------------
  // 1. DEPENDENCY INJECTION
  // --------------------------------------------------------------------------
  private adminService = inject(AdminService);
  private toast = inject(ToastService);

  // --------------------------------------------------------------------------
  // 2. SEARCH DEBOUNCE STREAM
  // --------------------------------------------------------------------------
  private searchSubject = new Subject<string>();

  // --------------------------------------------------------------------------
  // 3. COMPONENT STATE (SIGNALS)
  // --------------------------------------------------------------------------
  patients = signal<PatientDetailResponse[]>([]);
  searchQuery = signal('');
  selectedGender = signal<'all' | 'Male' | 'Female'>('all');
  selectedPatient = signal<PatientDetailResponse | null>(null);
  isLoading = signal(false);

  constructor() {
    // 300ms debounce to prevent hammering the server while the user types
    this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntilDestroyed()
    ).subscribe(term => {
      this.loadPatients(term);
    });
  }

  // --------------------------------------------------------------------------
  // 4. COMPUTED GENDER FILTER
  // --------------------------------------------------------------------------
  filteredPatients = computed(() => {
    const list = this.patients();
    const gender = this.selectedGender();
    if (gender === 'all') return list;
    return list.filter(p => p.gender.toLowerCase() === gender.toLowerCase());
  });

  // --------------------------------------------------------------------------
  // 5. LIFECYCLE HOOKS & DATA LOADING
  // --------------------------------------------------------------------------
  ngOnInit(): void {
    this.loadPatients();
  }

  /**
   * Loads up to 50 patients from the backend API.
   * If query is supplied, searches across MRN, Full Name, and Phone Number.
   */
  loadPatients(query?: string): void {
    this.isLoading.set(true);
    this.adminService.getPatients(query).subscribe({
      next: p => {
        this.patients.set(p);
        this.isLoading.set(false);
      },
      error: () => {
        this.patients.set([]);
        this.isLoading.set(false);
        this.toast.error('Failed to load patient directory.');
      }
    });
  }

  // --------------------------------------------------------------------------
  // 6. USER ACTIONS & SEARCH CONTROLS
  // --------------------------------------------------------------------------

  /** Called on every keystroke in search box; debounced by 300ms */
  onSearchInput(value: string): void {
    this.searchQuery.set(value);
    this.searchSubject.next(value);
  }

  /** Clears search and reloads latest 50 patients */
  clearSearch(): void {
    this.searchQuery.set('');
    this.loadPatients();
  }

  /** Opens full profile and booking history modal for clicked patient */
  selectPatient(p: PatientDetailResponse): void {
    this.selectedPatient.set(p);
  }

  /** Closes patient details modal */
  closeDetails(): void {
    this.selectedPatient.set(null);
  }

  /** Copies Medical Record Number to clipboard */
  copyMrn(mrn: string, event: MouseEvent): void {
    event.stopPropagation(); // Prevents triggering row click
    navigator.clipboard.writeText(mrn);
    this.toast.success(`Copied MRN: ${mrn}`);
  }

  /** Calculates patient's age in years from Date of Birth */
  calculateAge(dobString: string): number {
    if (!dobString) return 0;
    const dob = new Date(dobString);
    const today = new Date();
    let age = today.getFullYear() - dob.getFullYear();
    const monthDiff = today.getMonth() - dob.getMonth();
    if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < dob.getDate())) {
      age--;
    }
    return Math.max(0, age);
  }
}
