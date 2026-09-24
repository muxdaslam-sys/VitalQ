/**
 * ============================================================================
 * COMPONENT: DashboardComponent (Clinical Command Center Dashboard)
 * ============================================================================
 * 
 * PURPOSE:
 * Provides hospital administrators with an executive, live overview of:
 * 1. Active Doctor Fleet capacity and desk readiness percentage.
 * 2. Triage & Nursing staff headcounts.
 * 3. Clinical departments and intake triage desks.
 * 4. Real-time CTAS Triage Distribution (Red, Yellow, Green patient volume).
 * 
 * ARCHITECTURE & DATA FLOW:
 * - Uses forkJoin to fetch all operational datasets concurrently on initialization.
 * - Reactive Signals store counts, allowing instantaneous rendering.
 * - Patient visit records are analyzed to compute live emergency triage statistics.
 * ============================================================================
 */

import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { forkJoin, catchError, of } from 'rxjs';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit {
  // --------------------------------------------------------------------------
  // 1. DEPENDENCY INJECTION
  // --------------------------------------------------------------------------
  private adminService = inject(AdminService);

  // --------------------------------------------------------------------------
  // 2. CORE OPERATIONAL KPI SIGNALS
  // --------------------------------------------------------------------------
  
  /** Total registered physicians */
  totalDoctors = signal(0);

  /** Doctors currently marked 'Available' on their shift */
  availableDoctors = signal(0);

  /** Percentage of doctors on shift (available / total * 100) */
  doctorAvailabilityRate = signal(0);

  /** Total administrative and nursing personnel accounts */
  totalStaff = signal(0);

  /** Active clinical departments (Cardiology, Orthopedics, etc.) */
  totalDepartments = signal(0);

  /** Physical nursing triage desks distributed across hospital floors */
  totalStations = signal(0);

  /** Lifetime registered patient records */
  totalPatients = signal(0);

  // --------------------------------------------------------------------------
  // 3. CLINICAL TRIAGE TELEMETRY SIGNALS
  // Computed by aggregating all historical visits from the patient directory.
  // --------------------------------------------------------------------------
  
  /** CTAS 1 & 2: Resuscitation / Emergent (Highest priority, immediate danger) */
  triageRed = signal(0);

  /** CTAS 3: Urgent / Severe Distress (High priority, urgent medical review) */
  triageYellow = signal(0);

  /** CTAS 4 & 5: Less Urgent / Standard Walk-in (Normal outpatient priority) */
  triageGreen = signal(0);

  /** Total outpatient consultations recorded across all departments */
  totalVisits = signal(0);

  // --------------------------------------------------------------------------
  // 4. LIFECYCLE HOOK
  // --------------------------------------------------------------------------
  ngOnInit(): void {
    this.loadHospitalMetrics();
  }

  /**
   * Concurrently loads doctors, users, departments, stations, and patient records.
   * Uses catchError to ensure one failed API call does not crash the entire dashboard.
   */
  loadHospitalMetrics(): void {
    forkJoin({
      doctors: this.adminService.getDoctors().pipe(catchError(() => of([]))),
      users: this.adminService.getUsers().pipe(catchError(() => of([]))),
      departments: this.adminService.getDepartments().pipe(catchError(() => of([]))),
      stations: this.adminService.getNursingStations().pipe(catchError(() => of([]))),
      patients: this.adminService.getPatients().pipe(catchError(() => of([])))
    }).subscribe(({ doctors, users, departments, stations, patients }) => {
      // 1. Doctor Metrics
      this.totalDoctors.set(doctors.length);
      const onDuty = doctors.filter(d => d.status === 'Available').length;
      this.availableDoctors.set(onDuty);
      this.doctorAvailabilityRate.set(doctors.length > 0 ? Math.round((onDuty / doctors.length) * 100) : 0);

      // 2. Staff, Department & Station Metrics
      this.totalStaff.set(users.length);
      this.totalDepartments.set(departments.length);
      this.totalStations.set(stations.length);
      this.totalPatients.set(patients.length);

      // 3. Compute CTAS Triage Distribution across all patient visits
      let redCount = 0;
      let yellowCount = 0;
      let greenCount = 0;
      let visitCount = 0;

      for (const p of patients) {
        if (p.visits && p.visits.length > 0) {
          for (const v of p.visits) {
            visitCount++;
            if (v.triageLevel === 'Red') {
              redCount++;
            } else if (v.triageLevel === 'Yellow') {
              yellowCount++;
            } else if (v.triageLevel === 'Green') {
              greenCount++;
            }
          }
        }
      }

      this.triageRed.set(redCount);
      this.triageYellow.set(yellowCount);
      this.triageGreen.set(greenCount);
      this.totalVisits.set(visitCount);
    });
  }
}
